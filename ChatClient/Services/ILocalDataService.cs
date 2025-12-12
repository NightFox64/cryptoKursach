using ChatClient.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

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
    }
}
