using ChatServer.Models; // Changed from ChatClient.Shared.Models
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatServer.Services
{
    public interface IMessageBrokerService
    {
        Task SendMessage(Message message);
        Task<Message?> ReceiveMessage(int chatId, long lastDeliveryId);
        Task<List<Message>> GetChatHistory(int chatId);
    }
}
