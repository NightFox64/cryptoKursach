using ChatServer.Services;
using Microsoft.AspNetCore.Mvc;
using System;

namespace ChatServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ContactsController : ControllerBase
    {
        private readonly IContactService _contactService;

        public ContactsController(IContactService contactService)
        {
            _contactService = contactService;
        }

        [HttpPost("request")]
        public IActionResult SendContactRequest(int userId, int contactId)
        {
            try
            {
                _contactService.SendContactRequest(userId, contactId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("accept")]
        public IActionResult AcceptContactRequest(int userId, int contactId)
        {
            try
            {
                _contactService.AcceptContactRequest(userId, contactId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("decline")]
        public IActionResult DeclineContactRequest(int userId, int contactId)
        {
            try
            {
                _contactService.DeclineContactRequest(userId, contactId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("remove")]
        public IActionResult RemoveContact(int userId, int contactId)
        {
            try
            {
                _contactService.RemoveContact(userId, contactId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
