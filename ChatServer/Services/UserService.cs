using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ChatServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; // Added for IConfiguration
using System.IdentityModel.Tokens.Jwt; // Added for JwtSecurityTokenHandler
using Microsoft.IdentityModel.Tokens; // Added for SymmetricSecurityKey
using System.Security.Claims; // Added for Claims
using System.Threading.Tasks; // Added for Task
using ChatServer.Models;

namespace ChatServer.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration; // Injected IConfiguration

        public UserService(ApplicationDbContext context, IConfiguration configuration) // Inject DbContext and IConfiguration
        {
            _context = context;
            _configuration = configuration; // Initialize IConfiguration
        }

        public async Task<User?> Authenticate(string? login, string? password)
        {
            if (login == null || password == null)
            {
                return null;
            }

            var user = await _context.Users.Include(u => u.Contacts).FirstOrDefaultAsync(x => x.Login == login);
            if (user == null || user.Salt == null || user.PasswordHash == null)
            {
                return null;
            }

            var passwordHash = HashPassword(password, user.Salt);
            if (user.PasswordHash == passwordHash)
            {
                return user;
            }
            return null;
        }

        public async Task<User> Create(User user, string? password)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            if (await _context.Users.AnyAsync(x => x.Login == user.Login))
            {
                throw new Exception("User with login " + user.Login + " already exists");
            }

            user.Salt = GenerateSalt();
            user.PasswordHash = HashPassword(password, user.Salt);

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<User?> GetByLogin(string login)
        {
            return await _context.Users.Include(u => u.Contacts).FirstOrDefaultAsync(x => x.Login == login);
        }

        public async Task<User?> GetById(int id)
        {
            return await _context.Users.Include(u => u.Contacts).FirstOrDefaultAsync(x => x.Id == id);
        }

        public string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:Secret"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, user.Id.ToString()) }),
                Expires = DateTime.UtcNow.AddDays(7), // Token expires in 7 days
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public async Task Update(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        private byte[] GenerateSalt()
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }

        private string HashPassword(string password, byte[] salt)
        {
            using (var sha256 = SHA256.Create())
            {
                var saltedPassword = Encoding.UTF8.GetBytes(password).Concat(salt).ToArray();
                var hash = sha256.ComputeHash(saltedPassword);
                return Convert.ToBase64String(hash);
            }
        }
    }
}