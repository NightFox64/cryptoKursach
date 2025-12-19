using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ChatClient.Shared;
using ChatClient.Shared.Models;

namespace ChatClient
{
    public partial class ChatSelectionView : UserControl
    {
        private readonly IChatApiClient _apiClient;
        private int _currentUserId;
        private int _contactId;
        private string _contactName = string.Empty;
        private List<Chat> _chats = new List<Chat>();
        
        public event EventHandler<Chat>? ChatSelected;

        public ChatSelectionView(IChatApiClient apiClient)
        {
            InitializeComponent();
            _apiClient = apiClient;
        }

        public async void Initialize(int currentUserId, int contactId, string contactName)
        {
            _currentUserId = currentUserId;
            _contactId = contactId;
            _contactName = contactName;
            
            ContactNameText.Text = $"Chats with {contactName}";
            
            await LoadChats();
            
            // Show selection panel by default
            ShowSelectionPanel();
        }

        private async System.Threading.Tasks.Task LoadChats()
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

        private void ChatsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // This enables/disables the "Open Selected Chat" button
        }

        private void OpenChat_Click(object sender, RoutedEventArgs e)
        {
            if (ChatsListBox.SelectedItem is Chat selectedChat)
            {
                ChatSelected?.Invoke(this, selectedChat);
            }
        }

        private void ShowCreatePanel_Click(object sender, RoutedEventArgs e)
        {
            SelectionPanel.Visibility = Visibility.Collapsed;
            CreatePanel.Visibility = Visibility.Visible;
        }

        private void ShowSelectionPanel_Click(object sender, RoutedEventArgs e)
        {
            ShowSelectionPanel();
        }

        private void ShowSelectionPanel()
        {
            CreatePanel.Visibility = Visibility.Collapsed;
            SelectionPanel.Visibility = Visibility.Visible;
        }

        private async void CreateChat_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ChatNameTextBox.Text))
            {
                MessageBox.Show("Please enter a chat name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var chatName = ChatNameTextBox.Text;
                var algorithm = ((ComboBoxItem)AlgorithmComboBox.SelectedItem).Content.ToString() ?? "RC6";
                var mode = ((ComboBoxItem)ModeComboBox.SelectedItem).Content.ToString() ?? "CBC";
                var padding = ((ComboBoxItem)PaddingComboBox.SelectedItem).Content.ToString() ?? "PKCS7";

                var newChat = await _apiClient.CreateChat(
                    chatName,
                    _currentUserId,
                    _contactId,
                    algorithm,
                    mode,
                    padding
                );

                if (newChat != null)
                {
                    MessageBox.Show($"Chat '{chatName}' created successfully!");
                    
                    // Reload chats and show selection panel
                    await LoadChats();
                    ShowSelectionPanel();
                    
                    // Automatically open the newly created chat
                    ChatSelected?.Invoke(this, newChat);
                }
                else
                {
                    MessageBox.Show("Failed to create chat.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating chat: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
