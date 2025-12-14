namespace ChatServer.Models
{
    public class Contact
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ContactId { get; set; }
        public ChatClient.Shared.Models.ContactRequestStatus Status { get; set; }
    }
}
