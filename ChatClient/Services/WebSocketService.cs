using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static ChatClient.Services.FileLogger;

namespace ChatClient.Services
{
    public class WebSocketService : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _disposed = false;
        private readonly string _serverUrl;

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

        public WebSocketService(string serverUrl = "ws://localhost:5280")
        {
            _serverUrl = serverUrl;
        }

        public async Task<bool> ConnectAsync(int userId, int chatId)
        {
            try
            {
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    Log($"[WebSocket] Already connected (State: {_webSocket.State})");
                    return true;
                }

                // If there's an old websocket, properly close and dispose it first
                if (_webSocket != null)
                {
                    Log($"[WebSocket] Cleaning up old websocket (State: {_webSocket.State})");
                    
                    // Cancel any ongoing receive operations
                    if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                    {
                        _cancellationTokenSource.Cancel();
                    }
                    
                    // Try to close gracefully if still open
                    if (_webSocket.State == WebSocketState.Open)
                    {
                        try
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
                            Log($"[WebSocket] Old connection closed gracefully");
                        }
                        catch (Exception ex)
                        {
                            Log($"[WebSocket] Error closing old connection: {ex.Message}");
                        }
                    }
                    
                    _webSocket.Dispose();
                    _cancellationTokenSource?.Dispose();
                    Log($"[WebSocket] Old resources disposed");
                }

                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                var uri = new Uri($"{_serverUrl}/ws/chat/{chatId}?userId={userId}");
                Log($"[WebSocket] Connecting to {uri}");

                await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
                Log($"[WebSocket] Connected successfully (State: {_webSocket.State})");

                // Start receiving messages in background
                _ = ReceiveLoop(_cancellationTokenSource.Token);

                return true;
            }
            catch (Exception ex)
            {
                Log($"[WebSocket] Connection failed: {ex.Message}");
                Log($"[WebSocket] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 64]; // Увеличим буфер для больших сообщений
            var messageBuilder = new StringBuilder();

            try
            {
                while (_webSocket != null && 
                       _webSocket.State == WebSocketState.Open && 
                       !cancellationToken.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Log("[WebSocket] Server closed connection");
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Append the received chunk to the message builder
                        var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        messageBuilder.Append(chunk);

                        // Check if this is the end of the message
                        if (result.EndOfMessage)
                        {
                            var json = messageBuilder.ToString();
                            messageBuilder.Clear();

                            // Skip pong messages
                            if (json.Trim() == "pong")
                            {
                                continue;
                            }

                            try
                            {
                                var messageData = JsonSerializer.Deserialize<MessageData>(json);
                                if (messageData != null)
                                {
                                    Log($"[WebSocket] Received complete message (length: {json.Length}), invoking event handler");
                                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs(messageData));
                                    Log($"[WebSocket] Event handler invocation completed");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"[WebSocket] Failed to deserialize message: {ex.Message}");
                                Log($"[WebSocket] Message preview: {json.Substring(0, Math.Min(100, json.Length))}...");
                            }
                        }
                        else
                        {
                            Log($"[WebSocket] Received fragment ({result.Count} bytes), waiting for more...");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log("[WebSocket] Receive loop cancelled");
            }
            catch (Exception ex)
            {
                Log($"[WebSocket] Receive loop error: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                Log($"[WebSocket] DisconnectAsync called (State: {_webSocket?.State})");
                
                // Cancel ongoing operations first
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                    Log("[WebSocket] Cancelled ongoing operations");
                }
                
                // Then close the connection if it's open
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
                        Log("[WebSocket] Disconnected gracefully");
                    }
                    catch (Exception closeEx)
                    {
                        Log($"[WebSocket] Error during close: {closeEx.Message}");
                    }
                }
                else
                {
                    Log($"[WebSocket] Not closing socket (State: {_webSocket?.State})");
                }
            }
            catch (Exception ex)
            {
                Log($"[WebSocket] Disconnect error: {ex.Message}");
            }
        }

        public async Task SendPingAsync()
        {
            try
            {
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes("ping");
                    await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Log($"[WebSocket] Ping error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Log("[WebSocket] Dispose called - attempting graceful close");
                
                // Cancel ongoing operations first
                _cancellationTokenSource?.Cancel();
                
                // Try to close gracefully if still open
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        // Use Wait() since Dispose can't be async
                        _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disposing", CancellationToken.None)
                            .ConfigureAwait(false)
                            .GetAwaiter()
                            .GetResult();
                        Log("[WebSocket] Closed gracefully in Dispose");
                    }
                    catch (Exception ex)
                    {
                        Log($"[WebSocket] Error closing in Dispose: {ex.Message}");
                    }
                }
                
                // Now dispose resources
                _webSocket?.Dispose();
                _cancellationTokenSource?.Dispose();
                _disposed = true;
                Log("[WebSocket] WebSocketService disposed");
            }
        }
    }

}
