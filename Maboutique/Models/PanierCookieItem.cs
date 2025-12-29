/*PanierCookieItem est une classe "temporaire" qui sert juste à structurer les données 
 * avant de les transformer en texte (JSON) pour le cookie.
Elle n'existe que dans la mémoire vive (RAM) et dans le navigateur du client.*/
namespace Maboutique.Models
{
    public class PanierCookieItem
    {
        public int ProduitId { get; set; }
        public int Quantite { get; set; }
    }
}