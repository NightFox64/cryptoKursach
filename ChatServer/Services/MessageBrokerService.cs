using System;
using System.Collections.Generic;
using System.Linq;
using ChatClient.Shared.Models;

namespace ChatServer.Services
{
    public class MessageBrokerService : IMessageBrokerService
    {
        private Dictionary<int, Queue<Message>> _queues = new Dictionary<int, Queue<Message>>();
        private long _nextDeliveryId = 1;

        public void CreateQueue(int chatId)
        {
            if (!_queues.ContainsKey(chatId))
            {
                _queues[chatId] = new Queue<Message>();
            }
        }

        public void DeleteQueue(int chatId)
        {
            if (_queues.ContainsKey(chatId))
            {
                _queues.Remove(chatId);
            }
        }

        public Message? ReceiveMessage(int chatId, long lastDeliveryId)
        {
            if (_queues.ContainsKey(chatId))
            {
                var queue = _queues[chatId];
                if (queue.Any())
                {
                    var message = queue.Peek();
                    if (message.DeliveryId > lastDeliveryId)
                    {
                        return message;
                    }
                }
            }
            return null;
        }

        public void SendMessage(Message message)
        {
            if (_queues.ContainsKey(message.ChatId))
            {
                message.DeliveryId = _nextDeliveryId++;
                _queues[message.ChatId].Enqueue(message);
            }
        }
    }
}
