using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatServer.Models
{
    public class User
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string? Login { get; set; }
        public string? PasswordHash { get; set; }
        public byte[]? Salt { get; set; }
        public List<Contact> Contacts { get; set; } = new List<Contact>(); // This Contact will be ChatServer.Models.Contact
    }
}
