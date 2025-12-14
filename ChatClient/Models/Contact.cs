namespace ChatClient.Models
{
    public class Contact
    {
        public int UserId { get; set; }
        public int ContactUserId { get; set; } // The ID of the contact's user
        public ChatClient.Shared.Models.ContactRequestStatus Status { get; set; }
        public string? ContactUserName { get; set; } // To display in UI
    }
}
