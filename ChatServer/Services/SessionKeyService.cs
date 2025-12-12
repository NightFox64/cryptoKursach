using ChatServer.Models;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ChatServer.Services
{
    public class SessionKeyService : ISessionKeyService
    {
        private List<SessionKey> _sessionKeys = new List<SessionKey>();

        public SessionKey? GetSessionKey(int chatId, int userId)
        {
            return _sessionKeys.FirstOrDefault(sk => sk.ChatId == chatId && sk.UserId == userId);
        }

        public void StoreSharedSecret(int chatId, int userId, BigInteger sharedSecret, BigInteger p, BigInteger g)
        {
            var sessionKey = _sessionKeys.FirstOrDefault(sk => sk.ChatId == chatId && sk.UserId == userId);
            if (sessionKey != null)
            {
                sessionKey.SharedSecret = sharedSecret;
                sessionKey.P = p;
                sessionKey.G = g;
            }
            else
            {
                _sessionKeys.Add(new SessionKey
                {
                    ChatId = chatId,
                    UserId = userId,
                    SharedSecret = sharedSecret,
                    P = p,
                    G = g
                });
            }
        }
    }
}
