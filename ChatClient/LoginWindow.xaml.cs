using ChatClient.Services;
using ChatClient.Shared;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChatClient
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly IChatApiClient _chatApiClient;
        private readonly IServiceProvider _serviceProvider;

        public LoginWindow(IChatApiClient chatApiClient, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _chatApiClient = chatApiClient;
            _serviceProvider = serviceProvider;
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var userId = await _chatApiClient.Login(LoginTextBox.Text, PasswordTextBox.Password);
            if (userId.HasValue)
            {
                MessageBox.Show("Login successful!");
                var chatListWindow = _serviceProvider.GetService<ChatListWindow>();
                chatListWindow?.InitializeChat(userId.Value);
                chatListWindow?.Show();
                Close();
            }
            else
            {
                MessageBox.Show("Login failed. Invalid credentials.");
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var registerWindow = _serviceProvider.GetService<RegisterWindow>();
            registerWindow?.ShowDialog();
        }
    }
}
