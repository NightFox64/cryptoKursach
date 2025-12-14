using ChatClient.Shared.Models; // For ContactRequestStatus

namespace ChatClient.Shared.Models.DTO
{
    public class ContactDto
    {
        public int UserId { get; set; }
        public int ContactId { get; set; }
        public ContactRequestStatus Status { get; set; }
        public string ContactUserName { get; set; }
    }
}
