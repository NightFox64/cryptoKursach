using System.Numerics;
using System.Text.Json.Serialization;

namespace ChatClient.Shared.Models.DTO
{
    public class SessionKeyRequestDto
    {
        [JsonPropertyName("chatId")]
        public int ChatId { get; set; }
        [JsonPropertyName("userId")]
        public int UserId { get; set; }
        [JsonPropertyName("publicKey")]
        public string? PublicKey { get; set; } // Represented as string for transport
    }
}
