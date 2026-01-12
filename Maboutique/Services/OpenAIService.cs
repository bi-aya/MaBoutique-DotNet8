using Maboutique.Models; // Pour accéder à la classe Produit
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text; // Pour StringBuilder

namespace Maboutique.Services
{
    public class OpenAIService
    {
        private readonly string _apiKey;

        public OpenAIService(IConfiguration configuration)
        {
            _apiKey = configuration["OpenAI:ApiKey"];
        }

        // On ajoute un paramètre : la liste des produits pertinents
        public async Task<string> ConseillerClient(string questionClient, List<Produit> catalogue)
        {
            if (string.IsNullOrEmpty(_apiKey)) return "Désolé, je suis hors ligne.";

            try
            {
                // 1. Transformer la liste des produits en texte pour l'IA
                StringBuilder contexteProduits = new StringBuilder();
                if (catalogue != null && catalogue.Any())
                {
                    contexteProduits.AppendLine("VOICI LES LIVRES DISPONIBLES EN STOCK :");
                    foreach (var p in catalogue)
                    {
                        contexteProduits.AppendLine($"- Titre: {p.Nom}, Prix: {p.Prix}DH, Desc: {p.Description}");
                    }
                }
                else
                {
                    contexteProduits.AppendLine("AUCUN LIVRE TROUVÉ DANS LE STOCK CORRESPONDANT À LA DEMANDE.");
                }

                // 2. Configuration Groq
                var options = new OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") };
                ChatClient client = new(model: "llama-3.3-70b-versatile", credential: new ApiKeyCredential(_apiKey), options: options);

                // 3. Le Prompt RAG
                string systemPrompt = "Tu es un vendeur expert chez 'MaBoutique'. " +
                                      "Utilise UNIQUEMENT les informations fournies dans la liste 'VOICI LES LIVRES DISPONIBLES' pour répondre. " +
                                      "Si le livre n'est pas dans la liste, dis poliment que nous ne l'avons pas. " +
                                      "Ne propose jamais de livres qui ne sont pas dans la liste fournie. " +
                                      "Sois bref et commercial.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt + "\n\n" + contexteProduits.ToString()), // On injecte les données 
                    new UserChatMessage(questionClient)
                };

                ChatCompletion completion = await client.CompleteChatAsync(messages);
                return completion.Content[0].Text;
            }
            catch (Exception ex)
            {
                return $"Erreur IA : {ex.Message}";
            }
        }
    }
}