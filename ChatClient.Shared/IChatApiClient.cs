using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using ChatClient.Shared.Models.DTO;
using ChatClient.Shared.Models;

namespace ChatClient.Shared
{
    public interface IChatApiClient
    {
        Task<bool> Register(string login, string password);
        Task<int?> Login(string login, string password);
        Task<bool> SendContactRequest(int userId, string contactLogin);
        Task<bool> AcceptContactRequest(int userId, int contactId);
        Task<bool> DeclineContactRequest(int userId, int contactId);
        Task<bool> RemoveContact(int userId, int contactId);
        Task<Chat?> CreateChat(string name, int initialUserId, int otherUserId, string? cipherAlgorithm, string? cipherMode, string? paddingMode);
        Task<bool> CloseChat(int chatId);
        Task<bool> JoinChat(int chatId, int userId);
        Task<bool> LeaveChat(int chatId, int userId);
        Task<(BigInteger serverPublicKey, BigInteger p, BigInteger g, byte[]? encryptedKey, byte[]? encryptedIv)?> RequestSessionKey(int chatId, int userId, BigInteger clientPublicKey);
        Task<bool> SendEncryptedFragment(int chatId, int senderId, string encryptedContent);
        Task<Message?> ReceiveEncryptedFragment(int chatId, long lastDeliveryId);
        Task<List<ContactDto>> GetContacts(int userId);
        Task<List<Chat>> GetChats(int userId);
        Task<int?> GetOrCreateChat(int userId1, int userId2);
        Task<List<Message>> GetChatHistory(int chatId); // Added for fetching chat history
        void ClearAuthToken(); // Clear authentication token on logout
    }
}
