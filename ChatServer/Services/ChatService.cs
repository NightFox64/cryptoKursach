using ChatServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChatServer.Services
{
    public class ChatService : IChatService
    {
        private List<Chat> _chats = new List<Chat>();
        private int _nextId = 1;

        public Chat CreateChat(string name, int initialUserId)
        {
            var chat = new Chat
            {
                Id = _nextId++,
                Name = name
            };
            chat.UserIds.Add(initialUserId);
            _chats.Add(chat);
            return chat;
        }

        public void CloseChat(int chatId)
        {
            var chat = _chats.FirstOrDefault(c => c.Id == chatId);
            if (chat != null)
            {
                _chats.Remove(chat);
            }
        }

        public void JoinChat(int chatId, int userId)
        {
            var chat = _chats.FirstOrDefault(c => c.Id == chatId);
            if (chat == null)
            {
                throw new Exception("Chat not found");
            }

            if (!chat.UserIds.Contains(userId))
            {
                chat.UserIds.Add(userId);
            }
        }

        public void LeaveChat(int chatId, int userId)
        {
            var chat = _chats.FirstOrDefault(c => c.Id == chatId);
            if (chat == null)
            {
                throw new Exception("Chat not found");
            }

            if (chat.UserIds.Contains(userId))
            {
                chat.UserIds.Remove(userId);
            }
        }
    }
}
