using System.Collections.Generic;

namespace ChatServer.Models
{
    public class User
    {
        public int Id { get; set; }
        public string? Login { get; set; }
        public string? PasswordHash { get; set; }
        public byte[]? Salt { get; set; }
        public List<Contact> Contacts { get; set; } = new List<Contact>();
    }
}
