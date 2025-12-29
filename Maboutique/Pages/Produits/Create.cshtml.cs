using Maboutique.Data;
using Maboutique.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maboutique.Pages.Produits
{
    [Authorize(Roles = "Admin")] // <--- SEUL L'ADMIN PASSE
    public class CreateModel : PageModel
    {
        private readonly Maboutique.Data.MaboutiqueContext _context;
        private readonly IDistributedCache _cache; // 1. Déclarer le cache

        // 2. Injecter le cache dans le constructeur
        public CreateModel(MaboutiqueContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public IActionResult OnGet()
        {
        ViewData["CategorieId"] = new SelectList(_context.Categorie, "Id", "Nom");
            return Page();
        }

        [BindProperty]
        public Produit Produit { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // On a seulement besoin de l'ID (qui est déjà là), pas de l'objet complet.
            ModelState.Remove("Produit.Categorie");

            if (!ModelState.IsValid)
            {
                // Sinon, l'utilisateur verra une erreur ou une liste vide.
                ViewData["CategorieId"] = new SelectList(_context.Categorie, "Id", "Nom");
                return Page();
            }

            _context.Produit.Add(Produit);
            await _context.SaveChangesAsync();

            // NETTOYAGE
            await _cache.RemoveAsync("produits_tous");
            await _cache.RemoveAsync($"produits_cat_{Produit.CategorieId}");

            return RedirectToPage("./Index");
        }

    }
}
