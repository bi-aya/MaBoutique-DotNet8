using Maboutique.Data;
using Maboutique.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed; // Nécessaire pour le cache
using System.Text.Json; // Nécessaire pour convertir en texte
using Microsoft.AspNetCore.Mvc;         

namespace Maboutique.Pages
{
    public class IndexModel : PageModel
    {
        private readonly MaboutiqueContext _context;
        private readonly IDistributedCache _cache; // Injection du Cache

        public IndexModel(MaboutiqueContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public IList<Produit> Produits { get; set; } = default!;
        public IList<Categorie> Categories { get; set; } = default!;

        [BindProperty(SupportsGet = true)]
        public int? CategorieId { get; set; }

        public async Task OnGetAsync()
        {
            Categories = await _context.Categorie.ToListAsync();

            // --- LOGIQUE REDIS ---
            string cacheKey = CategorieId.HasValue ? $"produits_cat_{CategorieId}" : "produits_tous";

            // 1. Essayer de récupérer depuis Redis
            string cachedProduits = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedProduits))
            {
                // HIT : Trouvé dans le cache ! On désérialise (Texte -> Objets)
                // ReferenceHandler.Preserve est important si vous avez des boucles (Produit -> Categorie -> Produits...)
                // Mais ici, simplifions.
                Produits = JsonSerializer.Deserialize<List<Produit>>(cachedProduits);
            }
            else
            {
                // MISS : Pas dans le cache. On va chercher en Base de Données.
                var query = _context.Produit.Include(p => p.Categorie).AsQueryable();

                if (CategorieId.HasValue)
                {
                    query = query.Where(p => p.CategorieId == CategorieId.Value);
                }

                Produits = await query.ToListAsync();

                // 2. Sauvegarder dans Redis pour la prochaine fois
                var options = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10)); // Garder 10 minutes max
                                                                      // .SetSlidingExpiration(...) // Option : Repousser l'expiration si consulté

                // Important : Ignorer les cycles (Produit contient Categorie qui contient Produits...)
                var jsonOptions = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                string jsonString = JsonSerializer.Serialize(Produits, jsonOptions);

                await _cache.SetStringAsync(cacheKey, jsonString, options);
            }
        }

        // 5. Gestionnaire pour le bouton "Ajouter au panier" utilisant les COOKIES
        // Cette méthode est une copie de celle dans Details.cshtml.cs
        public IActionResult OnPostAddToCart(int id)
        {
            // --- LOGIQUE COOKIE (Copie de Details) ---

            List<PanierCookieItem> panier = new List<PanierCookieItem>();

            // 1. Lire le cookie existant
            var cookie = Request.Cookies["MonPanier"];
            if (!string.IsNullOrEmpty(cookie))
            {
                panier = JsonSerializer.Deserialize<List<PanierCookieItem>>(cookie);
            }

            // 2. Gestion du produit dans la liste
            var itemExistant = panier.FirstOrDefault(p => p.ProduitId == id);
            if (itemExistant != null)
            {
                itemExistant.Quantite++; // Sur l'accueil, on ajoute +1 par défaut
            }
            else
            {
                panier.Add(new PanierCookieItem { ProduitId = id, Quantite = 1 });
            }

            // 3. Sauvegarder (Réécrire le cookie)
            var optionsCookie = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(7),
                HttpOnly = true,
                IsEssential = true
            };

            Response.Cookies.Append("MonPanier", JsonSerializer.Serialize(panier), optionsCookie);

            // 4. Feedback utilisateur
            TempData["SuccesAjout"] = "Produit ajouté au panier !";

            // 5. Rester sur la page d'accueil (ou la page filtrée actuelle)
            // L'astuce ici est de garder le filtre de catégorie s'il y en avait un
            return RedirectToPage(new { CategorieId = CategorieId });
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