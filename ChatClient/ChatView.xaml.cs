using ChatClient.Services;
using ChatClient.Shared;
using ChatClient.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DiffieHellman;

namespace ChatClient
{
    public partial class ChatView : UserControl
    {
        private readonly IChatApiClient _chatApiClient;
        private readonly IEncryptionService _encryptionService;
        private readonly IServiceProvider _serviceProvider;
        private int _currentUserId;
        private int _currentChatId;
        private long _lastDeliveryId = 0;

        private byte[] _sessionKey = new byte[32];
        private byte[] _iv = Array.Empty<byte>();
        private DiffieHellman.DiffieHellman? _clientDh;

        private CancellationTokenSource _cancellationTokenSource;
        private RabbitMQConsumerService? _rabbitMQConsumer;
        private bool _useRabbitMQ = true;
        private DispatcherTimer _messageRefreshTimer;

        public ObservableCollection<Message> Messages { get; set; }

        public ChatView(IChatApiClient chatApiClient, IEncryptionService encryptionService, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _chatApiClient = chatApiClient;
            _encryptionService = encryptionService;
            _serviceProvider = serviceProvider;

            Messages = new ObservableCollection<Message>();
            ChatHistoryListBox.ItemsSource = Messages;
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Setup automatic message refresh timer (every 2 seconds)
            _messageRefreshTimer = new DispatcherTimer();
            _messageRefreshTimer.Interval = TimeSpan.FromSeconds(2);
            _messageRefreshTimer.Tick += async (s, e) => await CheckForNewMessages();
            
            // Initialize RabbitMQ consumer (but don't connect yet - wait for InitializeChat)
            if (_useRabbitMQ)
            {
                _rabbitMQConsumer = new RabbitMQConsumerService();
                _rabbitMQConsumer.MessageReceived += OnRabbitMQMessageReceived;
            }
            else
            {
                // Fallback to polling
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

                // Skip if this is our own message (to prevent duplication when we send)
                if (messageData.SenderId == _currentUserId)
                {
                    _lastDeliveryId = messageData.DeliveryId;
                    return;
                }

                // Decrypt the content
                var encryptedBytes = Convert.FromBase64String(messageData.Content ?? "");
                var decryptedBytes = _encryptionService.Decrypt(encryptedBytes, _sessionKey, _iv);
                var decryptedContent = Encoding.UTF8.GetString(decryptedBytes);

                // Create a new scope for database operations to avoid threading issues
                using (var scope = _serviceProvider.CreateScope())
                {
                    var localDataService = scope.ServiceProvider.GetRequiredService<ILocalDataService>();
                    
                    var sender_user = await localDataService.GetUserByIdAsync(messageData.SenderId);
                    var senderLogin = sender_user?.Login ?? "Unknown";

                    var message = new Message 
                    { 
                        ChatId = messageData.ChatId, 
                        SenderId = messageData.SenderId, 
                        Content = decryptedContent, 
                        IsMine = false, // It's not our message since we filtered above
                        Timestamp = messageData.Timestamp, 
                        DeliveryId = messageData.DeliveryId 
                    };
                    
                    await localDataService.AddMessageAsync(message);

                    if (!decryptedContent.StartsWith($"({senderLogin}): "))
                    {
                        message.Content = $"({senderLogin}): {decryptedContent}";
                    }

                    Dispatcher.Invoke(() =>
                    {
                        Messages.Add(message);
                        _lastDeliveryId = messageData.DeliveryId;
                        
                        // Auto-scroll to the new message
                        if (ChatHistoryListBox.Items.Count > 0)
                        {
                            ChatHistoryListBox.ScrollIntoView(ChatHistoryListBox.Items[ChatHistoryListBox.Items.Count - 1]);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing RabbitMQ message: {ex.Message}");
            }
        }

        public async void InitializeChat(int userId, int chatId, Chat? chat = null)
        {
            _currentUserId = userId;
            _currentChatId = chatId;
            
            // Show the chat UI
            RootGrid.Visibility = Visibility.Visible;
            EmptyStateTextBlock.Visibility = Visibility.Collapsed;
            
            // Create a scope for database operations
            using (var scope = _serviceProvider.CreateScope())
            {
                var localDataService = scope.ServiceProvider.GetRequiredService<ILocalDataService>();
                
                // If chat object is provided, use its settings
                if (chat != null)
                {
                    ChatTitleTextBlock.Text = chat.Name ?? $"Chat {chatId}";
                    
                    // Set encryption settings from chat
                    if (!string.IsNullOrEmpty(chat.CipherAlgorithm))
                    {
                        _encryptionService.SetCipherAlgorithm(chat.CipherAlgorithm);
                    }
                    if (!string.IsNullOrEmpty(chat.CipherMode))
                    {
                        _encryptionService.SetCipherMode(chat.CipherMode);
                    }
                    if (!string.IsNullOrEmpty(chat.PaddingMode))
                    {
                        _encryptionService.SetPaddingMode(chat.PaddingMode);
                    }
                    
                    // Save chat to local database
                    await localDataService.SaveChatAsync(chat);
                }
                else
                {
                    ChatTitleTextBlock.Text = $"Chat {_currentChatId}";
                    
                    // Ensure the chat exists in the local database to satisfy foreign key constraints
                    var localChat = await localDataService.GetChatAsync(chatId);
                    if (localChat == null)
                    {
                        await localDataService.SaveChatAsync(new Chat { Id = chatId, Name = $"Chat {chatId}" });
                    }
                }
            }

            // Initialize RabbitMQ connection and subscribe BEFORE loading history
            if (_useRabbitMQ && _rabbitMQConsumer != null)
            {
                try
                {
                    bool connected = await _rabbitMQConsumer.ConnectAsync();
                    if (connected)
                    {
                        await _rabbitMQConsumer.SubscribeToChatAsync(chatId, _cancellationTokenSource.Token);
                        Console.WriteLine($"Connected to RabbitMQ and subscribed to chat {chatId}");
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

            _ = LoadChatHistory(); // Load history first
            _ = EstablishSessionKey(); // Then establish a new session key
            
            // Start automatic message refresh timer
            _messageRefreshTimer.Start();
        }

        public void CloseChat()
        {
            // Stop timer
            _messageRefreshTimer?.Stop();
            
            // Unsubscribe from RabbitMQ
            if (_useRabbitMQ && _rabbitMQConsumer != null && _currentChatId > 0)
            {
                _ = _rabbitMQConsumer.UnsubscribeFromChatAsync(_currentChatId);
            }
            
            // Clear messages
            Messages.Clear();
            _lastDeliveryId = 0;
            
            // Hide chat UI
            RootGrid.Visibility = Visibility.Collapsed;
            EmptyStateTextBlock.Visibility = Visibility.Visible;
            
            // Reset state
            _currentChatId = 0;
            _currentUserId = 0;
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
                        int requiredIvLength = _encryptionService.BlockSize;
                        
                        _sessionKey = new byte[requiredKeyLength];
                        Array.Copy(hashedSecret, 0, _sessionKey, 0, requiredKeyLength);
                        
                        using (SHA256 sha256_iv = SHA256.Create())
                        {
                            byte[] iv_seed = Encoding.UTF8.GetBytes("IV_Seed_For_DH").Concat(sharedSecretBytes).ToArray();
                            _iv = sha256_iv.ComputeHash(iv_seed).Take(requiredIvLength).ToArray();
                        }
                    }
                    Console.WriteLine("Session key established successfully.");
                }
                else
                {
                    Console.WriteLine("Failed to establish session key: Server response empty.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error establishing session key: {ex.Message}");
            }
        }

        private async Task LoadChatHistory()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var localDataService = scope.ServiceProvider.GetRequiredService<ILocalDataService>();
                    
                    var historyMessages = await localDataService.GetChatHistoryAsync(_currentChatId);

                    var senderIds = historyMessages.Select(m => m.SenderId).Distinct().ToList();
                    var senders = new Dictionary<int, string>();
                    foreach (var senderId in senderIds)
                    {
                        var user = await localDataService.GetUserByIdAsync(senderId);
                        senders[senderId] = user?.Login ?? "Unknown";
                    }

                    Dispatcher.Invoke(() =>
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
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error loading chat history: {ex.Message}");
                });
            }
        }

        private async Task CheckForNewMessages()
        {
            try
            {
                if (_currentChatId > 0 && _sessionKey != null && _sessionKey.Length > 0)
                {
                    var serverMessage = await _chatApiClient.ReceiveEncryptedFragment(_currentChatId, _lastDeliveryId);
                    if (serverMessage != null && serverMessage.Content != null)
                    {
                        try
                        {
                            var encryptedBytes = Convert.FromBase64String(serverMessage.Content);
                            var decryptedBytes = _encryptionService.Decrypt(encryptedBytes, _sessionKey, _iv);
                            var decryptedContent = Encoding.UTF8.GetString(decryptedBytes);

                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var localDataService = scope.ServiceProvider.GetRequiredService<ILocalDataService>();
                                
                                var sender = await localDataService.GetUserByIdAsync(serverMessage.SenderId);
                                var senderLogin = sender?.Login ?? "Unknown";

                                var message = new Message 
                                { 
                                    ChatId = _currentChatId, 
                                    SenderId = serverMessage.SenderId, 
                                    Content = decryptedContent, 
                                    IsMine = serverMessage.SenderId == _currentUserId, 
                                    Timestamp = serverMessage.Timestamp, 
                                    DeliveryId = serverMessage.DeliveryId 
                                };
                                await localDataService.AddMessageAsync(message);

                                if (!decryptedContent.StartsWith($"({senderLogin}): "))
                                {
                                    message.Content = $"({senderLogin}): {decryptedContent}";
                                }

                                Dispatcher.Invoke(() =>
                                {
                                    Messages.Add(message);
                                    _lastDeliveryId = serverMessage.DeliveryId;
                                    
                                    // Auto-scroll to the new message
                                    if (ChatHistoryListBox.Items.Count > 0)
                                    {
                                        ChatHistoryListBox.ScrollIntoView(ChatHistoryListBox.Items[ChatHistoryListBox.Items.Count - 1]);
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            // Decryption failed, probably old session, ignore.
                            Console.WriteLine($"Error decrypting message: {ex.Message}");
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
                Console.WriteLine($"Error checking for new messages: {ex.Message}");
            }
        }

        private async Task ReceiveMessagesLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForNewMessages();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving messages: {ex.Message}");
                }
                await Task.Delay(1000, cancellationToken);
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
                using (var scope = _serviceProvider.CreateScope())
                {
                    var localDataService = scope.ServiceProvider.GetRequiredService<ILocalDataService>();
                    
                    var currentUser = await localDataService.GetUserByIdAsync(_currentUserId);
                    var senderLogin = currentUser?.Login ?? "Me";

                    var message = new Message { ChatId = _currentChatId, SenderId = _currentUserId, Content = plainText, IsMine = true, Timestamp = DateTime.UtcNow };
                    await localDataService.AddMessageAsync(message);

                    if (!message.Content.StartsWith($"({senderLogin}): "))
                    {
                        message.Content = $"({senderLogin}): {plainText}";
                    }
                    Messages.Add(message);
                    MessageTextBox.Clear();
                    
                    // Auto-scroll to the new message
                    if (ChatHistoryListBox.Items.Count > 0)
                    {
                        ChatHistoryListBox.ScrollIntoView(ChatHistoryListBox.Items[ChatHistoryListBox.Items.Count - 1]);
                    }
                }
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
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var localDataService = scope.ServiceProvider.GetRequiredService<ILocalDataService>();
                            
                            var currentUser = await localDataService.GetUserByIdAsync(_currentUserId);
                            var senderLogin = currentUser?.Login ?? "Me";

                            var message = new Message { ChatId = _currentChatId, SenderId = _currentUserId, Content = messageContent, IsMine = true, Timestamp = DateTime.UtcNow };
                            await localDataService.AddMessageAsync(message);

                            if (!message.Content.StartsWith($"({senderLogin}): "))
                            {
                                message.Content = $"({senderLogin}): {messageContent}";
                            }
                            Messages.Add(message);
                            
                            // Auto-scroll to the new message
                            if (ChatHistoryListBox.Items.Count > 0)
                            {
                                ChatHistoryListBox.ScrollIntoView(ChatHistoryListBox.Items[ChatHistoryListBox.Items.Count - 1]);
                            }
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

        public void Cleanup()
        {
            _cancellationTokenSource?.Cancel();
            _messageRefreshTimer?.Stop();
            
            if (_rabbitMQConsumer != null)
            {
                _rabbitMQConsumer.MessageReceived -= OnRabbitMQMessageReceived;
                _rabbitMQConsumer.Dispose();
            }
        }
    }
}
