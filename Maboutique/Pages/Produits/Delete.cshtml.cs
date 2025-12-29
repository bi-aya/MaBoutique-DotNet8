using Maboutique.Data;
using Maboutique.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maboutique.Pages.Produits
{
    [Authorize(Roles = "Admin")] // <--- SEUL L'ADMIN PASSE
    public class DeleteModel : PageModel
    {
        private readonly Maboutique.Data.MaboutiqueContext _context;
        private readonly IDistributedCache _cache; // 1. Déclarer le cache

        // 2. Injecter le cache dans le constructeur
        public DeleteModel(MaboutiqueContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [BindProperty]
        public Produit Produit { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var produit = await _context.Produit.FirstOrDefaultAsync(m => m.Id == id);

            if (produit == null)
            {
                return NotFound();
            }
            else
            {
                Produit = produit;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var produit = await _context.Produit.FindAsync(id);
            if (produit != null)
            {
                Produit = produit;
                _context.Produit.Remove(Produit);
                await _context.SaveChangesAsync();

                // NETTOYAGE
                await _cache.RemoveAsync("produits_tous");
                await _cache.RemoveAsync($"produits_cat_{Produit.CategorieId}");
            }

            return RedirectToPage("./Index");
        }
    }
}
