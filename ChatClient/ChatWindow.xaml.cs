using ChatClient.Services;
using ChatClient.Shared;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ChatClient.Shared.Models; // Added for shared models
using System.Numerics; // Added for BigInteger
using DiffieHellman; // Added for DiffieHellman class
using System.Security.Cryptography; // Added for SHA256
using System.Linq; // Added for Concat and Take

namespace ChatClient
{
    /// <summary>
    /// Interaction logic for ChatWindow.xaml
    /// </summary>
    public partial class ChatWindow : Window
    {
        private readonly IChatApiClient _chatApiClient;
        private readonly IEncryptionService _encryptionService;
        private readonly ILocalDataService _localDataService;
        private int _currentUserId;
        private int _currentChatId;
        private long _lastDeliveryId = 0;

        private byte[] _sessionKey = new byte[32]; // Will be set by DH
        private byte[] _iv = new byte[16]; // Will be set by DH

        private DiffieHellman.DiffieHellman? _clientDh; // Client's DH instance

        private CancellationTokenSource _cancellationTokenSource;

        public ObservableCollection<Message> Messages { get; set; }

        public ChatWindow(IChatApiClient chatApiClient, IEncryptionService encryptionService, ILocalDataService localDataService)
        {
            InitializeComponent();
            _chatApiClient = chatApiClient;
            _encryptionService = encryptionService;
            _localDataService = localDataService;

            Messages = new ObservableCollection<Message>();
            ChatHistoryListBox.ItemsSource = Messages;
            
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => ReceiveMessagesLoop(_cancellationTokenSource.Token));
        }

        public async void InitializeChat(int userId, int chatId)
        {
            _currentUserId = userId;
            _currentChatId = chatId;
            Title = $"Chat with User {_currentUserId} in Chat {_currentChatId}";

            // Ensure the chat exists in the local database to satisfy foreign key constraints
            var chat = await _localDataService.GetChatAsync(chatId);
            if (chat == null)
            {
                await _localDataService.SaveChatAsync(new Chat { Id = chatId, Name = $"Chat {chatId}" });
            }

            _ = LoadChatHistory(); // Load history first
            _ = EstablishSessionKey(); // Then establish a new session key
        }

