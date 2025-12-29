using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Maboutique.Models
{
    public class Avis
    {
        public int Id { get; set; }

        [Range(1, 5)]
        public int Note { get; set; } // De 1 à 5 étoiles

        public DateTime DatePublication { get; set; } = DateTime.Now;

        // Lien avec le Produit
        public int ProduitId { get; set; }
        public Produit Produit { get; set; }

        // Lien avec l'Utilisateur (Identity)
        public string UserId { get; set; }
        public IdentityUser User { get; set; }
    }
}