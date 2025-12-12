using ChatServer.Models;

namespace ChatServer.Services
{
    public interface IContactService
    {
        void SendContactRequest(int userId, int contactId);
        void AcceptContactRequest(int userId, int contactId);
        void DeclineContactRequest(int userId, int contactId);
        void RemoveContact(int userId, int contactId);
    }
}
