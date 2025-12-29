using Maboutique.Data;
using Maboutique.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Maboutique.Pages
{
    /* Classe pour afficher les articles du panier avec les détails du produit
     Utile pour l'affichage dans la vue
    */
    /*Le cookie ne contient que les ID (1, 5, 12...). Il faut demander à la Base de Données les infos (Nom, Prix, Image) correspondantes à ces ID.
     Classe "ViewModel" est juste pour l'affichage (qui combine l'info du cookie et l'info de la DB).*/
    public class CartDisplayItem
    {
        public Produit Produit { get; set; }
        public int Quantity { get; set; }
    }

    public class CartModel : PageModel
    {
        private readonly MaboutiqueContext _context;

        public CartModel(MaboutiqueContext context)
        {
            _context = context;
        }

        public List<CartDisplayItem> CartItems { get; set; } = new List<CartDisplayItem>();
        public decimal Total { get; set; }

        // Gestion de l'affichage du panier
        public async Task OnGetAsync()
        {
            // 1. Lire le cookie
            var cookie = Request.Cookies["MonPanier"];
            if (string.IsNullOrEmpty(cookie)) return; // Panier vide

            var panierCookie = JsonSerializer.Deserialize<List<PanierCookieItem>>(cookie);
            if (panierCookie == null || !panierCookie.Any()) return;

            // 2. Récupérer les IDs des produits du cookie
            var productIds = panierCookie.Select(p => p.ProduitId).ToList();

            // 3. Chercher les infos complètes en DB (en une seule requête)
            var produitsEnDb = await _context.Produit
                                             .Where(p => productIds.Contains(p.Id))
                                             .ToListAsync();

            // 4. Combiner (Jointure manuelle) Cookie + DB
            foreach (var itemCookie in panierCookie)
            {
                var produitReel = produitsEnDb.FirstOrDefault(p => p.Id == itemCookie.ProduitId);
                if (produitReel != null)
                {
                    CartItems.Add(new CartDisplayItem
                    {
                        Produit = produitReel,
                        Quantity = itemCookie.Quantite
                    });

                    Total += produitReel.Prix * itemCookie.Quantite;
                }
            }
        }

        // Gestion de la suppression (UPDATE DU COOKIE)
        public IActionResult OnPostRemove(int id)
        {
            var cookie = Request.Cookies["MonPanier"];
            if (!string.IsNullOrEmpty(cookie))
            {
                var panier = JsonSerializer.Deserialize<List<PanierCookieItem>>(cookie);

                // On retire l'élément
                var itemASupprimer = panier.FirstOrDefault(p => p.ProduitId == id);
                if (itemASupprimer != null)
                {
                    panier.Remove(itemASupprimer);

                    // On réécrit le cookie
                    Response.Cookies.Append("MonPanier", JsonSerializer.Serialize(panier), new CookieOptions { Expires = DateTime.Now.AddDays(7) });
                }
            }
            return RedirectToPage();
        }
        // Gestion de la mise à jour de la quantité (UPDATE DU COOKIE)
        public IActionResult OnPostUpdateQuantity(int id, int change)
        {
            // 1. Lire le cookie
            var cookie = Request.Cookies["MonPanier"];
            if (!string.IsNullOrEmpty(cookie))
            {
                var panier = JsonSerializer.Deserialize<List<PanierCookieItem>>(cookie);

                // 2. Trouver l'article
                var item = panier.FirstOrDefault(p => p.ProduitId == id);

                if (item != null)
                {
                    // 3. Modifier la quantité
                    item.Quantite += change;

                    // Sécurité : On ne descend pas en dessous de 1
                    if (item.Quantite < 1) item.Quantite = 1;

                    // (Optionnel) Limite Max : ex 99
                    if (item.Quantite > 99) item.Quantite = 99;

                    // 4. Sauvegarder le cookie mis à jour
                    var optionsCookie = new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(7),
                        HttpOnly = true,
                        IsEssential = true
                    };

                    Response.Cookies.Append("MonPanier", JsonSerializer.Serialize(panier), optionsCookie);
                }
            }

            // 5. Recharger la page
            return RedirectToPage();
        }
    }
}