using ChatServer.Models; // Changed from ChatClient.Shared.Models
using ChatServer.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ChatServer.Services
{
    public class MessageBrokerService : IMessageBrokerService
    {
        private readonly ApplicationDbContext _context;

        public MessageBrokerService(ApplicationDbContext context)
        {
            _context = context;
        }



        public async Task<Message?> ReceiveMessage(int chatId, long lastDeliveryId)
        {
            var message = await _context.Messages
                                        .Where(m => m.ChatId == chatId && m.Id > lastDeliveryId)
                                        .OrderBy(m => m.Id)
                                        .FirstOrDefaultAsync();
            return message;
        }

        public async Task<List<Message>> GetChatHistory(int chatId)
        {
            return await _context.Messages
                                 .Where(m => m.ChatId == chatId)
                                 .OrderBy(m => m.Id)
                                 .ToListAsync();
        }

        public async Task SendMessage(Message message)
        {
            // Message ID will be set by the database
            message.Timestamp = DateTime.UtcNow;
            
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // After saving, the database will assign an Id. Use this as DeliveryId.
            message.DeliveryId = message.Id;
        }
    }
}