        private async Task EstablishSessionKey()
        {
            try
            {
                _clientDh = new DiffieHellman.DiffieHellman(512); 
                BigInteger clientPublicKey = _clientDh.PublicKey;

                var result = await _chatApiClient.RequestSessionKey(_currentChatId, _currentUserId, clientPublicKey);

                if (result.HasValue)
                {
                    var (serverPublicKey, p, g) = result.Value;
                    
                    _clientDh = new DiffieHellman.DiffieHellman(_clientDh.PrivateKey, p, g);
                    
                    BigInteger sharedSecret = _clientDh.GetSharedSecret(serverPublicKey);
                    
                    byte[] sharedSecretBytes = sharedSecret.ToByteArray();
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] hashedSecret = sha256.ComputeHash(sharedSecretBytes);
                        
                        int requiredKeyLength = _encryptionService.RequiredKeySize;
                        
                        _sessionKey = new byte[requiredKeyLength];
                        Array.Copy(hashedSecret, 0, _sessionKey, 0, requiredKeyLength);
                        
                        using (SHA256 sha256_iv = SHA256.Create())
                        {
                            byte[] iv_seed = Encoding.UTF8.GetBytes("IV_Seed_For_DH").Concat(sharedSecretBytes).ToArray();
                            _iv = sha256_iv.ComputeHash(iv_seed).Take(16).ToArray();
                        }
                    }
                    MessageBox.Show("Session key established successfully.");
                }
                else
                {
                    MessageBox.Show("Failed to establish session key: Server response empty.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error establishing session key: {ex.Message}");
            }
        }

        private async Task LoadChatHistory()
        {
            try
            {
                var historyMessages = await _localDataService.GetChatHistoryAsync(_currentChatId);

                var senderIds = historyMessages.Select(m => m.SenderId).Distinct().ToList();
                var senders = new Dictionary<int, string>();
                foreach (var senderId in senderIds)
                {
                    var user = await _localDataService.GetUserByIdAsync(senderId);
                    senders[senderId] = user?.Login ?? "Unknown";
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Messages.Clear();
                    foreach (var message in historyMessages)
                    {
                        var senderLogin = senders.TryGetValue(message.SenderId, out var login) ? login : "Unknown";
                        message.Content = $"({senderLogin}): {message.Content}";
                        Messages.Add(new Message
                        {
                            SenderId = message.SenderId,
                            Content = message.Content,
                            IsMine = message.SenderId == _currentUserId,
                            Id = message.Id
                        });
                        if (message.Id > _lastDeliveryId)
                        {
                            _lastDeliveryId = message.Id;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading chat history: {ex.Message}");
                });
            }
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
                MessageBox.Show($"Selected file: {openFileDialog.FileName}");
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text)) return;

            var plainText = MessageTextBox.Text;
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = _encryptionService.Encrypt(plainTextBytes, _sessionKey, _iv);
            var encryptedContent = Convert.ToBase64String(encryptedBytes);

            var success = await _chatApiClient.SendEncryptedFragment(_currentChatId, _currentUserId, encryptedContent);
            if (success)
            {
                var currentUser = await _localDataService.GetUserByIdAsync(_currentUserId);
                var senderLogin = currentUser?.Login ?? "Me";

                var message = new Message { ChatId = _currentChatId, SenderId = _currentUserId, Content = plainText, IsMine = true, Timestamp = DateTime.UtcNow };
                await _localDataService.AddMessageAsync(message);

                message.Content = $"({senderLogin}): {plainText}";
                Messages.Add(message);
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
                    byte[] fileBytes = System.IO.File.ReadAllBytes(openFileDialog.FileName);
                    var encryptedBytes = _encryptionService.Encrypt(fileBytes, _sessionKey, _iv);
                    var encryptedContent = Convert.ToBase64String(encryptedBytes);
                    
                    var success = await _chatApiClient.SendEncryptedFragment(_currentChatId, _currentUserId, encryptedContent);
                    if (success)
                    {
                        var currentUser = await _localDataService.GetUserByIdAsync(_currentUserId);
                        var senderLogin = currentUser?.Login ?? "Me";

                        string fileName = Path.GetFileName(openFileDialog.FileName);
                        string messageContent;
                        if (fileName.EndsWith(".jpg") || fileName.EndsWith(".png") || fileName.EndsWith(".gif"))
                        {
                            messageContent = "[IMAGE]" + Convert.ToBase64String(fileBytes);
                        }
                        else
                        {
                            messageContent = "[FILE]" + fileName + "|" + Convert.ToBase64String(fileBytes);
                        }
                        var message = new Message { ChatId = _currentChatId, SenderId = _currentUserId, Content = messageContent, IsMine = true, Timestamp = DateTime.UtcNow };
                        await _localDataService.AddMessageAsync(message);

                        message.Content = $"({senderLogin}): {messageContent}";
                        Messages.Add(message);
                    }
                    else
                    {
                        MessageBox.Show("Failed to send file.");
                    }
                }
                catch (Exception ex)
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
                    if (_currentChatId > 0)
                    {
                        var serverMessage = await _chatApiClient.ReceiveEncryptedFragment(_currentChatId, _lastDeliveryId);
                        if (serverMessage != null && serverMessage.Content != null)
                        {
                            string decryptedContent;
                            try
                            {
                                var encryptedContent = serverMessage.Content;
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
                                    decryptedContent = "[FILE]" + fileName + "|" + Convert.ToBase64String(decryptedBytes);
                                }
                                else
                                {
                                    var decryptedBytes = _encryptionService.Decrypt(Convert.FromBase64String(encryptedContent), _sessionKey, _iv);
                                    decryptedContent = Encoding.UTF8.GetString(decryptedBytes);
                                }

                                var sender = await _localDataService.GetUserByIdAsync(serverMessage.SenderId);
                                var senderLogin = sender?.Login ?? "Unknown";

                                var message = new Message { ChatId = _currentChatId, SenderId = serverMessage.SenderId, Content = decryptedContent, IsMine = false, Timestamp = DateTime.UtcNow, DeliveryId = serverMessage.DeliveryId };
                                await _localDataService.AddMessageAsync(message);

                                message.Content = $"({senderLogin}): {decryptedContent}";

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Messages.Add(message);
                                    _lastDeliveryId = serverMessage.DeliveryId;
                                });
                            }
                            catch (Exception)
                            {
                                // Decryption failed, probably old session, ignore.
                                // The message is not stored locally and not displayed.
                                _lastDeliveryId = serverMessage.DeliveryId; // Still update delivery ID to not get stuck
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving messages: {ex.Message}");
                }
                await Task.Delay(1000, cancellationToken);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource.Cancel();
            base.OnClosed(e);
        }
    }
}
