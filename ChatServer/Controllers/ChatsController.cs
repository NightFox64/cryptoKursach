using ChatServer.Services;
using Microsoft.AspNetCore.Mvc;
using System;

namespace ChatServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatsController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatsController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("create")]
        public IActionResult CreateChat(string name, int userId)
        {
            try
            {
                var chat = _chatService.CreateChat(name, userId);
                return Ok(chat);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("close")]
        public IActionResult CloseChat(int chatId)
        {
            try
            {
                _chatService.CloseChat(chatId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("join")]
        public IActionResult JoinChat(int chatId, int userId)
        {
            try
            {
                _chatService.JoinChat(chatId, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("leave")]
        public IActionResult LeaveChat(int chatId, int userId)
        {
            try
            {
                _chatService.LeaveChat(chatId, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
