using Maboutique.Data;
using Maboutique.Models;
using Maboutique.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed; // Nécessaire pour Redis
using System.Text.Json;
using System.Text.Json.Serialization; // Pour gérer les cycles (Catégorie <-> Produit)

namespace Maboutique.Pages
{
    /// <summary>
    /// Contrôleur de la page d'accueil.
    /// Gère l'affichage des produits, le cache Redis, l'IA et le panier.
    /// </summary>
    public class IndexModel : PageModel
    {

        private readonly MaboutiqueContext _context; 
        private readonly OpenAIService _aiService;   // Notre service IA (Groq)
        private readonly IDistributedCache _cache;   // Service de Cache (Redis)

        public IndexModel(MaboutiqueContext context, OpenAIService aiService, IDistributedCache cache)
        {
            _context = context;
            _aiService = aiService;
            _cache = cache;
        }

        // Données affichées dans la Vue
        public IList<Produit> Produits { get; set; } = new List<Produit>();
        public IList<Categorie> Categories { get; set; } = new List<Categorie>();

        // Liste spécifique pour les résultats trouvés par le RAG (IA)
        public IList<Produit> ProduitsSuggeres { get; set; } = new List<Produit>();

        [BindProperty]
        public string QuestionUser { get; set; }

        // Stocke la réponse textuelle de l'IA
        public string ReponseIA { get; set; }

        // Paramètre d'URL pour filtrer (ex: /?CategorieId=1)
        [BindProperty(SupportsGet = true)]
        public int? CategorieId { get; set; }

        // --- MÉTHODE GET (Chargement initial) ---
        public async Task OnGetAsync()
        {
            // On charge les données en utilisant la stratégie de Cache
            await ChargerDonneesAvecRedis();
        }

        // --- MÉTHODE POST : INTERACTION IA (RAG) ---
        public async Task<IActionResult> OnPostAskAiAsync()
        {
            if (!string.IsNullOrWhiteSpace(QuestionUser))
            {
                // ÉTAPE 1 : RETRIEVAL (Récupération du contexte)
                // On prépare la recherche textuelle
                string phraseClient = QuestionUser.ToLower();

                // On découpe la phrase en mots-clés (en ignorant les mots courts comme "le", "de")
                var motsClient = phraseClient.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                             .Where(m => m.Length > 2).ToList();

                // On s'assure que les données sont chargées (depuis Redis ou SQL)
                await ChargerDonneesAvecRedis();
                var tousLesProduits = Produits;

                // Algorithme de filtrage "Full-Text" simplifié (C# LINQ)
                var candidats = tousLesProduits
                    .Where(p =>
                        // A. Recherche exacte dans le nom du produit
                        (!string.IsNullOrEmpty(p.Nom) && phraseClient.Contains(p.Nom.ToLower())) ||

                        // B. Recherche dans le nom de la catégorie (ex: "Je veux un Roman")
                        (p.Categorie != null && !string.IsNullOrEmpty(p.Categorie.Nom) && phraseClient.Contains(p.Categorie.Nom.ToLower())) ||

                        // C. Recherche par mots-clés épars
                        motsClient.Any(mot => p.Nom.ToLower().Contains(mot))
                    )
                    .Take(10).ToList(); // On limite à 10 pour ne pas surcharger le prompt

                // Fallback : Si rien trouvé, on donne quelques produits par défaut pour que l'IA ne soit pas muette
                if (!candidats.Any()) candidats = tousLesProduits.Take(5).ToList();

                // On stocke les suggestions pour l'affichage "Panier Discret"
                ProduitsSuggeres = candidats;

                // ÉTAPE 2 : GENERATION (Appel à l'API)
                // On envoie la question + les produits trouvés à l'IA
                ReponseIA = await _aiService.ConseillerClient(QuestionUser, candidats);
            }
            else
            {
                // Si la question est vide, on recharge juste la page normalement
                await ChargerDonneesAvecRedis();
            }

            return Page();
        }

        // --- MÉTHODE POST : AJOUT PANIER (Stateless / Cookie) ---
        public IActionResult OnPostAddToCart(int id)
        {
            List<PanierCookieItem> panier = new List<PanierCookieItem>();

            // 1. Lire le cookie existant
            var cookie = Request.Cookies["MonPanier"];
            if (!string.IsNullOrEmpty(cookie))
            {
                try { panier = JsonSerializer.Deserialize<List<PanierCookieItem>>(cookie); } catch { }
            }

            // 2. Mise à jour de la quantité ou ajout
            var item = panier.FirstOrDefault(p => p.ProduitId == id);
            if (item != null) item.Quantite++;
            else panier.Add(new PanierCookieItem { ProduitId = id, Quantite = 1 });

            // 3. Sauvegarde dans le navigateur (Expiration 7 jours)
            Response.Cookies.Append("MonPanier", JsonSerializer.Serialize(panier), new CookieOptions { Expires = DateTime.Now.AddDays(7), IsEssential = true });

            // Redirection vers la même page en gardant le filtre actif
            return RedirectToPage(new { CategorieId = CategorieId });
        }

