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
        private RabbitMQConsumerService? _rabbitMQConsumer;
        private bool _useRabbitMQ = true; // Set to true to use RabbitMQ, false for polling

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
            
            // Initialize RabbitMQ consumer
            if (_useRabbitMQ)
            {
                _rabbitMQConsumer = new RabbitMQConsumerService();
                _rabbitMQConsumer.MessageReceived += OnRabbitMQMessageReceived;
                _ = InitializeRabbitMQAsync();
            }
            else
            {
                // Fallback to polling
                Task.Run(() => ReceiveMessagesLoop(_cancellationTokenSource.Token));
            }
        }

        private async Task InitializeRabbitMQAsync()
        {
            try
            {
                bool connected = await _rabbitMQConsumer!.ConnectAsync();
                if (connected)
                {
                    Console.WriteLine("Connected to RabbitMQ successfully");
                }
                else
                {
                    Console.WriteLine("Failed to connect to RabbitMQ, falling back to polling");
                    _useRabbitMQ = false;
                    Task.Run(() => ReceiveMessagesLoop(_cancellationTokenSource.Token));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RabbitMQ initialization failed: {ex.Message}, falling back to polling");
                _useRabbitMQ = false;
                Task.Run(() => ReceiveMessagesLoop(_cancellationTokenSource.Token));
            }
        }

        private async void OnRabbitMQMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            try
            {
                var messageData = e.MessageData;
                
                // Skip if we already processed this message
                if (messageData.DeliveryId <= _lastDeliveryId)
                    return;

                // Decrypt the content
                var encryptedBytes = Convert.FromBase64String(messageData.Content ?? "");
                var decryptedBytes = _encryptionService.Decrypt(encryptedBytes, _sessionKey, _iv);
                var decryptedContent = Encoding.UTF8.GetString(decryptedBytes);

                var sender_user = await _localDataService.GetUserByIdAsync(messageData.SenderId);
                var senderLogin = sender_user?.Login ?? "Unknown";

                var message = new Message 
                { 
                    ChatId = messageData.ChatId, 
                    SenderId = messageData.SenderId, 
                    Content = decryptedContent, 
                    IsMine = messageData.SenderId == _currentUserId, 
                    Timestamp = messageData.Timestamp, 
                    DeliveryId = messageData.DeliveryId 
                };
                
                await _localDataService.AddMessageAsync(message);

                if (!decryptedContent.StartsWith($"({senderLogin}): "))
                {
                    message.Content = $"({senderLogin}): {decryptedContent}";
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Messages.Add(message);
                    _lastDeliveryId = messageData.DeliveryId;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing RabbitMQ message: {ex.Message}");
            }
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
            
            // Subscribe to RabbitMQ queue for this chat
            if (_useRabbitMQ && _rabbitMQConsumer != null)
            {
                try
                {
                    await _rabbitMQConsumer.SubscribeToChatAsync(chatId, _cancellationTokenSource.Token);
                    Console.WriteLine($"Subscribed to RabbitMQ queue for chat {chatId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to subscribe to RabbitMQ: {ex.Message}");
                }
            }
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
                        if (!message.Content.StartsWith($"({senderLogin}): "))
                        {
                            message.Content = $"({senderLogin}): {message.Content}";
                        }
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

                if (!message.Content.StartsWith($"({senderLogin}): "))
                {
                    message.Content = $"({senderLogin}): {plainText}";
                }
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

                    var messageContentBytes = Encoding.UTF8.GetBytes(messageContent);
                    var encryptedBytes = _encryptionService.Encrypt(messageContentBytes, _sessionKey, _iv);
                    var encryptedContent = Convert.ToBase64String(encryptedBytes);
                    
                    var success = await _chatApiClient.SendEncryptedFragment(_currentChatId, _currentUserId, encryptedContent);
                    if (success)
                    {
                        var currentUser = await _localDataService.GetUserByIdAsync(_currentUserId);
                        var senderLogin = currentUser?.Login ?? "Me";

                        var message = new Message { ChatId = _currentChatId, SenderId = _currentUserId, Content = messageContent, IsMine = true, Timestamp = DateTime.UtcNow };
                        await _localDataService.AddMessageAsync(message);

                        if (!message.Content.StartsWith($"({senderLogin}): "))
                        {
                            message.Content = $"({senderLogin}): {messageContent}";
                        }
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
                            try
                            {
                                var encryptedBytes = Convert.FromBase64String(serverMessage.Content);
                                var decryptedBytes = _encryptionService.Decrypt(encryptedBytes, _sessionKey, _iv);
                                var decryptedContent = Encoding.UTF8.GetString(decryptedBytes);

                                var sender = await _localDataService.GetUserByIdAsync(serverMessage.SenderId);
                                var senderLogin = sender?.Login ?? "Unknown";

                                var message = new Message { ChatId = _currentChatId, SenderId = serverMessage.SenderId, Content = decryptedContent, IsMine = false, Timestamp = DateTime.UtcNow, DeliveryId = serverMessage.DeliveryId };
                                await _localDataService.AddMessageAsync(message);

                                if (!decryptedContent.StartsWith($"({senderLogin}): "))
                                {
                                    message.Content = $"({senderLogin}): {decryptedContent}";
                                }

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
                                if(serverMessage != null)
                                {
                                    _lastDeliveryId = serverMessage.DeliveryId; // Still update delivery ID to not get stuck
                                }
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
            try
            {
                _cancellationTokenSource.Cancel();
                
                // Unsubscribe from RabbitMQ (fire and forget with timeout)
                if (_useRabbitMQ && _rabbitMQConsumer != null)
                {
                    try
                    {
                        // Use ConfigureAwait(false) to avoid deadlock and add timeout
                        var unsubscribeTask = Task.Run(async () => 
                        {
                            try
                            {
                                await _rabbitMQConsumer.UnsubscribeFromChatAsync(_currentChatId);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error unsubscribing from RabbitMQ: {ex.Message}");
                            }
                        });
                        
                        // Wait with timeout to prevent hanging
                        if (!unsubscribeTask.Wait(TimeSpan.FromSeconds(2)))
                        {
                            Console.WriteLine("RabbitMQ unsubscribe timeout - continuing with dispose");
                        }
                        
                        _rabbitMQConsumer.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during RabbitMQ cleanup: {ex.Message}");
                    }
                }
                
                // Dispose cancellation token source
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during window cleanup: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}
