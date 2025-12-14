using System.Collections.Generic;
using System.Threading.Tasks; // Added for Task
using ChatClient.Shared.Models.DTO;
using ChatClient.Shared.Models;

namespace ChatServer.Services
{
    public interface IContactService
    {
        Task SendContactRequest(int userId, string contactLogin);
        Task AcceptContactRequest(int userId, int contactId);
        Task DeclineContactRequest(int userId, int contactId);
        Task RemoveContact(int userId, int contactId);
        Task<List<ContactDto>> GetContacts(int userId);
    }
}
