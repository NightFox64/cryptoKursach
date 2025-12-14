using ChatServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization; // Added for [AllowAnonymous]
using ChatServer.Models;
using ChatClient.Shared.Models.DTO;
using System;
using System.Threading.Tasks; // Added for Task

namespace ChatServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [AllowAnonymous] // Added
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto model) // Made async
        {
            try
            {
                var user = new User { Login = model.Login };
                await _userService.Create(user, model.Password); // Await call
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous] // Added
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto model) // Made async
        {
            var user = await _userService.Authenticate(model.Login, model.Password); // Await call
            if (user != null)
            {
                var token = _userService.GenerateJwtToken(user); // Generate JWT
                return Ok(new LoginResponseDto { UserId = user.Id, AuthToken = token }); // Return LoginResponseDto
            }

            return Unauthorized();
        }
    }
}