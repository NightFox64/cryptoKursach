using ChatServer.Models;
using System.Collections.Generic;

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
