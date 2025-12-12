using ChatServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ChatServer.Services
{
    public class UserService : IUserService
    {
        private List<User> _users = new List<User>();
        private int _nextId = 1;

        public bool Authenticate(string? login, string? password)
        {
            if (login == null || password == null)
            {
                return false;
            }

            var user = GetByLogin(login);
            if (user == null || user.Salt == null)
            {
                return false;
            }

            var passwordHash = HashPassword(password, user.Salt);
            return user.PasswordHash == passwordHash;
        }

        public User Create(User user, string? password)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            if (_users.Any(x => x.Login == user.Login))
            {
                throw new Exception("User with login " + user.Login + " already exists");
            }

            user.Id = _nextId++;
            user.Salt = GenerateSalt();
            user.PasswordHash = HashPassword(password, user.Salt);
            _users.Add(user);
            return user;
        }

        public User? GetByLogin(string login)
        {
            return _users.FirstOrDefault(x => x.Login == login);
        }

        public User? GetById(int id)
        {
            return _users.FirstOrDefault(x => x.Id == id);
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
