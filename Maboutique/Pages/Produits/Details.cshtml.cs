using Maboutique.Data;
using Maboutique.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maboutique.Pages.Produits
{
    [Authorize(Roles = "Admin")] // <--- SEUL L'ADMIN PASSE
    public class DetailsModel : PageModel
    {
        private readonly Maboutique.Data.MaboutiqueContext _context;

        public DetailsModel(Maboutique.Data.MaboutiqueContext context)
        {
            _context = context;
        }

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
    }
}
