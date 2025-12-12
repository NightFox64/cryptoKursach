using ChatServer.Models;
using ChatServer.Models.DTO;
using ChatServer.Services;
using Microsoft.AspNetCore.Mvc;

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

        [HttpPost("register")]
        public IActionResult Register(RegisterDto model)
        {
            try
            {
                var user = new User { Login = model.Login };
                _userService.Create(user, model.Password);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public IActionResult Login(LoginDto model)
        {
            var user = _userService.Authenticate(model.Login, model.Password);
            if (user != null)
            {
                return Ok(new { UserId = user.Id });
            }

            return Unauthorized();
        }
    }
}
