using ChatClient.Services;
using ChatClient.Shared;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ChatClient
{
    /// <summary>
    /// Interaction logic for ChatWindow.xaml
    /// </summary>
    public partial class ChatWindow : Window
    {
        private readonly IChatApiClient _chatApiClient;
        private readonly IEncryptionService _encryptionService;
        private int _currentUserId;
        private int _currentChatId;
        private long _lastDeliveryId = 0;

        private byte[] _sessionKey = new byte[32]; // Placeholder for actual session key
        private byte[] _iv = new byte[8]; // Placeholder for actual IV

        private CancellationTokenSource _cancellationTokenSource;

        public ObservableCollection<Models.Message> Messages { get; set; }

        public ChatWindow(IChatApiClient chatApiClient, IEncryptionService encryptionService)
        {
            InitializeComponent();
            _chatApiClient = chatApiClient;
            _encryptionService = encryptionService;

            Messages = new ObservableCollection<Models.Message>();
            ChatHistoryListBox.ItemsSource = Messages;

            // Initialize dummy session key and IV for testing
            new Random().NextBytes(_sessionKey);
            new Random().NextBytes(_iv);

            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => ReceiveMessagesLoop(_cancellationTokenSource.Token));
        }

        public void InitializeChat(int userId, int chatId)
        {
            _currentUserId = userId;
            _currentChatId = chatId;
            Title = $"Chat with User {_currentUserId} in Chat {_currentChatId}";
            // Here you would also request/generate session keys for this chat
            // and load chat history.
        }

        private void AlgorithmSettings_Click(object sender, RoutedEventArgs e)
        {
            var algorithmSettingsWindow = new AlgorithmSettingsWindow(_encryptionService);
            algorithmSettingsWindow.ShowDialog();
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                // Implement file sending logic
                MessageBox.Show($"Selected file: {openFileDialog.FileName}");
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text)) return;

            var plainTextBytes = Encoding.UTF8.GetBytes(MessageTextBox.Text);
            var encryptedBytes = _encryptionService.Encrypt(plainTextBytes, _sessionKey, _iv);
            var encryptedContent = Convert.ToBase64String(encryptedBytes);

            var success = await _chatApiClient.SendEncryptedFragment(_currentChatId, _currentUserId, encryptedContent);
            if (success)
            {
                Messages.Add(new Models.Message { SenderId = _currentUserId, Content = MessageTextBox.Text, IsMine = true, Id = (int)DateTimeOffset.Now.ToUnixTimeMilliseconds() });
                MessageTextBox.Clear();
            }
            else
            {
                MessageBox.Show("Failed to send message.");
            }
        }

        private async void AttachFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    TransferProgressBar.Visibility = Visibility.Visible;
                    byte[] fileBytes = File.ReadAllBytes(openFileDialog.FileName);
                    var encryptedBytes = _encryptionService.Encrypt(fileBytes, _sessionKey, _iv);
                    var encryptedContent = Convert.ToBase64String(encryptedBytes);

                    var success = await _chatApiClient.SendEncryptedFragment(_currentChatId, _currentUserId, encryptedContent);
                    if (success)
                    {
                        string fileName = Path.GetFileName(openFileDialog.FileName);
                        if (fileName.EndsWith(".jpg") || fileName.EndsWith(".png") || fileName.EndsWith(".gif"))
                        {
                            Messages.Add(new Models.Message { SenderId = _currentUserId, Content = "[IMAGE]" + encryptedContent, IsMine = true, Id = (int)DateTimeOffset.Now.ToUnixTimeMilliseconds() });
                        }
                        else
                        {
                            Messages.Add(new Models.Message { SenderId = _currentUserId, Content = "[FILE]" + fileName + "|" + encryptedContent, IsMine = true, Id = (int)DateTimeOffset.Now.ToUnixTimeMilliseconds() });
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failed to send file.");
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show("Error sending file.");
                }
                finally
                {
                    TransferProgressBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task ReceiveMessagesLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_currentChatId > 0) // Only poll if a chat is initialized
                    {
                        var serverMessage = await _chatApiClient.ReceiveEncryptedFragment(_currentChatId, _lastDeliveryId);
                        if (serverMessage != null && serverMessage.Content != null && serverMessage.SenderId != _currentUserId)
                        {
                            // Decrypt and display message
                            var encryptedContent = serverMessage.Content;
                            string decryptedContent = string.Empty;

                            if (encryptedContent.StartsWith("[IMAGE]"))
                            {
                                var base64Image = encryptedContent.Substring("[IMAGE]".Length);
                                var decryptedBytes = _encryptionService.Decrypt(Convert.FromBase64String(base64Image), _sessionKey, _iv);
                                decryptedContent = "[IMAGE]" + Convert.ToBase64String(decryptedBytes);
                            }
                            else if (encryptedContent.StartsWith("[FILE]"))
                            {
                                var parts = encryptedContent.Split('|');
                                var fileName = parts[0].Substring("[FILE]".Length);
                                var base64File = parts[1];
                                var decryptedBytes = _encryptionService.Decrypt(Convert.FromBase64String(base64File), _sessionKey, _iv);
                                // For files, we might want to save them or offer download
                                decryptedContent = "[FILE]" + fileName + "|" + Convert.ToBase64String(decryptedBytes);
                            }
                            else
                            {
                                var decryptedBytes = _encryptionService.Decrypt(Convert.FromBase64String(encryptedContent), _sessionKey, _iv);
                                decryptedContent = Encoding.UTF8.GetString(decryptedBytes);
                            }
                            
                            // Ensure UI update is on the UI thread
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Messages.Add(new Models.Message { SenderId = serverMessage.SenderId, Content = decryptedContent, IsMine = false, Id = (int)DateTimeOffset.Now.ToUnixTimeMilliseconds() });
                                _lastDeliveryId = serverMessage.DeliveryId;
                            });
                        }
                    }
                }
                catch (Exception)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // MessageBox.Show($"Error receiving messages: {ex.Message}");
                    });
                }
                await Task.Delay(1000, cancellationToken); // Poll every 1 second
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource.Cancel();
            base.OnClosed(e);
        }
    }
}
