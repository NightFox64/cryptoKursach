namespace ChatClient.Models
{
    public enum ContactRequestStatus
    {
        Pending,
        Accepted,
        Declined
    }

    public class Contact
    {
        public int UserId { get; set; }
        public int ContactUserId { get; set; }
        public ContactRequestStatus Status { get; set; }
        public string? ContactUserName { get; set; } // To display in UI
    }
}
