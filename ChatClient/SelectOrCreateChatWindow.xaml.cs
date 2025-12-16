using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ChatClient.Shared.Models;
using ChatClient.Shared;

namespace ChatClient
{
    public partial class SelectOrCreateChatWindow : Window
    {
        private readonly IChatApiClient _apiClient;
        private readonly int _currentUserId;
        private readonly int _contactId;
        private readonly string _contactName;
        private List<Chat> _chats;
        
        public Chat? SelectedChat { get; private set; }
        public bool CreateNew { get; private set; }

        public SelectOrCreateChatWindow(IChatApiClient apiClient, int currentUserId, int contactId, string contactName)
        {
            InitializeComponent();
            _apiClient = apiClient;
            _currentUserId = currentUserId;
            _contactId = contactId;
            _contactName = contactName;
            
            DataContext = this;
            ContactName = contactName;
            
            LoadChats();
        }

        public string ContactName { get; set; }

        private async void LoadChats()
        {
            try
            {
                // Get all user's chats
                var allChats = await _apiClient.GetChats(_currentUserId);
                
                // Filter chats with this specific contact
                _chats = allChats.Where(c => c.UserIds.Contains(_contactId)).ToList();
                
                ChatsListBox.ItemsSource = _chats;
                
                if (_chats.Count == 0)
                {
                    MessageBox.Show("No existing chats found. You can create a new one.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load chats: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChatsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // This enables/disables the "Open Selected Chat" button
        }

        private void OpenChat_Click(object sender, RoutedEventArgs e)
        {
            if (ChatsListBox.SelectedItem is Chat selectedChat)
            {
                SelectedChat = selectedChat;
                CreateNew = false;
                DialogResult = true;
                Close();
            }
        }

        private void CreateNewChat_Click(object sender, RoutedEventArgs e)
        {
            CreateNew = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
