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
    public class IndexModel : PageModel
    {
        private readonly Maboutique.Data.MaboutiqueContext _context;

        public IndexModel(Maboutique.Data.MaboutiqueContext context)
        {
            _context = context;
        }

        public IList<Produit> Produit { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Produit = await _context.Produit
                .Include(p => p.Categorie).ToListAsync();
        }
    }
}
