using Maboutique.Data;
using Maboutique.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Maboutique.Pages
{
    public class DetailsModel : PageModel
    {
        private readonly MaboutiqueContext _context;
        private readonly UserManager<IdentityUser> _userManager; // Pour savoir qui est connecté

        public DetailsModel(MaboutiqueContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Produit Produit { get; set; } = default!;

        // Pour afficher la moyenne
        public double NoteMoyenne { get; set; }
        public int NombreAvis { get; set; }
        // Pour savoir si l'utilisateur actuel a déjà voté
        public bool ADejaVote { get; set; }

        // Pour le formulaire d'ajout d'avis (BindProperty est important)
        [BindProperty]
        public int NouvelleNote { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            // 1. Charger le produit
            Produit = await _context.Produit
                .Include(p => p.Categorie)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (Produit == null) return NotFound();

            // 2. Calculer les stats des avis
            var avisDuProduit = _context.Avis.Where(a => a.ProduitId == id);
            NombreAvis = await avisDuProduit.CountAsync();
            if (NombreAvis > 0)
            {
                NoteMoyenne = await avisDuProduit.AverageAsync(a => a.Note);
            }

            // 3. Vérifier si l'utilisateur connecté a déjà voté
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                ADejaVote = await avisDuProduit.AnyAsync(a => a.UserId == user.Id);
            }

            return Page();
        }

        // Gestionnaire pour AJOUTER UN AVIS
        public async Task<IActionResult> OnPostSubmitReviewAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge(); // Redirige vers la connexion si pas connecté

            // Vérification basique
            if (NouvelleNote < 1 || NouvelleNote > 5) return RedirectToPage(new { id });

            // Créer et sauvegarder l'avis
            var avis = new Avis
            {
                ProduitId = id,
                UserId = user.Id,
                Note = NouvelleNote,
                DatePublication = DateTime.Now
            };

            _context.Avis.Add(avis);
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id }); // Recharge la page
        }

        // Logique d'ajout au panier (Identique à l'Index, mais redirige vers le Panier ou reste ici)
        // ** COOKIE SIMPLE (LISTE DANS LE COOKIE)  on ne touche plus à _context **
        //On lit le cookie, on modifie la liste en mémoire, et on réécrit le cookie.
        public IActionResult OnPostAddToCart(int id, int quantite)
        {
            if (quantite < 1) quantite = 1;

            List<PanierCookieItem> panier = new List<PanierCookieItem>();

            // 1. Lire le cookie existant (s'il y en a un)
            var cookie = Request.Cookies["MonPanier"];
            if (!string.IsNullOrEmpty(cookie))
            {
                panier = JsonSerializer.Deserialize<List<PanierCookieItem>>(cookie);
            }

            // 2. Vérifier si le produit est déjà dedans
            var itemExistant = panier.FirstOrDefault(p => p.ProduitId == id);
            if (itemExistant != null)
            {
                itemExistant.Quantite += quantite;
            }
            else
            {
                panier.Add(new PanierCookieItem { ProduitId = id, Quantite = quantite });
            }

            // 3. Sauvegarder dans le cookie (Sérialisation JSON)
            var optionsCookie = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(7),
                HttpOnly = true, // Sécurité : empêche le JavaScript de lire le cookie
                IsEssential = true
            };

            string jsonPanier = JsonSerializer.Serialize(panier);
            Response.Cookies.Append("MonPanier", jsonPanier, optionsCookie);

            TempData["SuccesAjout"] = "Produit ajouté au panier (Cookie) !";
            return RedirectToPage();
        }

        /* ** COOKIE AVEC BASE DE DONNÉES (CARTITEM)**
        public async Task<IActionResult> OnPostAddToCartAsync(int id, int quantite)
        {
            // Sécurité : on s'assure qu'on ajoute au moins 1 article
            if (quantite < 1) quantite = 1;

            // 1. Gestion du Cookie (Code inchangé)
            string cartId = Request.Cookies["CartId"];
            if (string.IsNullOrEmpty(cartId))
            {
                cartId = Guid.NewGuid().ToString();
                var cookieOptions = new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(30),
                    HttpOnly = true,
                    IsEssential = true
                };
                Response.Cookies.Append("CartId", cartId, cookieOptions);
            }

            // 2. Gestion de la base de données
            var cartItem = await _context.CartItem
                .FirstOrDefaultAsync(c => c.CartId == cartId && c.ProduitId == id);

            if (cartItem != null)
            {
                // AJOUTER LA QUANTITÉ CHOISIE (et non plus juste ++1)
                cartItem.Quantity += quantite;
            }
            else
            {
                cartItem = new CartItem
                {
                    CartId = cartId,
                    ProduitId = id,
                    Quantity = quantite // Utiliser la quantité choisie
                };
                _context.CartItem.Add(cartItem);
            }

            await _context.SaveChangesAsync();

            // 3. Message de succès (Feedback visuel)
            TempData["SuccesAjout"] = "Produit ajouté au panier avec succès !";

            // 4. RESTER SUR LA PAGE (Ne pas mettre d'argument recharge la page actuelle)
            return RedirectToPage();
        }*/
    }
}