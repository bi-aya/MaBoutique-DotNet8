using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Maboutique.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Maboutique.Data
{
    public class MaboutiqueContext : IdentityDbContext
    {
        public MaboutiqueContext (DbContextOptions<MaboutiqueContext> options)
            : base(options)
        {
        }

        public DbSet<Maboutique.Models.Categorie> Categorie { get; set; } = default!;
        public DbSet<Maboutique.Models.Produit> Produit { get; set; } = default!;
        public DbSet<Avis> Avis { get; set; } = default!;
    }
}
