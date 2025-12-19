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
using System.Windows.Threading; // Added for DispatcherTimer

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
        private int _currentUserId;
        private DispatcherTimer _refreshTimer;
        private ChatView? _chatView;

        public ChatListWindow(IChatApiClient chatApiClient, IEncryptionService encryptionService, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _chatApiClient = chatApiClient;
            _encryptionService = encryptionService;
            _serviceProvider = serviceProvider;
            ContactsListBox.DisplayMemberPath = "ContactUserName";
            ChatsListBox.DisplayMemberPath = "Name";
            
            // Setup auto-refresh timer (every 3 seconds)
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(3);
            _refreshTimer.Tick += async (s, e) => await AutoRefresh();
        }

        public async void InitializeChat(int userId)
        {
            _currentUserId = userId;
            await RefreshContacts();
            await RefreshChats();
            
            // Start auto-refresh timer
            _refreshTimer.Start();
        }
        
        private async Task AutoRefresh()
        {
            try
            {
                await RefreshContacts();
                await RefreshChats();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auto-refresh error: {ex.Message}");
            }
        }

        private async Task RefreshContacts()
        {
            try
            {
                var contacts = await _chatApiClient.GetContacts(_currentUserId);
                ContactsListBox.ItemsSource = contacts;
                
                using (var scope = _serviceProvider.CreateScope())
                {
                    var localDataService = scope.ServiceProvider.GetRequiredService<ILocalDataService>();
                    foreach (var contact in contacts)
                    {
                        await localDataService.SaveUserAsync(new User { Id = contact.ContactId, Login = contact.ContactUserName });
                    }
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
            if (ChatsListBox.SelectedItem is Chat selectedChat)
            {
                OpenChatInView(selectedChat.Id, selectedChat);
            }
        }

        private void OpenChatInView(int chatId, Chat? chat = null)
        {
            // Create ChatView if not exists
            if (_chatView == null)
            {
                _chatView = _serviceProvider.GetRequiredService<ChatView>();
                ChatViewContainer.Child = _chatView;
            }
            
            _chatView.InitializeChat(_currentUserId, chatId, chat);
        }

        private async void ContactsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ContactsListBox.SelectedItem is ContactDto selectedContact)
            {
                try
                {
                    // Show dialog to select existing chat or create new one
                    var selectChatWindow = new SelectOrCreateChatWindow(
                        _chatApiClient, 
                        _currentUserId, 
                        selectedContact.ContactId, 
                        selectedContact.ContactUserName);
                    
                    if (selectChatWindow.ShowDialog() == true)
                    {
                        Chat? chatToOpen = null;
                        
                        if (selectChatWindow.CreateNew)
                        {
                            // Show create chat dialog
                            var createChatWindow = new CreateChatWindow();
                            if (createChatWindow.ShowDialog() == true)
                            {
                                chatToOpen = await _chatApiClient.CreateChat(
                                    createChatWindow.ChatName,
                                    _currentUserId,
                                    selectedContact.ContactId,
                                    createChatWindow.CipherAlgorithm,
                                    createChatWindow.CipherMode,
                                    createChatWindow.PaddingMode
                                );
                                
                                if (chatToOpen != null)
                                {
                                    MessageBox.Show($"Chat '{createChatWindow.ChatName}' created successfully!");
                                    await RefreshChats();
                                }
                            }
                        }
                        else
                        {
                            chatToOpen = selectChatWindow.SelectedChat;
                        }
                        
                        if (chatToOpen != null)
                        {
                            OpenChatInView(chatToOpen.Id, chatToOpen);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening chat with contact: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // Clear selection to allow re-selecting the same contact
                    ContactsListBox.SelectedItem = null;
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
            MessageBox.Show("Please select a contact to create a chat with them.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?", "Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Services.FileLogger.Log("[ChatListWindow] Logout button clicked");
                    
                    // Stop the refresh timer
                    _refreshTimer?.Stop();
                    Services.FileLogger.Log("[ChatListWindow] Refresh timer stopped");
                    
                    // Cleanup ChatView
                    if (_chatView != null)
                    {
                        Services.FileLogger.Log("[ChatListWindow] Cleaning up ChatView");
                        await _chatView.CleanupAsync();
                        _chatView = null;
                    }
                    
                    // Clear authentication token
                    _chatApiClient.ClearAuthToken();
                    Services.FileLogger.Log("[ChatListWindow] Auth token cleared");
                    
                    // Open login window
                    var loginWindow = _serviceProvider.GetService<LoginWindow>();
                    if (loginWindow != null)
                    {
                        loginWindow.Show();
                        Services.FileLogger.Log("[ChatListWindow] Login window opened");
                    }
                    
                    // Close this window
                    Close();
                    Services.FileLogger.Log("[ChatListWindow] ChatListWindow closed");
                }
                catch (Exception ex)
                {
                    Services.FileLogger.Log($"[ChatListWindow] Error during logout: {ex.Message}");
                    MessageBox.Show($"Error during logout: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        protected override async void OnClosed(EventArgs e)
        {
            Services.FileLogger.Log($"[ChatListWindow] OnClosed called, _chatView is {(_chatView == null ? "null" : "not null")}");
            
            // Stop the refresh timer when window closes
            _refreshTimer?.Stop();
            
            // Cleanup ChatView
            if (_chatView != null)
            {
                Services.FileLogger.Log($"[ChatListWindow] Calling CleanupAsync on ChatView");
                try
                {
                    await _chatView.CleanupAsync();
                    Services.FileLogger.Log($"[ChatListWindow] CleanupAsync completed");
                }
                catch (Exception ex)
                {
                    Services.FileLogger.Log($"[ChatListWindow] Error during cleanup: {ex.Message}");
                }
                _chatView = null; // Clear reference to prevent double cleanup
            }
            
            base.OnClosed(e);
        }
    }
}
