using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChatServer.Data;
using ChatServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Services
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;

        public ChatService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Chat> CreateChat(string name, int initialUserId)
        {
            var chat = new Chat
            {
                Name = name,
                UserIds = new List<int> { initialUserId }
            };
            _context.Chats.Add(chat);
            await _context.SaveChangesAsync();
            return chat;
        }

        public async Task CloseChat(int chatId)
        {
            var chat = await _context.Chats.FirstOrDefaultAsync(c => c.Id == chatId);
            if (chat != null)
            {
                _context.Chats.Remove(chat);
                await _context.SaveChangesAsync();
            }
        }

        public async Task JoinChat(int chatId, int userId)
        {
            var chat = await _context.Chats.FirstOrDefaultAsync(c => c.Id == chatId);
            if (chat == null)
            {
                throw new Exception("Chat not found");
            }

            if (!chat.UserIds.Contains(userId))
            {
                chat.UserIds.Add(userId);
                _context.Chats.Update(chat);
                await _context.SaveChangesAsync();
            }
        }

        public async Task LeaveChat(int chatId, int userId)
        {
            var chat = await _context.Chats.FirstOrDefaultAsync(c => c.Id == chatId);
            if (chat == null)
            {
                throw new Exception("Chat not found");
            }

            if (chat.UserIds.Contains(userId))
            {
                chat.UserIds.Remove(userId);
                _context.Chats.Update(chat);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Chat>> GetChats(int userId)
        {
            return (await _context.Chats.ToListAsync())
                   .Where(c => c.UserIds.Contains(userId))
                   .ToList();
        }

        public async Task<Chat> GetOrCreateChat(int userId1, int userId2)
        {
            // Try to find an existing chat between these two users
            var existingChat = (await _context.Chats.ToListAsync())
                .FirstOrDefault(c => c.UserIds.Contains(userId1) && c.UserIds.Contains(userId2) && c.UserIds.Count == 2);

            if (existingChat != null)
            {
                return existingChat;
            }

            // If no existing chat, create a new one
            var newChat = new Chat
            {
                Name = $"Chat with {userId1} and {userId2}", // A more descriptive name can be generated
                UserIds = new List<int> { userId1, userId2 }
            };

            _context.Chats.Add(newChat);
            await _context.SaveChangesAsync();
            return newChat;
        }
    }
}