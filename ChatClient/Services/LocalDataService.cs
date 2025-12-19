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
            // Check if message already exists (by DeliveryId and ChatId to avoid duplicates)
            if (message.DeliveryId > 0)
            {
                var existingMessage = await _context.Messages
                    .FirstOrDefaultAsync(m => m.ChatId == message.ChatId && m.DeliveryId == message.DeliveryId);
                
                if (existingMessage != null)
                {
                    // Message already exists in DB, skip insertion
                    return;
                }
            }
            
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
                                 .AsNoTracking() // Don't track these entities - they are read-only for display
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

        /// <summary>
        /// Remove duplicate messages from the database
        /// This is a one-time cleanup method to fix existing duplicates
        /// </summary>
        public async Task CleanupDuplicateMessagesAsync()
        {
            try
            {
                static void Log(string message) => FileLogger.Log(message);
                
                Log("[LocalDataService] Starting duplicate message cleanup...");
                
                // Find all messages with DeliveryId > 0
                var allMessages = await _context.Messages
                    .Where(m => m.DeliveryId > 0)
                    .OrderBy(m => m.Id) // Order by Id to keep the oldest
                    .ToListAsync();

                Log($"[LocalDataService] Found {allMessages.Count} messages with DeliveryId > 0");

                // Group by ChatId and DeliveryId to find duplicates
                var duplicateGroups = allMessages
                    .GroupBy(m => new { m.ChatId, m.DeliveryId })
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (duplicateGroups.Any())
                {
                    int totalRemoved = 0;
                    Log($"[LocalDataService] Found {duplicateGroups.Count} groups of duplicate messages");
                    
                    foreach (var group in duplicateGroups)
                    {
                        var messages = group.ToList();
                        Log($"[LocalDataService] ChatId={group.Key.ChatId}, DeliveryId={group.Key.DeliveryId}: {messages.Count} copies (keeping Id={messages[0].Id})");
                        
                        // Keep the first (oldest) message, remove the rest
                        for (int i = 1; i < messages.Count; i++)
                        {
                            _context.Messages.Remove(messages[i]);
                            totalRemoved++;
                        }
                    }

                    await _context.SaveChangesAsync();
                    Log($"[LocalDataService] ✓ Cleaned up {totalRemoved} duplicate messages from {duplicateGroups.Count} groups");
                }
                else
                {
                    Log("[LocalDataService] ✓ No duplicate messages found - database is clean");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[LocalDataService] ✗ Error cleaning up duplicates: {ex.Message}");
                FileLogger.Log($"[LocalDataService] Stack trace: {ex.StackTrace}");
            }
        }
    }
}
