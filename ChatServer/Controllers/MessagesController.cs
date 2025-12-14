using ChatServer.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using ChatClient.Shared.Models;

namespace ChatServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageBrokerService _messageBrokerService;

        public MessagesController(IMessageBrokerService messageBrokerService)
        {
            _messageBrokerService = messageBrokerService;
        }

        [HttpPost("send")]
        public IActionResult SendMessage(Message message)
        {
            try
            {
                _messageBrokerService.SendMessage(message);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("receive")]
        public IActionResult ReceiveMessage(int chatId, long lastDeliveryId)
        {
            try
            {
                var message = _messageBrokerService.ReceiveMessage(chatId, lastDeliveryId);
                if (message != null)
                {
                    return Ok(message);
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
