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
        
        // Encryption settings for this chat
        public string? CipherAlgorithm { get; set; } // LOKI97, RC6, Aes
        public string? CipherMode { get; set; } // ECB, CBC, PCBC, CFB, OFB, CTR, RandomDelta
        public string? PaddingMode { get; set; } // PKCS7, Zeros, None
    }
}