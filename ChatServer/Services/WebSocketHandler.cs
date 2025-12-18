using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChatServer.Services
{
    public class WebSocketHandler
    {
        // Store connections per chat: chatId -> list of WebSocket connections
        private static readonly ConcurrentDictionary<int, ConcurrentBag<WebSocket>> _chatConnections = new();
        
        // Store user to socket mapping for cleanup
        private static readonly ConcurrentDictionary<WebSocket, int> _socketToUser = new();

        public async Task HandleWebSocketAsync(WebSocket webSocket, int userId, int chatId)
        {
            FileLogger.Log($"[WebSocket] New connection: UserId={userId}, ChatId={chatId}");
            
            // Add connection to the chat's connection list
            var connections = _chatConnections.GetOrAdd(chatId, _ => new ConcurrentBag<WebSocket>());
            connections.Add(webSocket);
            _socketToUser[webSocket] = userId;

            var buffer = new byte[1024 * 4];
            
            try
            {
                // Keep the connection alive and listen for messages
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        FileLogger.Log($"[WebSocket] Close requested by client: UserId={userId}, ChatId={chatId}");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                        break;
                    }
                    
                    // Echo back or handle ping/pong
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        FileLogger.Log($"[WebSocket] Received: {message}");
                        
                        // Simple ping/pong
                        if (message.Trim() == "ping")
                        {
                            await SendMessageAsync(webSocket, "pong");
                        }
                    }
                }
            }
            catch (WebSocketException ex)
            {
                FileLogger.Log($"[WebSocket] WebSocket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[WebSocket] Unexpected error: {ex.Message}");
            }
            finally
            {
                // Clean up the connection
                FileLogger.Log($"[WebSocket] Removing connection: UserId={userId}, ChatId={chatId}");
                RemoveConnection(chatId, webSocket);
                _socketToUser.TryRemove(webSocket, out _);
                
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                }
                webSocket.Dispose();
            }
        }

        public static async Task NotifyNewMessageAsync(int chatId, object messageData)
        {
            if (!_chatConnections.TryGetValue(chatId, out var connections))
            {
                FileLogger.Log($"[WebSocket] No connections for chat {chatId}");
                return;
            }

            var json = JsonSerializer.Serialize(messageData);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            FileLogger.Log($"[WebSocket] Broadcasting to chat {chatId}: {json}");

            var tasks = new List<Task>();
            var deadSockets = new List<WebSocket>();

            foreach (var socket in connections)
            {
                if (socket.State == WebSocketState.Open)
                {
                    tasks.Add(SendMessageAsync(socket, json));
                }
                else
                {
                    deadSockets.Add(socket);
                }
            }

            // Remove dead connections
            foreach (var deadSocket in deadSockets)
            {
                RemoveConnection(chatId, deadSocket);
                _socketToUser.TryRemove(deadSocket, out _);
            }

            await Task.WhenAll(tasks);
            FileLogger.Log($"[WebSocket] Broadcast complete for chat {chatId}");
        }

        private static async Task SendMessageAsync(WebSocket socket, string message)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(
                    new ArraySegment<byte>(bytes), 
                    WebSocketMessageType.Text, 
                    true, 
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[WebSocket] Error sending message: {ex.Message}");
            }
        }

        private static void RemoveConnection(int chatId, WebSocket socket)
        {
            if (_chatConnections.TryGetValue(chatId, out var connections))
            {
                // ConcurrentBag doesn't have Remove, so we need to rebuild it
                var newBag = new ConcurrentBag<WebSocket>();
                foreach (var s in connections)
                {
                    if (s != socket)
                    {
                        newBag.Add(s);
                    }
                }
                _chatConnections[chatId] = newBag;
            }
        }
    }
}
