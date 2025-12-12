using ChatClient.Models;
using ChatClient.Services;
using ChatClient.Shared;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

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

        public ChatListWindow(IChatApiClient chatApiClient, IEncryptionService encryptionService, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _chatApiClient = chatApiClient;
            _encryptionService = encryptionService;
            _serviceProvider = serviceProvider;
        }

        public void InitializeChat(int userId)
        {
            _currentUserId = userId;
            // Load contacts and chats here
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
    }
}
