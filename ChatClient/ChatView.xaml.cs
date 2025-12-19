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
        private WebSocketService? _webSocketService;
        private bool _useRabbitMQ = true;
        private bool _useWebSocket = true;
        private DispatcherTimer _messageRefreshTimer;
        
        // CRITICAL: Semaphore to prevent concurrent message processing
        private readonly SemaphoreSlim _messageProcessingSemaphore = new SemaphoreSlim(1, 1);

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
            
            // Setup automatic message refresh timer (every 1 second) - same pattern as contacts/chats refresh
            _messageRefreshTimer = new DispatcherTimer();
            _messageRefreshTimer.Interval = TimeSpan.FromSeconds(1);
            _messageRefreshTimer.Tick += async (s, e) => await CheckForNewMessages();
            
            // Initialize WebSocket service (but don't connect yet - wait for InitializeChat)
            if (_useWebSocket)
            {
                _webSocketService = new WebSocketService();
                _webSocketService.MessageReceived += OnWebSocketMessageReceived;
            }
            
            // Initialize RabbitMQ consumer (but don't connect yet - wait for InitializeChat)
            if (_useRabbitMQ)
            {
                _rabbitMQConsumer = new RabbitMQConsumerService();
                _rabbitMQConsumer.MessageReceived += OnRabbitMQMessageReceived;
            }
        }

        private async void OnWebSocketMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            // CRITICAL: Use semaphore to ensure only one message is processed at a time
            await _messageProcessingSemaphore.WaitAsync();
            try
            {
                var messageData = e.MessageData;
                
                // CRITICAL FIX: Capture current state at the start to avoid race conditions
                var currentChatId = _currentChatId;
                var currentUserId = _currentUserId;
                
                FileLogger.Log($"[WebSocket] ★★★ Message received: ChatId={messageData.ChatId}, SenderId={messageData.SenderId}, DeliveryId={messageData.DeliveryId}");
                FileLogger.Log($"[WebSocket] Current chat: {currentChatId}, Current user: {currentUserId}, _lastDeliveryId: {_lastDeliveryId}");
                
                // Skip if message is not for this chat
                if (messageData.ChatId != currentChatId)
                {
                    FileLogger.Log($"[WebSocket] Skipping message for different chat: {messageData.ChatId} != {currentChatId}");
                    return;
                }
                
                // Skip if we already processed this message
                if (messageData.DeliveryId <= _lastDeliveryId)
                {
                    FileLogger.Log($"[WebSocket] Skipping duplicate message: {messageData.DeliveryId} <= {_lastDeliveryId}");
                    return;
                }

                // Check if this is our own message
                bool isMyMessage = messageData.SenderId == currentUserId;
                if (isMyMessage)
                {
                    FileLogger.Log($"[WebSocket] Received own message back from server with DeliveryId={messageData.DeliveryId}");
                }

                // Decrypt the content
                string decryptedContent;
                try
                {
                    var encryptedBytes = Convert.FromBase64String(messageData.Content ?? "");
                    var decryptedBytes = _encryptionService.Decrypt(encryptedBytes, _sessionKey, _iv);
                    decryptedContent = Encoding.UTF8.GetString(decryptedBytes);
                    FileLogger.Log($"[WebSocket] Successfully decrypted message");
                }
                catch (Exception decryptEx)
                {
                    FileLogger.Log($"[WebSocket] Decryption failed ({decryptEx.Message}). Message will show as [Encrypted]");
                    decryptedContent = $"[Encrypted message - decryption failed]";
                }

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
                        IsMine = isMyMessage, // Use captured value
                        Timestamp = messageData.Timestamp, 
                        DeliveryId = messageData.DeliveryId 
                    };
                    
                    // Try to save to DB - AddMessageAsync already checks for duplicates
                    try
                    {
                        await localDataService.AddMessageAsync(message);
                        FileLogger.Log($"[WebSocket] Message saved to DB (DeliveryId={messageData.DeliveryId})");
                    }
                    catch (Exception dbEx)
                    {
                        FileLogger.Log($"[WebSocket] Failed to save message to DB: {dbEx.Message}");
                        // Continue to display even if DB save fails
                    }

                    var displayContent = decryptedContent;
                    if (!displayContent.StartsWith($"({senderLogin}): "))
                    {
                        displayContent = $"({senderLogin}): {decryptedContent}";
                    }

                    await Dispatcher.InvokeAsync(() =>
                    {
                        // CRITICAL: Check if message already exists in UI (by DeliveryId)
                        var existingMessage = Messages.FirstOrDefault(m => m.DeliveryId == messageData.DeliveryId);
                        if (existingMessage != null)
                        {
                            FileLogger.Log($"[WebSocket] Message with DeliveryId={messageData.DeliveryId} already in UI, skipping");
                            _lastDeliveryId = messageData.DeliveryId;
                            return;
                        }
                        
                        // CRITICAL FIX: Remove any temporary messages (DeliveryId = 0) for this sender before adding the real message
                        // This prevents duplicates when attaching files/images
                        if (isMyMessage)
                        {
                            var tempMessages = Messages.Where(m => m.DeliveryId == 0 && m.SenderId == messageData.SenderId).ToList();
                            if (tempMessages.Any())
                            {
                                FileLogger.Log($"[WebSocket] Removing {tempMessages.Count} temporary messages before adding real message");
                                foreach (var tempMsg in tempMessages)
                                {
                                    Messages.Remove(tempMsg);
                                }
                            }
                        }
                        
                        FileLogger.Log($"[WebSocket] About to add message to Messages collection (current count: {Messages.Count})");
                        Messages.Add(new Message
                        {
                            ChatId = message.ChatId,
                            SenderId = message.SenderId,
                            Content = displayContent,
                            IsMine = message.IsMine,
                            Timestamp = message.Timestamp,
                            DeliveryId = message.DeliveryId
                        });
                        FileLogger.Log($"[WebSocket] ✓ Message added! New count: {Messages.Count}");
                        _lastDeliveryId = messageData.DeliveryId;
                        
                        FileLogger.Log($"[WebSocket] Message from {senderLogin}: {decryptedContent}");
                        
                        // Auto-scroll to the new message
                        if (ChatHistoryListBox.Items.Count > 0)
                        {
                            FileLogger.Log($"[WebSocket] Scrolling to last item (ListBox count: {ChatHistoryListBox.Items.Count})");
                            ChatHistoryListBox.ScrollIntoView(ChatHistoryListBox.Items[ChatHistoryListBox.Items.Count - 1]);
                        }
                        else
                        {
                            FileLogger.Log($"[WebSocket] WARNING: ChatHistoryListBox.Items is empty!");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[WebSocket] Error processing message: {ex.Message}");
                FileLogger.Log($"[WebSocket] Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // CRITICAL: Always release the semaphore
                _messageProcessingSemaphore.Release();
            }
        }

        private async void OnRabbitMQMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            // CRITICAL: Use semaphore to ensure only one message is processed at a time
            await _messageProcessingSemaphore.WaitAsync();
            try
            {
                var messageData = e.MessageData;
                
                // CRITICAL FIX: Capture current state at the start to avoid race conditions
                var currentChatId = _currentChatId;
                var currentUserId = _currentUserId;
                
                FileLogger.Log($"[RabbitMQ] Message received: ChatId={messageData.ChatId}, SenderId={messageData.SenderId}, DeliveryId={messageData.DeliveryId}");
                FileLogger.Log($"[RabbitMQ] Current chat: {currentChatId}, Current user: {currentUserId}, _lastDeliveryId: {_lastDeliveryId}");
                
                // Skip if message is not for this chat
                if (messageData.ChatId != currentChatId)
                {
                    FileLogger.Log($"[RabbitMQ] Skipping message for different chat: {messageData.ChatId} != {currentChatId}");
                    return;
                }
                
                // Skip if we already processed this message
                if (messageData.DeliveryId <= _lastDeliveryId)
                {
                    FileLogger.Log($"[RabbitMQ] Skipping duplicate message: {messageData.DeliveryId} <= {_lastDeliveryId}");
                    return;
                }

                // Check if this is our own message
                bool isMyMessage = messageData.SenderId == currentUserId;
                if (isMyMessage)
                {
                    FileLogger.Log($"[RabbitMQ] Received own message back from server with DeliveryId={messageData.DeliveryId}");
                }

                // Decrypt the content
                string decryptedContent;
                try
                {
                    var encryptedBytes = Convert.FromBase64String(messageData.Content ?? "");
                    var decryptedBytes = _encryptionService.Decrypt(encryptedBytes, _sessionKey, _iv);
                    decryptedContent = Encoding.UTF8.GetString(decryptedBytes);
                    FileLogger.Log($"[RabbitMQ] Successfully decrypted message");
                }
                catch (Exception decryptEx)
                {
                    FileLogger.Log($"[RabbitMQ] Decryption failed ({decryptEx.Message}). Message will show as [Encrypted]");
                    decryptedContent = $"[Encrypted message - decryption failed]";
                }

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
                        IsMine = isMyMessage, // Use captured value
                        Timestamp = messageData.Timestamp, 
                        DeliveryId = messageData.DeliveryId 
                    };
                    
                    // Try to save to DB - AddMessageAsync already checks for duplicates
                    try
                    {
                        await localDataService.AddMessageAsync(message);
                        FileLogger.Log($"[RabbitMQ] Message saved to DB (DeliveryId={messageData.DeliveryId})");
                    }
                    catch (Exception dbEx)
                    {
                        FileLogger.Log($"[RabbitMQ] Failed to save message to DB: {dbEx.Message}");
                        // Continue to display even if DB save fails
                    }

                    var displayContent = decryptedContent;
                    if (!displayContent.StartsWith($"({senderLogin}): "))
                    {
                        displayContent = $"({senderLogin}): {decryptedContent}";
                    }

                    await Dispatcher.InvokeAsync(() =>
                    {
                        // CRITICAL: Check if message already exists in UI (by DeliveryId)
                        var existingMessage = Messages.FirstOrDefault(m => m.DeliveryId == messageData.DeliveryId);
                        if (existingMessage != null)
                        {
                            FileLogger.Log($"[RabbitMQ] Message with DeliveryId={messageData.DeliveryId} already in UI, skipping");
                            _lastDeliveryId = messageData.DeliveryId;
                            return;
                        }
                        
                        // CRITICAL FIX: Remove any temporary messages (DeliveryId = 0) for this sender before adding the real message
                        // This prevents duplicates when attaching files/images
                        if (isMyMessage)
                        {
                            var tempMessages = Messages.Where(m => m.DeliveryId == 0 && m.SenderId == messageData.SenderId).ToList();
                            if (tempMessages.Any())
                            {
                                FileLogger.Log($"[RabbitMQ] Removing {tempMessages.Count} temporary messages before adding real message");
                                foreach (var tempMsg in tempMessages)
                                {
                                    Messages.Remove(tempMsg);
                                }
                            }
                        }
                        
                        Messages.Add(new Message
                        {
                            ChatId = message.ChatId,
                            SenderId = message.SenderId,
                            Content = displayContent,
                            IsMine = message.IsMine,
                            Timestamp = message.Timestamp,
                            DeliveryId = message.DeliveryId
                        });
                        _lastDeliveryId = messageData.DeliveryId;
                        
                        FileLogger.Log($"[RabbitMQ] Added message from {senderLogin}: {decryptedContent}");
                        
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
                FileLogger.Log($"[RabbitMQ] Error processing message: {ex.Message}");
                FileLogger.Log($"[RabbitMQ] Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // CRITICAL: Always release the semaphore
                _messageProcessingSemaphore.Release();
            }
        }

        public async void InitializeChat(int userId, int chatId, Chat? chat = null)
        {
            FileLogger.Log($"[ChatView] InitializeChat called: userId={userId}, chatId={chatId}, current={_currentChatId}");
            
            // If reopening the same chat with same user, just return - don't reconnect
            if (_currentChatId == chatId && _currentUserId == userId)
            {
                FileLogger.Log($"[ChatView] Chat {chatId} already open for user {userId}, skipping initialization");
                return;
            }
            
            // Always cleanup before initializing a new chat (even if _currentChatId is 0)
            // But only if we're actually switching chats or users
            if (_currentChatId > 0 || _webSocketService != null)
            {
                FileLogger.Log($"[ChatView] Cleaning up before initializing chat {chatId}...");
                
                // Stop timer temporarily
                _messageRefreshTimer?.Stop();
                
                // CRITICAL: Properly disconnect and dispose WebSocket before creating a new one
                if (_webSocketService != null)
                {
                    FileLogger.Log($"[ChatView] Disconnecting WebSocket for chat {_currentChatId}");
                    // Unsubscribe from events first
                    _webSocketService.MessageReceived -= OnWebSocketMessageReceived;
                    // Then disconnect and dispose
                    await _webSocketService.DisconnectAsync();
                    _webSocketService.Dispose();
                    _webSocketService = null;
                    FileLogger.Log($"[ChatView] WebSocket disconnected and disposed");
                }
                
                // Unsubscribe from RabbitMQ
                if (_useRabbitMQ && _rabbitMQConsumer != null && _currentChatId > 0)
                {
                    await _rabbitMQConsumer.UnsubscribeFromChatAsync(_currentChatId);
                }
                
                // Clear messages ALWAYS when switching chats/users to prevent showing old messages
                FileLogger.Log($"[ChatView] Clearing messages (switching from chat {_currentChatId}/user {_currentUserId} to chat {chatId}/user {userId})");
                Messages.Clear();
                _lastDeliveryId = 0;
            }
            
            // Update state BEFORE recreating services
            _currentUserId = userId;
            _currentChatId = chatId;
            
            // Recreate WebSocket service (always create fresh instance for new chat/user)
            if (_useWebSocket)
            {
                FileLogger.Log($"[ChatView] Creating new WebSocket service for chat {chatId}");
                _webSocketService = new WebSocketService();
                _webSocketService.MessageReceived += OnWebSocketMessageReceived;
            }
            
            // Recreate RabbitMQ consumer if needed (after Cleanup or first time)
            if (_useRabbitMQ && _rabbitMQConsumer == null)
            {
                FileLogger.Log($"[ChatView] Creating new RabbitMQ consumer");
                _rabbitMQConsumer = new RabbitMQConsumerService();
                _rabbitMQConsumer.MessageReceived += OnRabbitMQMessageReceived;
            }
            
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

            // CRITICAL FIX: Load history BEFORE connecting to WebSocket/RabbitMQ
            // This ensures _lastDeliveryId is set correctly before any new messages arrive
            await LoadChatHistory(); // Load history first and set _lastDeliveryId
            await EstablishSessionKey(); // Then establish a new session key
            
            FileLogger.Log($"[ChatView] History loaded, _lastDeliveryId={_lastDeliveryId}. Now connecting to real-time services...");
            
            // Try WebSocket first (AFTER loading history)
            if (_useWebSocket && _webSocketService != null)
            {
                try
                {
                    FileLogger.Log($"[ChatView] Attempting WebSocket connection for chat {chatId}...");
                    bool connected = await _webSocketService.ConnectAsync(userId, chatId);
                    if (connected)
                    {
                        FileLogger.Log($"[ChatView] ✓ Connected to WebSocket for chat {chatId}");
                    }
                    else
                    {
                        FileLogger.Log("[ChatView] ✗ Failed to connect to WebSocket, trying RabbitMQ");
                        _useWebSocket = false;
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Log($"[ChatView] ✗ WebSocket initialization failed: {ex.Message}, trying RabbitMQ");
                    _useWebSocket = false;
                }
            }
            else
            {
                FileLogger.Log($"[ChatView] WebSocket disabled (_useWebSocket={_useWebSocket}, _webSocketService={_webSocketService != null})");
            }
            
            // Initialize RabbitMQ connection and subscribe if WebSocket failed (AFTER loading history)
            if (!_useWebSocket && _useRabbitMQ && _rabbitMQConsumer != null)
            {
                try
                {
                    bool connected = await _rabbitMQConsumer.ConnectAsync();
                    if (connected)
                    {
                        await _rabbitMQConsumer.SubscribeToChatAsync(chatId, _cancellationTokenSource.Token);
                        FileLogger.Log($"[ChatView] Connected to RabbitMQ and subscribed to chat {chatId}");
                    }
                    else
                    {
                        FileLogger.Log("[ChatView] Failed to connect to RabbitMQ, falling back to polling");
                        _useRabbitMQ = false;
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Log($"[ChatView] RabbitMQ initialization failed: {ex.Message}, falling back to polling");
                    _useRabbitMQ = false;
                }
            }
            
            // Start automatic message refresh timer - ALWAYS start it (same as contacts/chats)
            _messageRefreshTimer.Start();
        }

        public void CloseChat()
        {
            // Stop timer
            _messageRefreshTimer?.Stop();
            
            // Disconnect WebSocket
            if (_webSocketService != null)
            {
                _ = _webSocketService.DisconnectAsync();
            }
            
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
                    var (serverPublicKey, p, g, encryptedChatKey, encryptedChatIv) = result.Value;
                    
                    FileLogger.Log($"[SessionKey] Received response from server: p={p.ToString().Substring(0, Math.Min(20, p.ToString().Length))}...");
                    
                    _clientDh = new DiffieHellman.DiffieHellman(_clientDh.PrivateKey, p, g);
                    
                    BigInteger sharedSecret = _clientDh.GetSharedSecret(serverPublicKey);
                    
                    byte[] sharedSecretBytes = sharedSecret.ToByteArray();
                    byte[] dhKey;
                    
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        dhKey = sha256.ComputeHash(sharedSecretBytes);
                        FileLogger.Log($"[SessionKey] Derived DH key for decryption");
                    }
                    
                    // Decrypt the chat's shared key using DH-derived key
                    if (encryptedChatKey != null && encryptedChatIv != null)
                    {
                        try
                        {
                            using (Aes aes = Aes.Create())
                            {
                                aes.Key = dhKey;
                                aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                                aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
                                
                                using (var decryptor = aes.CreateDecryptor())
                                {
                                    _sessionKey = decryptor.TransformFinalBlock(encryptedChatKey, 0, encryptedChatKey.Length);
                                    _iv = decryptor.TransformFinalBlock(encryptedChatIv, 0, encryptedChatIv.Length);
                                }
                            }
                            
                            FileLogger.Log($"[SessionKey] ✓ Decrypted shared chat key (key length: {_sessionKey.Length}, IV length: {_iv.Length})");
                        }
                        catch (Exception ex)
                        {
                            FileLogger.Log($"[SessionKey] ✗ Failed to decrypt shared key: {ex.Message}");
                            
                            // Fallback to old method
                            int requiredKeyLength = _encryptionService.RequiredKeySize;
                            int requiredIvLength = _encryptionService.BlockSize;
                            
                            _sessionKey = new byte[requiredKeyLength];
                            Array.Copy(dhKey, 0, _sessionKey, 0, requiredKeyLength);
                            
                            using (SHA256 sha256_iv = SHA256.Create())
                            {
                                byte[] iv_seed = Encoding.UTF8.GetBytes("IV_Seed_For_DH").Concat(sharedSecretBytes).ToArray();
                                _iv = sha256_iv.ComputeHash(iv_seed).Take(requiredIvLength).ToArray();
                            }
                            
                            FileLogger.Log("[SessionKey] Using DH-derived key as fallback");
                        }
                    }
                    else
                    {
                        FileLogger.Log("[SessionKey] No encrypted key received, using DH-derived key");
                        
                        // Fallback to old method
                        int requiredKeyLength = _encryptionService.RequiredKeySize;
                        int requiredIvLength = _encryptionService.BlockSize;
                        
                        _sessionKey = new byte[requiredKeyLength];
                        Array.Copy(dhKey, 0, _sessionKey, 0, requiredKeyLength);
                        
                        using (SHA256 sha256_iv = SHA256.Create())
                        {
                            byte[] iv_seed = Encoding.UTF8.GetBytes("IV_Seed_For_DH").Concat(sharedSecretBytes).ToArray();
                            _iv = sha256_iv.ComputeHash(iv_seed).Take(requiredIvLength).ToArray();
                        }
                    }
                }
                else
                {
                    FileLogger.Log("[SessionKey] ✗ Failed to establish session key: Server response empty.");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[SessionKey] ✗ Error establishing session key: {ex.Message}");
            }
        }

        private async Task LoadChatHistory()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var localDataService = scope.ServiceProvider.GetRequiredService<ILocalDataService>();
                    
                    // Clean up any duplicate messages before loading history
                    await localDataService.CleanupDuplicateMessagesAsync();
                    
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
                        // Clear existing messages to prevent duplication
                        FileLogger.Log($"[LoadChatHistory] Clearing {Messages.Count} existing messages");
                        Messages.Clear();
                        
                        // CRITICAL: Also reset _lastDeliveryId before loading to ensure consistency
                        _lastDeliveryId = 0;
                        
                        FileLogger.Log($"[LoadChatHistory] Loading {historyMessages.Count} messages from DB");
                        foreach (var message in historyMessages)
                        {
                            var senderLogin = senders.TryGetValue(message.SenderId, out var login) ? login : "Unknown";
                            // CRITICAL FIX: Don't modify the original message object from DB
                            // Create content with prefix directly when creating the new Message
                            var displayContent = message.Content;
                            if (!displayContent.StartsWith($"({senderLogin}): "))
                            {
                                displayContent = $"({senderLogin}): {displayContent}";
                            }
                            Messages.Add(new Message
                            {
                                SenderId = message.SenderId,
                                Content = displayContent,
                                IsMine = message.SenderId == _currentUserId,
                                Id = message.Id,
                                DeliveryId = message.DeliveryId
                            });
                            // CRITICAL FIX: Use DeliveryId (server's global ID) instead of Id (local SQLite ID)
                            if (message.DeliveryId > _lastDeliveryId)
                            {
                                _lastDeliveryId = message.DeliveryId;
                            }
                        }
                        
                        FileLogger.Log($"[LoadChatHistory] Loaded {Messages.Count} messages, _lastDeliveryId set to {_lastDeliveryId}");
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
            // Simple polling check - same pattern as contacts/chats refresh
            // CRITICAL: Use semaphore to ensure only one message is processed at a time
            await _messageProcessingSemaphore.WaitAsync();
            try
            {
                // Capture current state at the start to avoid race conditions
                var currentChatId = _currentChatId;
                var currentUserId = _currentUserId;
                
                if (currentChatId <= 0) return;
                
                // Check if session key is established
                if (_sessionKey == null || _sessionKey.Length == 0)
                {
                    return;
                }

                var serverMessage = await _chatApiClient.ReceiveEncryptedFragment(currentChatId, _lastDeliveryId);
                if (serverMessage != null && serverMessage.Content != null)
                {
                    // Skip if we already processed this message
                    if (serverMessage.DeliveryId <= _lastDeliveryId)
                    {
                        return;
                    }
                    
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

                            bool isMyMessage = serverMessage.SenderId == currentUserId;
                            
                            var message = new Message 
                            { 
                                ChatId = currentChatId, 
                                SenderId = serverMessage.SenderId, 
                                Content = decryptedContent, 
                                IsMine = isMyMessage, 
                                Timestamp = serverMessage.Timestamp, 
                                DeliveryId = serverMessage.DeliveryId 
                            };
                            
                            // Try to save to DB - AddMessageAsync already checks for duplicates
                            try
                            {
                                await localDataService.AddMessageAsync(message);
                                FileLogger.Log($"[CheckForNewMessages] Message saved to DB (DeliveryId={serverMessage.DeliveryId})");
                            }
                            catch (Exception dbEx)
                            {
                                FileLogger.Log($"[CheckForNewMessages] Failed to save message to DB: {dbEx.Message}");
                                // Continue to display even if DB save fails
                            }

                            var displayContent = decryptedContent;
                            if (!displayContent.StartsWith($"({senderLogin}): "))
                            {
                                displayContent = $"({senderLogin}): {decryptedContent}";
                            }

                            await Dispatcher.InvokeAsync(() =>
                            {
                                // CRITICAL: Check if message already exists in UI (by DeliveryId)
                                var existingMessage = Messages.FirstOrDefault(m => m.DeliveryId == serverMessage.DeliveryId);
                                if (existingMessage != null)
                                {
                                    FileLogger.Log($"[CheckForNewMessages] Message with DeliveryId={serverMessage.DeliveryId} already in UI, skipping");
                                    _lastDeliveryId = serverMessage.DeliveryId;
                                    return;
                                }
                                
                                // CRITICAL FIX: Remove any temporary messages (DeliveryId = 0) for this sender before adding the real message
                                // This prevents duplicates when attaching files/images
                                if (isMyMessage)
                                {
                                    var tempMessages = Messages.Where(m => m.DeliveryId == 0 && m.SenderId == serverMessage.SenderId).ToList();
                                    if (tempMessages.Any())
                                    {
                                        FileLogger.Log($"[CheckForNewMessages] Removing {tempMessages.Count} temporary messages before adding real message");
                                        foreach (var tempMsg in tempMessages)
                                        {
                                            Messages.Remove(tempMsg);
                                        }
                                    }
                                }
                                
                                Messages.Add(new Message
                                {
                                    ChatId = message.ChatId,
                                    SenderId = message.SenderId,
                                    Content = displayContent,
                                    IsMine = message.IsMine,
                                    Timestamp = message.Timestamp,
                                    DeliveryId = message.DeliveryId
                                });
                                _lastDeliveryId = serverMessage.DeliveryId;
                                
                                FileLogger.Log($"[CheckForNewMessages] Added message from {senderLogin}: {decryptedContent}");
                                
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
                        FileLogger.Log($"[CheckForNewMessages] Error decrypting message: {ex.Message}");
                        if(serverMessage != null)
                        {
                            _lastDeliveryId = serverMessage.DeliveryId; // Still update delivery ID to not get stuck
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[CheckForNewMessages] Error: {ex.Message}");
            }
            finally
            {
                // CRITICAL: Always release the semaphore
                _messageProcessingSemaphore.Release();
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text)) return;

            var plainText = MessageTextBox.Text;
            MessageTextBox.Clear(); // Clear immediately for better UX
            
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = _encryptionService.Encrypt(plainTextBytes, _sessionKey, _iv);
            var encryptedContent = Convert.ToBase64String(encryptedBytes);

            FileLogger.Log($"[SendButton] Sending message to chat {_currentChatId}");
            var success = await _chatApiClient.SendEncryptedFragment(_currentChatId, _currentUserId, encryptedContent);
            
            if (success)
            {
                FileLogger.Log($"[SendButton] Message sent successfully, waiting for server echo via WebSocket");
                // CRITICAL FIX: Don't add any temporary message to UI
                // The message will appear when it comes back from server via WebSocket/RabbitMQ/polling
                // This completely eliminates the duplicate message problem
            }
            else
            {
                FileLogger.Log($"[SendButton] Failed to send message");
                MessageBox.Show("Failed to send message.");
                // Restore the text if send failed
                MessageTextBox.Text = plainText;
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
                        FileLogger.Log($"[AttachFile] File sent successfully, waiting for server echo via WebSocket");
                        // CRITICAL FIX: Don't add any temporary message to UI for images/files
                        // The message will appear when it comes back from server via WebSocket/RabbitMQ/polling
                        // This completely eliminates the duplicate message problem (same as text messages)
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

        public async Task CleanupAsync()
        {
            FileLogger.Log($"[ChatView] CleanupAsync called for chat {_currentChatId}");
            
            // Stop timers first
            _messageRefreshTimer?.Stop();
            
            // Cancel any ongoing operations
            _cancellationTokenSource?.Cancel();
            
            // Properly disconnect and cleanup WebSocket
            if (_webSocketService != null)
            {
                FileLogger.Log($"[ChatView] Cleaning up WebSocket service...");
                _webSocketService.MessageReceived -= OnWebSocketMessageReceived;
                
                // Disconnect gracefully before disposing
                try
                {
                    await _webSocketService.DisconnectAsync();
                    FileLogger.Log($"[ChatView] WebSocket disconnected successfully");
                }
                catch (Exception ex)
                {
                    FileLogger.Log($"[ChatView] Error disconnecting WebSocket: {ex.Message}");
                }
                
                _webSocketService.Dispose();
                _webSocketService = null;
                FileLogger.Log($"[ChatView] WebSocket disposed and set to null");
            }
            
            // Cleanup RabbitMQ
            if (_rabbitMQConsumer != null)
            {
                FileLogger.Log($"[ChatView] Cleaning up RabbitMQ consumer...");
                _rabbitMQConsumer.MessageReceived -= OnRabbitMQMessageReceived;
                _rabbitMQConsumer.Dispose();
                _rabbitMQConsumer = null;
                FileLogger.Log($"[ChatView] RabbitMQ disposed and set to null");
            }
            
            // Recreate cancellation token source for next initialization
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // IMPORTANT: Reset state to allow re-initialization
            FileLogger.Log($"[ChatView] Resetting state: clearing {Messages.Count} messages, resetting _currentChatId from {_currentChatId} to 0");
            Messages.Clear();
            _lastDeliveryId = 0;
            _currentChatId = 0;
            _currentUserId = 0;
            
            // Hide chat UI
            RootGrid.Visibility = Visibility.Collapsed;
            EmptyStateTextBlock.Visibility = Visibility.Visible;
        }
    }
}
