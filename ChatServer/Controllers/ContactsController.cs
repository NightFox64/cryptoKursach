using ChatServer.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks; // Added for Task
using ChatClient.Shared.Models;
using Microsoft.AspNetCore.Authorization; // Added for [Authorize]

namespace ChatServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize] // Added
    public class ContactsController : ControllerBase
    {
        private readonly IContactService _contactService;

        public ContactsController(IContactService contactService)
        {
            _contactService = contactService;
        }

        [HttpPost("request")]
        public async Task<IActionResult> SendContactRequest(int userId, [FromQuery]string contactLogin)
        {
            try
            {
                await _contactService.SendContactRequest(userId, contactLogin);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("accept")]
        public async Task<IActionResult> AcceptContactRequest(int userId, int contactId) // Made async
        {
            try
            {
                await _contactService.AcceptContactRequest(userId, contactId); // Await call
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("decline")]
        public async Task<IActionResult> DeclineContactRequest(int userId, int contactId) // Made async
        {
            try
            {
                await _contactService.DeclineContactRequest(userId, contactId); // Await call
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("remove")]
        public async Task<IActionResult> RemoveContact(int userId, int contactId) // Made async
        {
            try
            {
                await _contactService.RemoveContact(userId, contactId); // Await call
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetContacts(int userId) // Made async
        {
            try
            {
                var contacts = await _contactService.GetContacts(userId); // Await call
                return Ok(contacts);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
