using System.Collections.Generic;
using ChatClient.Shared.Models;

namespace ChatServer.Services
{
    public interface IMessageBrokerService
    {
        void CreateQueue(int chatId);
        void DeleteQueue(int chatId);
        void SendMessage(Message message);
        Message? ReceiveMessage(int chatId, long lastDeliveryId);
    }
}
