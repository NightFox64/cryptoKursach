using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
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
                Console.WriteLine($"Failed to connect to RabbitMQ: {ex.Message}");
                return false;
            }
        }

        public async Task SubscribeToChatAsync(int chatId, CancellationToken cancellationToken = default)
        {
            if (_channel == null)
            {
                throw new InvalidOperationException("Not connected to RabbitMQ. Call ConnectAsync first.");
            }

            string queueName = $"{_queuePrefix}.{chatId}";
            string routingKey = $"chat.{chatId}";

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

            // Create consumer
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    
                    var messageData = JsonSerializer.Deserialize<MessageData>(message);
                    if (messageData != null)
                    {
                        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(messageData));
                    }

                    // Acknowledge the message
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message: {ex.Message}");
                    // Reject the message and requeue
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }

                await Task.CompletedTask;
            };

            // Start consuming
            await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer
            );
        }

        public async Task UnsubscribeFromChatAsync(int chatId)
        {
            if (_channel == null) return;

            string queueName = $"{_queuePrefix}.{chatId}";
            
            try
            {
                // Delete queue when unsubscribing
                await _channel.QueueDeleteAsync(queueName, false, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to unsubscribe from chat {chatId}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _channel?.Dispose();
                _connection?.Dispose();
                _disposed = true;
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
