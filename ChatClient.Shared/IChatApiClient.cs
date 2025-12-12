using ChatServer.Models;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace ChatClient.Shared
{
    public interface IChatApiClient
    {
        Task<bool> Register(string login, string password);
        Task<int?> Login(string login, string password);
        Task<bool> SendContactRequest(int userId, int contactId);
        Task<bool> AcceptContactRequest(int userId, int contactId);
        Task<bool> DeclineContactRequest(int userId, int contactId);
        Task<bool> RemoveContact(int userId, int contactId);
        Task<int?> CreateChat(string name, int initialUserId);
        Task<bool> CloseChat(int chatId);
        Task<bool> JoinChat(int chatId, int userId);
        Task<bool> LeaveChat(int chatId, int userId);
        Task<(BigInteger serverPublicKey, BigInteger p, BigInteger g)?> RequestSessionKey(int chatId, int userId, BigInteger clientPublicKey);
        Task<bool> SendEncryptedFragment(int chatId, int senderId, string encryptedContent);
        Task<ChatServer.Models.Message?> ReceiveEncryptedFragment(int chatId, long lastDeliveryId);
    }
}
