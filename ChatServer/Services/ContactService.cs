using ChatServer.Models;
using System;
using System.Linq;

namespace ChatServer.Services
{
    public class ContactService : IContactService
    {
        private readonly IUserService _userService;

        public ContactService(IUserService userService)
        {
            _userService = userService;
        }

        public void AcceptContactRequest(int userId, int contactId)
        {
            var user = _userService.GetById(userId);
            var contactUser = _userService.GetById(contactId);

            if (user == null || contactUser == null)
            {
                throw new Exception("User not found");
            }

            var contactRequest = user.Contacts.FirstOrDefault(c => c.ContactId == contactId && c.Status == ContactRequestStatus.Pending);
            if (contactRequest == null)
            {
                throw new Exception("Contact request not found");
            }

            contactRequest.Status = ContactRequestStatus.Accepted;

            // Add the contact to the other user as well
            if (!contactUser.Contacts.Any(c => c.ContactId == userId))
            {
                contactUser.Contacts.Add(new Contact
                {
                    UserId = contactId,
                    ContactId = userId,
                    Status = ContactRequestStatus.Accepted
                });
            }
        }

        public void DeclineContactRequest(int userId, int contactId)
        {
            var user = _userService.GetById(userId);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            var contactRequest = user.Contacts.FirstOrDefault(c => c.ContactId == contactId && c.Status == ContactRequestStatus.Pending);
            if (contactRequest == null)
            {
                throw new Exception("Contact request not found");
            }

            user.Contacts.Remove(contactRequest);
        }

        public void RemoveContact(int userId, int contactId)
        {
            var user = _userService.GetById(userId);
            var contactUser = _userService.GetById(contactId);

            if (user == null || contactUser == null)
            {
                throw new Exception("User not found");
            }

            var userContact = user.Contacts.FirstOrDefault(c => c.ContactId == contactId);
            var contactUserContact = contactUser.Contacts.FirstOrDefault(c => c.ContactId == userId);

            if (userContact != null)
            {
                user.Contacts.Remove(userContact);
            }

            if (contactUserContact != null)
            {
                contactUser.Contacts.Remove(contactUserContact);
            }
        }

        public void SendContactRequest(int userId, int contactId)
        {
            var user = _userService.GetById(userId);
            var contactUser = _userService.GetById(contactId);

            if (user == null || contactUser == null)
            {
                throw new Exception("User not found");
            }

            if (contactUser.Contacts.Any(c => c.ContactId == userId))
            {
                throw new Exception("Contact request already sent or contact already exists");
            }

            contactUser.Contacts.Add(new Contact
            {
                UserId = contactId,
                ContactId = userId,
                Status = ContactRequestStatus.Pending
            });
        }
    }
}
