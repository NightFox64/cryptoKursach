using ChatServer.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks; // Added for Task
using ChatServer.Models; // Explicitly use ChatServer.Models
using Microsoft.AspNetCore.Authorization; // Added for [Authorize]

namespace ChatServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize] // Added
    public class ChatsController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatsController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateChat([FromBody] ChatClient.Shared.DTO.CreateChatDto dto)
        {
            try
            {
                Console.WriteLine($"ChatsController: Received CreateChat for name: {dto.Name}, userId: {dto.UserId}, otherUserId: {dto.OtherUserId}");
                var chat = await _chatService.CreateChat(
                    dto.Name ?? "New Chat", 
                    dto.UserId, 
                    dto.OtherUserId, 
                    dto.CipherAlgorithm, 
                    dto.CipherMode, 
                    dto.PaddingMode
                );
                Console.WriteLine($"ChatsController: Created chat with ID: {chat.Id}");
                return Ok(chat);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChatsController: Error in CreateChat: {ex.Message}");
                return BadRequest(new { message = $"Failed to create chat: {ex.Message}" });
            }
        }

        [HttpPost("close")]
        public async Task<IActionResult> CloseChat(int chatId) // Made async
        {
            try
            {
                Console.WriteLine($"ChatsController: Received CloseChat for chatId: {chatId}");
                await _chatService.CloseChat(chatId); // Await call
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChatsController: Error in CloseChat: {ex.Message}");
                return BadRequest(new { message = $"Failed to close chat: {ex.Message}" });
            }
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinChat(int chatId, int userId) // Made async
        {
            try
            {
                Console.WriteLine($"ChatsController: Received JoinChat for chatId: {chatId}, userId: {userId}");
                await _chatService.JoinChat(chatId, userId); // Await call
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChatsController: Error in JoinChat: {ex.Message}");
                return BadRequest(new { message = $"Failed to join chat: {ex.Message}" });
            }
        }

        [HttpPost("leave")]
        public async Task<IActionResult> LeaveChat(int chatId, int userId) // Made async
        {
            try
            {
                Console.WriteLine($"ChatsController: Received LeaveChat for chatId: {chatId}, userId: {userId}");
                await _chatService.LeaveChat(chatId, userId); // Await call
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChatsController: Error in LeaveChat: {ex.Message}");
                return BadRequest(new { message = $"Failed to leave chat: {ex.Message}" });
            }
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetChats(int userId) // Made async
        {
            try
            {
                Console.WriteLine($"ChatsController: Received GetChats for userId: {userId}");
                var chats = await _chatService.GetChats(userId); // Await call
                Console.WriteLine($"ChatsController: Returning {chats.Count} chats for userId: {userId}");
                return Ok(chats);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChatsController: Error in GetChats: {ex.Message}");
                return BadRequest(new { message = $"Failed to load chats: {ex.Message}" });
            }
        }

        [HttpPost("getOrCreate")]
        public async Task<IActionResult> GetOrCreateChat(int userId1, int userId2)
        {
            try
            {
                Console.WriteLine($"ChatsController: Received GetOrCreateChat for userId1: {userId1}, userId2: {userId2}");
                var chat = await _chatService.GetOrCreateChat(userId1, userId2);
                Console.WriteLine($"ChatsController: Returning chat with ID: {chat.Id}");
                return Ok(chat);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChatsController: Error in GetOrCreateChat: {ex.Message}");
                return BadRequest(new { message = $"Failed to get or create chat: {ex.Message}" });
            }
        }
    }
}
