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

                // If there's an old websocket, dispose it first
                if (_webSocket != null)
                {
                    Log($"[WebSocket] Disposing old websocket (State: {_webSocket.State})");
                    _webSocket.Dispose();
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
            var buffer = new byte[1024 * 4];

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
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Log($"[WebSocket] Received: {json}");

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
                                Log($"[WebSocket] Invoking MessageReceived event");
                                MessageReceived?.Invoke(this, new MessageReceivedEventArgs(messageData));
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"[WebSocket] Failed to deserialize message: {ex.Message}");
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
                
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    _cancellationTokenSource?.Cancel();
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
                    Log("[WebSocket] Disconnected gracefully");
                }
                else
                {
                    Log($"[WebSocket] Not disconnecting (State: {_webSocket?.State})");
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
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _webSocket?.Dispose();
                _disposed = true;
                Log("[WebSocket] WebSocketService disposed");
            }
        }
    }

}
