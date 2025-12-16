using ChatClient.Services;
using ChatClient.Shared;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.Windows;
using ChatClient.Models;

namespace ChatClient
{
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
            try
            {
                var userId = await _chatApiClient.Login(LoginTextBox.Text, PasswordTextBox.Password);
                if (userId.HasValue)
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var localDataService = scope.ServiceProvider.GetRequiredService<ILocalDataService>();
                        await localDataService.SaveUserAsync(new User { Id = userId.Value, Login = LoginTextBox.Text });
                    }

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
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during login: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var registerWindow = _serviceProvider.GetService<RegisterWindow>();
            registerWindow?.ShowDialog();
        }
    }
}
