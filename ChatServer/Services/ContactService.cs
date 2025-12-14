using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Added for Task
using ChatServer.Models;
using ChatClient.Shared.Models;
using ChatClient.Shared.Models.DTO;
using Microsoft.EntityFrameworkCore; // Added for Include
using ChatServer.Data; // Added for ApplicationDbContext

namespace ChatServer.Services
{
    public class ContactService : IContactService
    {
        private readonly IUserService _userService;
        private readonly ApplicationDbContext _context; // Inject ApplicationDbContext

        public ContactService(IUserService userService, ApplicationDbContext context) // Inject ApplicationDbContext
        {
            _userService = userService;
            _context = context; // Initialize ApplicationDbContext
        }

        public async Task AcceptContactRequest(int userId, int contactId)
        {
            var user = await _userService.GetById(userId);
            var contactUser = await _userService.GetById(contactId);

            if (user == null || contactUser == null)
            {
                throw new Exception("User not found");
            }

            var contactRequest = user.Contacts.FirstOrDefault(c => c.ContactId == contactId && c.Status == ChatClient.Shared.Models.ContactRequestStatus.Pending);
            if (contactRequest == null)
            {
                throw new Exception("Contact request not found");
            }

            contactRequest.Status = ChatClient.Shared.Models.ContactRequestStatus.Accepted;
            await _userService.Update(user); // Save changes to user's contacts

            // Add the contact to the other user as well
            if (!contactUser.Contacts.Any(c => c.ContactId == userId))
            {
                contactUser.Contacts.Add(new ChatServer.Models.Contact
                {
                    UserId = contactId,
                    ContactId = userId,
                    Status = ChatClient.Shared.Models.ContactRequestStatus.Accepted
                });
                await _userService.Update(contactUser); // Save changes to contactUser's contacts
            }
        }

        public async Task DeclineContactRequest(int userId, int contactId)
        {
            var user = await _userService.GetById(userId);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            var contactRequest = user.Contacts.FirstOrDefault(c => c.ContactId == contactId && c.Status == ChatClient.Shared.Models.ContactRequestStatus.Pending);
            if (contactRequest == null)
            {
                throw new Exception("Contact request not found");
            }

            user.Contacts.Remove(contactRequest);
            await _userService.Update(user); // Save changes to user's contacts
        }

        public async Task RemoveContact(int userId, int contactId)
        {
            var user = await _userService.GetById(userId);
            var contactUser = await _userService.GetById(contactId);

            if (user == null || contactUser == null)
            {
                throw new Exception("User not found");
            }

            var userContact = user.Contacts.FirstOrDefault(c => c.ContactId == contactId);
            var contactUserContact = contactUser.Contacts.FirstOrDefault(c => c.ContactId == userId);

            if (userContact != null)
            {
                user.Contacts.Remove(userContact);
                await _userService.Update(user); // Save changes to user's contacts
            }

            if (contactUserContact != null)
            {
                contactUser.Contacts.Remove(contactUserContact);
                await _userService.Update(contactUser); // Save changes to contactUser's contacts
            }
        }

        public async Task SendContactRequest(int userId, string contactLogin)
        {
            var user = await _userService.GetById(userId);
            var contactUser = await _userService.GetByLogin(contactLogin);

            if (user == null)
            {
                throw new Exception("User not found");
            }
            if (contactUser == null)
            {
                throw new Exception("Contact user not found");
            }

            if (user.Id == contactUser.Id)
            {
                throw new ArgumentException("Cannot send a contact request to yourself.");
            }

            if (user.Contacts.Any(c => c.ContactId == contactUser.Id))
            {
                throw new Exception("Contact request already sent or contact already exists");
            }

            // Add contact to the recipient
            contactUser.Contacts.Add(new ChatServer.Models.Contact
            {
                UserId = contactUser.Id,
                ContactId = user.Id, // The other user is the sender
                Status = ChatClient.Shared.Models.ContactRequestStatus.Accepted
            });
            await _userService.Update(contactUser); // Save changes to contactUser's contacts

            // Add contact to the sender
            user.Contacts.Add(new ChatServer.Models.Contact
            {
                UserId = user.Id,
                ContactId = contactUser.Id,
                Status = ChatClient.Shared.Models.ContactRequestStatus.Accepted
            });
            await _userService.Update(user); // Save changes to user's contacts
        }

        public async Task<List<ContactDto>> GetContacts(int userId)
        {
            var user = await _userService.GetById(userId);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            var contactIds = user.Contacts.Select(c => c.ContactId).ToList();

            // Fetch all contact users in a single query
            var contactUsers = await _context.Users
                                           .Where(u => contactIds.Contains(u.Id))
                                           .ToDictionaryAsync(u => u.Id, u => u.Login);

            return user.Contacts.Select(c => new ContactDto
            {
                UserId = c.UserId,
                ContactId = c.ContactId,
                Status = c.Status,
                ContactUserName = contactUsers.GetValueOrDefault(c.ContactId, "Unknown")
            }).ToList();
        }
    }
}