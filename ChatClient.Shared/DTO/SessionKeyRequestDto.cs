using System.Numerics;

namespace ChatClient.Shared.Models.DTO
{
    public class SessionKeyRequestDto
    {
        public int ChatId { get; set; }
        public int UserId { get; set; }
        public string? PublicKey { get; set; } // Represented as string for transport
    }
}
