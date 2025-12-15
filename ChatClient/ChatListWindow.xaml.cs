using ChatClient.Services;
using ChatClient.Shared;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChatClient.Models; // Using client-specific models
using ChatClient.Shared.Models.DTO; // Using shared DTOs
using ChatClient.Shared.Models; // Added for shared Chat model

namespace ChatClient
{
    /// <summary>
    /// Interaction logic for ChatListWindow.xaml
    /// </summary>
    public partial class ChatListWindow : Window
    {
        private readonly IChatApiClient _chatApiClient;
        private readonly IEncryptionService _encryptionService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILocalDataService _localDataService;
        private int _currentUserId;

        public ChatListWindow(IChatApiClient chatApiClient, IEncryptionService encryptionService, IServiceProvider serviceProvider, ILocalDataService localDataService)
        {
            InitializeComponent();
            _chatApiClient = chatApiClient;
            _encryptionService = encryptionService;
            _serviceProvider = serviceProvider;
            _localDataService = localDataService;
            ContactsListBox.DisplayMemberPath = "ContactUserName";
            ChatsListBox.DisplayMemberPath = "Name";
        }

        public async void InitializeChat(int userId)
        {
            _currentUserId = userId;
            await RefreshContacts();
            await RefreshChats();
        }

        private async Task RefreshContacts()
        {
            try
            {
                var contacts = await _chatApiClient.GetContacts(_currentUserId);
                ContactsListBox.ItemsSource = contacts;
                foreach (var contact in contacts)
                {
                    await _localDataService.SaveUserAsync(new User { Id = contact.ContactId, Login = contact.ContactUserName });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load contacts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshChats()
        {
            try
            {
                var chats = await _chatApiClient.GetChats(_currentUserId);
                ChatsListBox.ItemsSource = chats;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load chats: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChatsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ChatsListBox.SelectedItem is Chat selectedChat) // Assuming Chat objects are directly in the list
            {
                var chatWindow = _serviceProvider.GetService<ChatWindow>();
                chatWindow?.InitializeChat(_currentUserId, selectedChat.Id);
                chatWindow?.Show();
            }
        }

        private async void ContactsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ContactsListBox.SelectedItem is ContactDto selectedContact)
            {
                try
                {
                    // Get or create a chat with the selected contact
                    var chatId = await _chatApiClient.GetOrCreateChat(_currentUserId, selectedContact.ContactId);

                    if (chatId.HasValue)
                    {
                        var chatWindow = _serviceProvider.GetService<ChatWindow>();
                        chatWindow?.InitializeChat(_currentUserId, chatId.Value);
                        chatWindow?.Show();
                    }
                    else
                    {
                        MessageBox.Show("Failed to get or create chat with contact.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening chat with contact: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void AddContactButton_Click(object sender, RoutedEventArgs e)
        {
            var addContactWindow = new AddContactWindow();
            if (addContactWindow.ShowDialog() == true)
            {
                var contactLogin = addContactWindow.ContactLogin;
                var success = await _chatApiClient.SendContactRequest(_currentUserId, contactLogin);
                if (success)
                {
                    MessageBox.Show("Contact added successfully!");
                    await RefreshContacts();
                }
                else
                {
                    MessageBox.Show("Failed to send contact request.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void CreateChatButton_Click(object sender, RoutedEventArgs e)
        {
            var createChatWindow = new CreateChatWindow();
            if (createChatWindow.ShowDialog() == true)
            {
                var chatName = createChatWindow.ChatName;
                try
                {
                    var chatId = await _chatApiClient.CreateChat(chatName, _currentUserId);
                    if (chatId.HasValue)
                    {
                        MessageBox.Show($"Chat '{chatName}' created successfully!");
                        await RefreshChats();
                    }
                    else
                    {
                        MessageBox.Show("Failed to create chat.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while creating chat: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
