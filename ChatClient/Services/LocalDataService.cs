using ChatClient.Data;
using Microsoft.EntityFrameworkCore;
using System; // Added for DateTime
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChatClient.Models; // For User model
using ChatClient.Shared.Models; // For Contact, Message, Chat, File models

namespace ChatClient.Services
{
    public class LocalDataService : ILocalDataService
    {
        private readonly ApplicationDbContext _context;

        public LocalDataService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task InitializeAsync()
        {
            await _context.Database.EnsureCreatedAsync();
        }

        public async Task SaveUserAsync(User user)
        {
            var existingUser = await _context.Users.FindAsync(user.Id);
            if (existingUser == null)
            {
                _context.Users.Add(user);
            }
            else
            {
                _context.Entry(existingUser).CurrentValues.SetValues(user);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        public async Task<User?> GetUserByLoginAsync(string login)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Login == login);
        }

        public async Task AddContactAsync(Contact contact)
        {
            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Contact>> GetContactsForUserAsync(int userId)
        {
            return await _context.Contacts.Where(c => c.UserId == userId).ToListAsync();
        }

        public async Task RemoveContactAsync(int userId, int contactUserId)
        {
            var contact = await _context.Contacts.FindAsync(userId, contactUserId);
            if (contact != null)
            {
                _context.Contacts.Remove(contact);
                await _context.SaveChangesAsync();
            }
        }

        public async Task AddMessageAsync(Message message)
        {
            // Ensure timestamp is set if not already (or set it in the ViewModel/calling code)
            if (message.Timestamp == default)
            {
                message.Timestamp = DateTime.UtcNow;
            }

            _context.Messages.Add(message);

            // If message has files, ensure they are also added and linked
            if (message.Files != null && message.Files.Any())
            {
                foreach (var file in message.Files)
                {
                    file.MessageId = message.Id; // Link file to message
                    _context.Files.Add(file);
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task<List<Message>> GetChatHistoryAsync(int chatId)
        {
            return await _context.Messages
                                 .Where(m => m.ChatId == chatId)
                                 .Include(m => m.Files) // Eager load associated files
                                 .OrderBy(m => m.Timestamp) // Order by timestamp for history
                                 .ToListAsync();
        }

        public async Task SaveChatAsync(Chat chat)
        {
            var existingChat = await _context.Chats.FindAsync(chat.Id);
            if (existingChat == null)
            {
                _context.Chats.Add(chat);
            }
            else
            {
                _context.Entry(existingChat).CurrentValues.SetValues(chat);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<Chat?> GetChatAsync(int chatId)
        {
            return await _context.Chats.FindAsync(chatId);
        }

        public async Task SaveFilesAsync(IEnumerable<File> files)
        {
            _context.Files.AddRange(files);
            await _context.SaveChangesAsync();
        }
    }
}
