using System.Numerics;

namespace ChatServer.Models
{
    public class SessionKey
    {
        public int ChatId { get; set; }
        public int UserId { get; set; }
        public BigInteger SharedSecret { get; set; }
        public BigInteger P { get; set; }
        public BigInteger G { get; set; }
    }
}
