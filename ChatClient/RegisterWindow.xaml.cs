using ChatClient.Services;
using ChatClient.Shared;
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
using System.Windows.Shapes;

namespace ChatClient
{
    /// <summary>
    /// Interaction logic for RegisterWindow.xaml
    /// </summary>
    public partial class RegisterWindow : Window
    {
        private readonly IChatApiClient _chatApiClient;

        public RegisterWindow(IChatApiClient chatApiClient)
        {
            InitializeComponent();
            _chatApiClient = chatApiClient;
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var success = await _chatApiClient.Register(LoginTextBox.Text, PasswordTextBox.Password);
            if (success)
            {
                MessageBox.Show("Registration successful! You can now log in.");
                Close();
            }
            else
            {
                MessageBox.Show("Registration failed. Please try again.");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
