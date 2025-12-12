using ChatServer.Models;
using System.Numerics;

namespace ChatServer.Services
{
    public interface ISessionKeyService
    {
        void StoreSharedSecret(int chatId, int userId, BigInteger sharedSecret, BigInteger p, BigInteger g);
        SessionKey? GetSessionKey(int chatId, int userId);
    }
}
