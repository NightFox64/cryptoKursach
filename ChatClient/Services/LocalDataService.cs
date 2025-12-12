using ChatClient.Data;
using ChatClient.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Message>> GetChatHistoryAsync(int chatId)
        {
            return await _context.Messages.Where(m => m.ChatId == chatId).OrderBy(m => m.Id).ToListAsync();
        }
    }
}
