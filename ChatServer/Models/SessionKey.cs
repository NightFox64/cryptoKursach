using System.Numerics;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatServer.Models
{
    public class SessionKey
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int UserId { get; set; }
        public BigInteger ServerPrivateKey { get; set; }
        public BigInteger ServerPublicKey { get; set; }
        public BigInteger ClientPublicKey { get; set; } // Stored after client sends it
        public required byte[] SymmetricKey { get; set; } // The derived symmetric key for encryption/decryption
        public required byte[] Iv { get; set; } // The derived IV for encryption/decryption
        public BigInteger P { get; set; } // Diffie-Hellman prime
        public BigInteger G { get; set; } // Diffie-Hellman generator
    }
}