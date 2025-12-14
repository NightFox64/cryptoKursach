using System.Collections.Generic;
using System.Threading.Tasks; // Added for Task
using ChatServer.Models;

namespace ChatServer.Services
{
    public interface IUserService
    {
        Task<User?> GetById(int id);
        Task<User?> GetByLogin(string login);
        Task<User> Create(User user, string? password);
        Task<User?> Authenticate(string? login, string? password);
        Task Update(User user);
        string GenerateJwtToken(User user); // This can remain synchronous
    }
}
