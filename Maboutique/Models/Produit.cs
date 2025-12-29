using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maboutique.Models
{
    public class Produit
    {
        public int Id { get; set; }
        [Required]
        public string Nom { get; set; }
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Prix { get; set; }
        public string ImageUrl { get; set; } = "https://placehold.co/200"; // Image par défaut

        public int Quantité { get; set; }

        public int CategorieId { get; set; }
        public Categorie Categorie { get; set; }
    }
}