        // --- MÉTHODE PRIVÉE : PATTERN CACHE-ASIDE (Redis) ---
        private async Task ChargerDonneesAvecRedis()
        {
            // 1. Toujours charger les catégories depuis SQL (Volume faible, change peu)
            Categories = await _context.Categorie.ToListAsync();

            // 2. Gestion des Produits avec Redis
            string cacheKey = "produits_tous"; // Clé unique pour stocker la liste
            string produitsJson = null;

            // Si un filtre est actif, on bypass le cache global pour faire une requête précise
            if (CategorieId.HasValue)
            {
                Produits = await _context.Produit
                    .Include(p => p.Categorie)
                    .Where(p => p.CategorieId == CategorieId)
                    .ToListAsync();
                return;
            }

            // A. Tentative de lecture dans Redis (Cache Hit ?)
            try
            {
                produitsJson = await _cache.GetStringAsync(cacheKey);
            }
            catch (Exception)
            {
                // Fail-Safe : Si Redis est éteint, on continue sans planter
                Console.WriteLine("Redis non disponible");
            }

            if (!string.IsNullOrEmpty(produitsJson))
            {
                // CACHE HIT : Données trouvées dans la RAM (Rapide)
                var options = new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles };
                Produits = JsonSerializer.Deserialize<List<Produit>>(produitsJson, options);
            }
            else
            {
                // CACHE MISS : Données absentes, lecture SQL (Lent)
                Produits = await _context.Produit.Include(p => p.Categorie).ToListAsync();

                // B. Mise en cache pour la prochaine fois (Write-Back)
                try
                {
                    var options = new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles };
                    var serialized = JsonSerializer.Serialize(Produits, options);

                    // Configuration de l'expiration (TTL : Time To Live) de 10 minutes
                    var cacheOptions = new DistributedCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

                    await _cache.SetStringAsync(cacheKey, serialized, cacheOptions);
                }
                catch (Exception) { /* Ignorer les erreurs d'écriture cache */ }
            }
        }


        /*  Le cookie "CartId" contient un identifiant unique pour le panier de l'utilisateur.
Chaque fois qu'un utilisateur ajoute un produit, on utilise cet ID pour retrouver son panier en base de données.
Si le cookie n'existe pas, on le crée avec un nouvel ID unique.

public async Task<IActionResult> OnPostAddToCartAsync(int id)
{
// 1. Essayer de lire l'ID du panier depuis les COOKIES (et non la Session)
string cartId = Request.Cookies["CartId"];

// 2. Si le cookie n'existe pas, on le crée
if (string.IsNullOrEmpty(cartId))
{
cartId = Guid.NewGuid().ToString();

// Configuration du cookie pour qu'il dure 30 jours
var cookieOptions = new CookieOptions
{
    Expires = DateTime.Now.AddDays(30), // durée de vie
    HttpOnly = true, // Sécurité : inaccessible via JavaScript
    IsEssential = true
};

Response.Cookies.Append("CartId", cartId, cookieOptions);
}

// 3. Le reste de la logique 
var cartItem = await _context.CartItem
.FirstOrDefaultAsync(c => c.CartId == cartId && c.ProduitId == id);

if (cartItem != null)
{
cartItem.Quantity++;
}
else
{
cartItem = new CartItem
{
    CartId = cartId,
    ProduitId = id,
    Quantity = 1
};
_context.CartItem.Add(cartItem);
}
// Sauvegarder et recharger la page
await _context.SaveChangesAsync();

return RedirectToPage();// Reste sur la même page après l'ajout
}*/

        /*  Avec utilisation de session pour le panier
        // 5. Gestionnaire pour le bouton "Ajouter au panier"
        public async Task<IActionResult> OnPostAddToCartAsync(int id)
        {
            // A. Récupérer ou Créer l'ID de session du panier
            string cartId = HttpContext.Session.GetString("CartId");

            if (string.IsNullOrEmpty(cartId))
            {
                cartId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("CartId", cartId);
            }

            // B. Vérifier si l'article est déjà dans le panier pour ce cartId
            var cartItem = await _context.CartItem
                .FirstOrDefaultAsync(c => c.CartId == cartId && c.ProduitId == id);

            if (cartItem != null)
            {
                // Si oui, on augmente la quantité
                cartItem.Quantity++;
            }
            else
            {
                // Sinon, on crée une nouvelle ligne dans le panier
                cartItem = new CartItem
                {
                    CartId = cartId,
                    ProduitId = id,
                    Quantity = 1
                };
                _context.CartItem.Add(cartItem);
            }

            // C. Sauvegarder et recharger la page
            await _context.SaveChangesAsync();

            return RedirectToPage(); // Reste sur la même page après l'ajout
        }
        */
    }
}