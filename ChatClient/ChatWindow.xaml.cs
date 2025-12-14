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
        private int _currentUserId;
        private int _currentChatId;
        private long _lastDeliveryId = 0;

        private byte[] _sessionKey = new byte[32]; // Will be set by DH
        private byte[] _iv = new byte[16]; // Will be set by DH

        private DiffieHellman.DiffieHellman? _clientDh; // Client's DH instance

        private CancellationTokenSource _cancellationTokenSource;

        public ObservableCollection<Message> Messages { get; set; }

        public ChatWindow(IChatApiClient chatApiClient, IEncryptionService encryptionService)
        {
            InitializeComponent();
            _chatApiClient = chatApiClient;
            _encryptionService = encryptionService;

            Messages = new ObservableCollection<Message>();
            ChatHistoryListBox.ItemsSource = Messages;
            
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => ReceiveMessagesLoop(_cancellationTokenSource.Token));
        }

        public void InitializeChat(int userId, int chatId)
        {
            _currentUserId = userId;
            _currentChatId = chatId;
            Title = $"Chat with User {_currentUserId} in Chat {_currentChatId}";
            _ = EstablishSessionKey(); // Establish key before loading history
            _ = LoadChatHistory(); // Load history when chat is initialized
        }

        private async Task EstablishSessionKey()
        {
            try
            {
                // 1. Generate client's DH keys
                // For simplicity, using 512 bits for now. A larger size like 1024 or 2048 is better.
                _clientDh = new DiffieHellman.DiffieHellman(512); 
                BigInteger clientPublicKey = _clientDh.PublicKey;

                // 2. Request Session Key from server
                var result = await _chatApiClient.RequestSessionKey(_currentChatId, _currentUserId, clientPublicKey);

                if (result.HasValue)
                {
                    var (serverPublicKey, p, g) = result.Value;

                    // Reconstruct client's DH instance with shared P and G from server
                    _clientDh = new DiffieHellman.DiffieHellman(_clientDh.PrivateKey, p, g);

                    // 3. Derive shared secret
                    BigInteger sharedSecret = _clientDh.GetSharedSecret(serverPublicKey);

                    // 4. Derive symmetric key and IV from shared secret (matching server's logic)
                    byte[] sharedSecretBytes = sharedSecret.ToByteArray();
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] hashedSecret = sha256.ComputeHash(sharedSecretBytes);

                        // Symmetric Key: first 32 bytes (256 bits for AES-256)
                        _sessionKey = new byte[32];
                        Array.Copy(hashedSecret, 0, _sessionKey, 0, 32);

                        // IV: Derive IV by hashing shared secret again with a different context
                        using (SHA256 sha256_iv = SHA256.Create())
                        {
                            byte[] iv_seed = Encoding.UTF8.GetBytes("IV_Seed_For_DH").Concat(sharedSecretBytes).ToArray();
                            _iv = sha256_iv.ComputeHash(iv_seed).Take(16).ToArray(); // AES IV is 16 bytes
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
                var historyMessages = await _chatApiClient.GetChatHistory(_currentChatId);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Messages.Clear(); // Clear existing messages
                    foreach (var message in historyMessages)
                    {
                        var decryptedContent = DecryptMessageContent(message); // Helper for decryption
                        Messages.Add(new Message
                        {
                            SenderId = message.SenderId,
                            Content = decryptedContent,
                            IsMine = message.SenderId == _currentUserId,
                            Id = message.Id // Use actual message ID from server
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

        private string DecryptMessageContent(Message message)
        {
            // This is largely duplicated from ReceiveMessagesLoop, consider refactoring
            var encryptedContent = message.Content;
            string decryptedContent = string.Empty;

            if (string.IsNullOrEmpty(encryptedContent)) return string.Empty;

            try
            {
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
            }
            catch (Exception ex)
            {
                // Handle decryption errors, e.g., if key is wrong or content is malformed
                decryptedContent = $"[Decryption Error: {ex.Message}]";
            }
            return decryptedContent;
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
                Messages.Add(new Message { SenderId = _currentUserId, Content = MessageTextBox.Text, IsMine = true, Id = (int)DateTimeOffset.Now.ToUnixTimeMilliseconds() });
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
                        string fileName = Path.GetFileName(openFileDialog.FileName);
                        if (fileName.EndsWith(".jpg") || fileName.EndsWith(".png") || fileName.EndsWith(".gif"))
                        {
                            Messages.Add(new Message { SenderId = _currentUserId, Content = "[IMAGE]" + encryptedContent, IsMine = true, Id = (int)DateTimeOffset.Now.ToUnixTimeMilliseconds() });
                        }
                        else
                        {
                            Messages.Add(new Message { SenderId = _currentUserId, Content = "[FILE]" + fileName + "|" + encryptedContent, IsMine = true, Id = (int)DateTimeOffset.Now.ToUnixTimeMilliseconds() });
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
                        if (serverMessage != null && serverMessage.Content != null)
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
                                Messages.Add(new Message { SenderId = serverMessage.SenderId, Content = decryptedContent, IsMine = false, Id = (int)DateTimeOffset.Now.ToUnixTimeMilliseconds() });
                                _lastDeliveryId = serverMessage.DeliveryId;
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Error receiving messages: {ex.Message}");
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
