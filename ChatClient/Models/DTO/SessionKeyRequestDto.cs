namespace ChatClient.Models.DTO
{
    public class SessionKeyRequestDto
    {
        public int ChatId { get; set; }
        public int UserId { get; set; }
        public string? PublicKey { get; set; }
    }
}
