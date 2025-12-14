using System.Numerics;
using ChatServer.Models; // Explicitly use ChatServer.Models

namespace ChatServer.Services
{
    public interface ISessionKeyService
    {
        void StoreSharedSecret(int chatId, int userId, BigInteger sharedSecret, BigInteger p, BigInteger g);
        SessionKey? GetSessionKey(int chatId, int userId);
    }
}
