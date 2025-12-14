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
        public BigInteger SharedSecret { get; set; }
        public BigInteger P { get; set; }
        public BigInteger G { get; set; }
    }
}