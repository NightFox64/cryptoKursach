using ChatServer.Models;

namespace ChatServer.Services
{
    public interface IChatService
    {
        Chat CreateChat(string name, int initialUserId);
        void CloseChat(int chatId);
        void JoinChat(int chatId, int userId);
        void LeaveChat(int chatId, int userId);
    }
}
