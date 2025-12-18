using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services
{
    public class RabbitMQConsumerService : IDisposable
    {
        private IConnection? _connection;
        private IChannel? _channel;
        private bool _disposed = false;
        private readonly string _exchangeName = "chat.exchange";
        private readonly string _queuePrefix = "chat.queue";
        private readonly Dictionary<int, string> _consumerTags = new Dictionary<int, string>();
        private readonly Dictionary<int, AsyncEventingBasicConsumer> _consumers = new Dictionary<int, AsyncEventingBasicConsumer>();

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

        public async Task<bool> ConnectAsync(string host = "localhost", int port = 5672, 
            string username = "admin", string password = "admin123")
        {
            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = host,
                    Port = port,
                    UserName = username,
                    Password = password,
                    VirtualHost = "/"
                };

                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();

                // Declare exchange
                await _channel.ExchangeDeclareAsync(
                    exchange: _exchangeName,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false
                );

                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Log($"Failed to connect to RabbitMQ: {ex.Message}");
                return false;
            }
        }

        public async Task SubscribeToChatAsync(int chatId, CancellationToken cancellationToken = default)
        {
            if (_channel == null)
            {
                throw new InvalidOperationException("Not connected to RabbitMQ. Call ConnectAsync first.");
            }

            // If already subscribed, unsubscribe first
            if (_consumerTags.ContainsKey(chatId))
            {
                FileLogger.Log($"Already subscribed to chat {chatId}, unsubscribing first...");
                await UnsubscribeFromChatAsync(chatId);
            }

            string queueName = $"{_queuePrefix}.{chatId}";
            string routingKey = $"chat.{chatId}";

            FileLogger.Log($"[RabbitMQ] Declaring queue: {queueName}");
            
            // Declare queue
            await _channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // Bind queue to exchange
            await _channel.QueueBindAsync(
                queue: queueName,
                exchange: _exchangeName,
                routingKey: routingKey
            );

            FileLogger.Log($"[RabbitMQ] Queue bound to exchange with routing key: {routingKey}");

            // Create consumer and store it to prevent GC
            var consumer = new AsyncEventingBasicConsumer(_channel);
            _consumers[chatId] = consumer;
            
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    FileLogger.Log($"[RabbitMQ] Message received from queue {queueName}");
                    
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    
                    FileLogger.Log($"[RabbitMQ] Raw message: {message}");
                    
                    var messageData = JsonSerializer.Deserialize<MessageData>(message);
                    if (messageData != null)
                    {
                        FileLogger.Log($"[RabbitMQ] Invoking MessageReceived event for chat {messageData.ChatId}");
                        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(messageData));
                    }

                    // Acknowledge the message
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                    FileLogger.Log($"[RabbitMQ] Message acknowledged");
                }
                catch (Exception ex)
                {
                    FileLogger.Log($"[RabbitMQ] Error processing message: {ex.Message}");
                    FileLogger.Log($"[RabbitMQ] Stack trace: {ex.StackTrace}");
                    // Reject the message and requeue
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            // Start consuming and store the consumer tag
            var consumerTag = await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer
            );
            
            _consumerTags[chatId] = consumerTag;
            FileLogger.Log($"[RabbitMQ] Consumer started with tag: {consumerTag}");
        }

        public async Task UnsubscribeFromChatAsync(int chatId)
        {
            if (_channel == null) return;

            try
            {
                // Cancel consumer if exists
                if (_consumerTags.TryGetValue(chatId, out var consumerTag))
                {
                    await _channel.BasicCancelAsync(consumerTag);
                    _consumerTags.Remove(chatId);
                    FileLogger.Log($"[RabbitMQ] Consumer {consumerTag} cancelled for chat {chatId}");
                }

                // Remove consumer reference
                if (_consumers.ContainsKey(chatId))
                {
                    _consumers.Remove(chatId);
                }

                // Note: We don't delete the queue anymore, just unsubscribe
                // This allows messages to accumulate if client reconnects
                FileLogger.Log($"[RabbitMQ] Unsubscribed from chat {chatId}");
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[RabbitMQ] Failed to unsubscribe from chat {chatId}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _consumers.Clear();
                _consumerTags.Clear();
                _channel?.Dispose();
                _connection?.Dispose();
                _disposed = true;
                FileLogger.Log("[RabbitMQ] RabbitMQConsumerService disposed");
            }
        }
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public MessageData MessageData { get; }

        public MessageReceivedEventArgs(MessageData messageData)
        {
            MessageData = messageData;
        }
    }

    public class MessageData
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int SenderId { get; set; }
        public string? Content { get; set; }
        public long DeliveryId { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
