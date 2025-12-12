using System.Collections.Generic;

namespace ChatServer.Models
{
    public class Chat
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public List<int> UserIds { get; set; } = new List<int>();
    }
}
