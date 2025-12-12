namespace ChatServer.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int SenderId { get; set; }
        public string? Content { get; set; }
        public long DeliveryId { get; set; }
    }
}
