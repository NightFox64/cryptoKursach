using System.Collections.Generic;
using System.Threading.Tasks;
using ChatClient.Models; // For User model
using ChatClient.Shared.Models; // For Contact, Message, Chat, File models

namespace ChatClient.Services
{
    public interface ILocalDataService
    {
        Task InitializeAsync();
        Task SaveUserAsync(User user);
        Task<User?> GetUserByIdAsync(int userId);
        Task<User?> GetUserByLoginAsync(string login);
        Task AddContactAsync(Contact contact);
        Task<List<Contact>> GetContactsForUserAsync(int userId);
        Task RemoveContactAsync(int userId, int contactUserId);
        Task AddMessageAsync(Message message);
        Task<List<Message>> GetChatHistoryAsync(int chatId);

        // New methods for Chat
        Task SaveChatAsync(Chat chat);
        Task<Chat?> GetChatAsync(int chatId);

        // New methods for Files
        Task SaveFilesAsync(IEnumerable<File> files);
        
        // Cleanup method
        Task CleanupDuplicateMessagesAsync();
    }
}
