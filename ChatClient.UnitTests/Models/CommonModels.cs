namespace ChatClient.UnitTests.Models
{
    public class RegisterDto
    {
        public string? Login { get; set; }
        public string? Password { get; set; }
    }

    public class LoginDto
    {
        public string? Login { get; set; }
        public string? Password { get; set; }
    }

    public class SessionKeyRequestDto
    {
        public int ChatId { get; set; }
        public int UserId { get; set; }
        public string? PublicKey { get; set; }
    }

    public class Chat
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class Message
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int SenderId { get; set; }
        public string? Content { get; set; }
        public long DeliveryId { get; set; }
    }
}
