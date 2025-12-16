using System.Collections.Generic;
using ChatServer.Models; // Explicitly use ChatServer.Models

namespace ChatServer.Services
{
    public interface IChatService
    {
        Task<Chat> CreateChat(string name, int initialUserId, int otherUserId, string? cipherAlgorithm, string? cipherMode, string? paddingMode);
        Task CloseChat(int chatId);
        Task JoinChat(int chatId, int userId);
        Task LeaveChat(int chatId, int userId);
        Task<List<Chat>> GetChats(int userId);
        Task<Chat> GetOrCreateChat(int userId1, int userId2);
    }
}
