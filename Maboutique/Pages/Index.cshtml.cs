using Maboutique.Data;
using Maboutique.Models;
using Maboutique.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Maboutique.Pages
{
    public class IndexModel : PageModel
    {
        private readonly MaboutiqueContext _context;
        private readonly OpenAIService _aiService;

        public IndexModel(MaboutiqueContext context, OpenAIService aiService)
        {
            _context = context;
            _aiService = aiService;
        }

        public IList<Produit> Produits { get; set; } = new List<Produit>();
        public IList<Categorie> Categories { get; set; } = new List<Categorie>();

        [BindProperty]
        public string QuestionUser { get; set; }
        public string ReponseIA { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CategorieId { get; set; }

        public async Task OnGetAsync()
        {
            await ChargerDonnees();
        }

        // --- C'EST ICI QUE LE RAG OPÈRE ---
        public async Task<IActionResult> OnPostAskAiAsync()
        {
            if (!string.IsNullOrWhiteSpace(QuestionUser))
            {
                // 1. ÉTAPE RETRIEVAL (Récupération)
                // On cherche sommairement dans la base de données ce qui pourrait correspondre
                // pour donner du contexte à l'IA.

                var motsCles = QuestionUser.ToLower().Split(' '); // On découpe la phrase

                // Recherche simple : on prend les produits qui contiennent un des mots
                // (C'est un RAG simplifié, un vrai RAG utiliserait des vecteurs)
                // 1. On nettoie la phrase (minuscules)
                string phraseClient = QuestionUser.ToLower();

                // 2. On récupère TOUS les produits (pour faire le tri en mémoire, plus facile pour le code complexe)
                // Note: Sur une vraie grosse base de données, on ferait différemment, mais pour notre projet c'est parfait.
                var tousLesProduits = await _context.Produit
                    .Include(p => p.Categorie)
                    .ToListAsync();

                // 3. Filtrage intelligent 
                var produitsCandidats = tousLesProduits
                    .Where(p =>
                        // A. Est-ce que la phrase du client contient le TITRE du livre ?
                        // Ex: Client dit "Avez-vous Les Misérables ?", Titre="Les Misérables" -> OUI
                        (!string.IsNullOrEmpty(p.Nom) && phraseClient.Contains(p.Nom.ToLower()))

                        || // OU

                        // B. Est-ce que la phrase du client contient la CATÉGORIE ?
                        // Ex: Client dit "Je veux un Roman", Categorie="Roman" -> OUI
                        (p.Categorie != null && !string.IsNullOrEmpty(p.Categorie.Nom) && phraseClient.Contains(p.Categorie.Nom.ToLower()))

                        || // OU

                        // C. Recherche par mot-clé simple (si le titre contient un mot de la phrase)
                        // Ex: Client dit "Livre sur Napoléon", Titre="Napoléon Bonaparte" -> OUI
                        (phraseClient.Split(' ').Any(mot => mot.Length > 3 && p.Nom.ToLower().Contains(mot)))
                    )
                    .Take(10) // On garde les 10 meilleurs
                    .ToList();

                // 4. Sécurité : Si on ne trouve rien, on envoie quand même quelques produits au hasard
                // pour que l'IA ne soit pas "aveugle" et puisse proposer autre chose.
                if (!produitsCandidats.Any())
                {
                    produitsCandidats = tousLesProduits.Take(5).ToList();
                }

                // 2. ÉTAPE GENERATION (L'IA répond avec ces produits)
                ReponseIA = await _aiService.ConseillerClient(QuestionUser, produitsCandidats);
            }

            await ChargerDonnees();
            return Page();
        }

        // --- ON POST (Panier Cookie) ---
        public IActionResult OnPostAddToCart(int id)
        {
            List<PanierCookieItem> panier = new List<PanierCookieItem>();
            var cookie = Request.Cookies["MonPanier"];
            if (!string.IsNullOrEmpty(cookie))
            {
                try { panier = JsonSerializer.Deserialize<List<PanierCookieItem>>(cookie); } catch { }
            }

            var item = panier.FirstOrDefault(p => p.ProduitId == id);
            if (item != null) item.Quantite++;
            else panier.Add(new PanierCookieItem { ProduitId = id, Quantite = 1 });

            Response.Cookies.Append("MonPanier", JsonSerializer.Serialize(panier), new CookieOptions { Expires = DateTime.Now.AddDays(7), IsEssential = true });

            TempData["SuccesAjout"] = "Ajouté au panier !";

            // Redirection intelligente : on garde la catégorie sélectionnée
            return RedirectToPage(new { CategorieId = CategorieId });
        }

        // --- MÉTHODE DE CHARGEMENT COMPLÈTE ---
        private async Task ChargerDonnees()
        {
            // Charger les catégories pour le filtre
            Categories = await _context.Categorie.ToListAsync();

            // Charger les produits avec filtre éventuel
            IQueryable<Produit> query = _context.Produit.Include(p => p.Categorie);
            if (CategorieId.HasValue) query = query.Where(p => p.CategorieId == CategorieId.Value);

            Produits = await query.ToListAsync() ?? new List<Produit>();
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