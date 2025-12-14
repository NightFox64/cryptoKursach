using System.Numerics;
using System.Threading.Tasks; // Added for async methods
using ChatServer.Models;

namespace ChatServer.Services
{
    public interface ISessionKeyService
    {
        // Initiates the Diffie-Hellman key exchange on the server side
        Task<(BigInteger serverPublicKey, BigInteger p, BigInteger g)?> InitiateKeyExchange(int chatId, int userId, BigInteger clientPublicKey);

        // Retrieves the stored session key information for a given chat and user
        Task<SessionKey?> GetSessionKey(int chatId, int userId);
    }
}
