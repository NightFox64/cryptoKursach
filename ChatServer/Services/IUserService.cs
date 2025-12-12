using ChatServer.Models;
using System.Collections.Generic;

namespace ChatServer.Services
{
    public interface IUserService
    {
        User? GetById(int id);
        User? GetByLogin(string login);
        User Create(User user, string? password);
        bool Authenticate(string? login, string? password);
    }
}
