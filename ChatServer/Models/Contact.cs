namespace ChatServer.Models
{
    public enum ContactRequestStatus
    {
        Pending,
        Accepted,
        Declined
    }

    public class Contact
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ContactId { get; set; }
        public ContactRequestStatus Status { get; set; }
    }
}
