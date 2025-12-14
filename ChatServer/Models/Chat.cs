using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatServer.Models
{
    public class Chat
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<int> UserIds { get; set; } = new List<int>(); // Participants
    }
}