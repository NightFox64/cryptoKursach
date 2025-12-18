using ChatServer.Services;
using DiffieHellman;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Numerics;
using System.Threading.Tasks;
using ChatClient.Shared.Models.DTO;
using Microsoft.Extensions.Logging; // Added for ILogger

namespace ChatServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class KeysController : ControllerBase
    {
        private readonly ISessionKeyService _sessionKeyService;
        private readonly ILogger<KeysController> _logger; // Added ILogger

        public KeysController(ISessionKeyService sessionKeyService, ILogger<KeysController> logger) // Injected ILogger
        {
            _sessionKeyService = sessionKeyService;
            _logger = logger; // Assigned ILogger
        }

        [HttpPost("requestSessionKey")]
        public async Task<IActionResult> RequestSessionKey(SessionKeyRequestDto model)
        {
            _logger.LogInformation("Received RequestSessionKey: ChatId={ChatId}, UserId={UserId}, PublicKey={PublicKey}", model.ChatId, model.UserId, model.PublicKey); // Log incoming request

            if (model == null)
            {
                _logger.LogError("Model is null in RequestSessionKey.");
                return BadRequest(new { message = "Request payload is null." });
            }

            try
            {
                if (string.IsNullOrEmpty(model.PublicKey))
                {
                    _logger.LogError("Public key is null or empty for ChatId={ChatId}, UserId={UserId}", model.ChatId, model.UserId);
                    return BadRequest(new { message = "Public key cannot be null or empty." });
                }

                BigInteger clientPublicKey = BigInteger.Parse(model.PublicKey);

                // Delegate key exchange initiation to SessionKeyService
                var result = await _sessionKeyService.InitiateKeyExchange(model.ChatId, model.UserId, clientPublicKey);

                if (result.HasValue)
                {
                    var (serverPublicKey, p, g, encryptedChatKey, encryptedChatIv) = result.Value;
                    return Ok(new 
                    { 
                        serverPublicKey = serverPublicKey.ToString(), 
                        p = p.ToString(), 
                        g = g.ToString(),
                        encryptedKey = Convert.ToBase64String(encryptedChatKey),
                        encryptedIv = Convert.ToBase64String(encryptedChatIv)
                    });
                }
                else
                {
                    _logger.LogError("SessionKeyService.InitiateKeyExchange returned null for ChatId={ChatId}, UserId={UserId}", model.ChatId, model.UserId);
                    return BadRequest(new { message = "Failed to initiate key exchange." });
                }
            }
            catch (FormatException fex)
            {
                _logger.LogError(fex, "PublicKey parsing failed for ChatId={ChatId}, UserId={UserId}, PublicKey={PublicKey}", model.ChatId, model.UserId, model.PublicKey);
                return BadRequest(new { message = $"Invalid PublicKey format: {fex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RequestSessionKey for ChatId={ChatId}, UserId={UserId}", model.ChatId, model.UserId);
                return BadRequest(new { message = $"Error in RequestSessionKey: {ex.Message}", details = ex.InnerException?.Message ?? ex.ToString() });
            }
        }
    }
}
