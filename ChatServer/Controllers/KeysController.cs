using ChatServer.Services;
using DiffieHellman;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Numerics;
using System.Threading.Tasks; // Added
using ChatClient.Shared.Models.DTO;

namespace ChatServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class KeysController : ControllerBase
    {
        private readonly ISessionKeyService _sessionKeyService;

        public KeysController(ISessionKeyService sessionKeyService)
        {
            _sessionKeyService = sessionKeyService;
        }

        [HttpPost("requestSessionKey")]
        public async Task<IActionResult> RequestSessionKey(SessionKeyRequestDto model)
        {
            try
            {
                BigInteger clientPublicKey = BigInteger.Parse(model.PublicKey ?? throw new InvalidOperationException("Public key cannot be null."));

                // Delegate key exchange initiation to SessionKeyService
                var result = await _sessionKeyService.InitiateKeyExchange(model.ChatId, model.UserId, clientPublicKey);

                if (result.HasValue)
                {
                    var (serverPublicKey, p, g) = result.Value;
                    return Ok(new { serverPublicKey = serverPublicKey.ToString(), p = p.ToString(), g = g.ToString() });
                }
                else
                {
                    return BadRequest(new { message = "Failed to initiate key exchange." });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
