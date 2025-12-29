using Maboutique.Data;
using Maboutique.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maboutique.Pages.Produits
{
    [Authorize(Roles = "Admin")] // <--- SEUL L'ADMIN PASSE
    public class EditModel : PageModel
    {
        private readonly Maboutique.Data.MaboutiqueContext _context;
        private readonly IDistributedCache _cache; // 1. Déclarer le cache

        // 2. Injecter le cache dans le constructeur
        public EditModel(MaboutiqueContext context, IDistributedCache cache)
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

            var produit =  await _context.Produit.FirstOrDefaultAsync(m => m.Id == id);
            if (produit == null)
            {
                return NotFound();
            }
            Produit = produit;
           ViewData["CategorieId"] = new SelectList(_context.Categorie, "Id", "Nom");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // 1. IMPORTANT : On ignore l'erreur de validation concernant l'objet Categorie manquant
            ModelState.Remove("Produit.Categorie");

            if (!ModelState.IsValid)
            {
                // 2. Si la validation échoue (ex: Nom vide), on doit recharger la liste déroulante
                // sinon elle sera vide au rechargement de la page.
                ViewData["CategorieId"] = new SelectList(_context.Categorie, "Id", "Nom");
                return Page();
            }

            _context.Attach(Produit).State = EntityState.Modified;

            try{
                await _context.SaveChangesAsync();
                // --- CACHE ---

                // A. On supprime la liste principale "Tous les produits"
                await _cache.RemoveAsync("produits_tous");

                // B. (Optionnel mais recommandé) On supprime aussi le cache de la catégorie du produit
                // car la liste de cette catégorie a changé aussi.
                await _cache.RemoveAsync($"produits_cat_{Produit.CategorieId}");

                // -----------------------------
            }
            catch (DbUpdateConcurrencyException){
                if (!ProduitExists(Produit.Id)){
                    return NotFound();
                }else{
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool ProduitExists(int id)
        {
            return _context.Produit.Any(e => e.Id == id);
        }
    }
}
