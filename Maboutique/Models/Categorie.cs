using System.ComponentModel.DataAnnotations;

namespace Maboutique.Models
{
    public class Categorie
    {
        public int Id { get; set; }
        [Required]
        public string Nom { get; set; }
        public List<Produit> Produits { get; set; } = new();
    }
}
