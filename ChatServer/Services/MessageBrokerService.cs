using ChatServer.Models;
using ChatServer.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatServer.Services
{
    public class MessageBrokerService : IMessageBrokerService, IDisposable
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IConnection? _connection;
        private readonly IChannel? _channel;
        private readonly string _exchangeName;
        private readonly string _queuePrefix;
        private bool _disposed = false;

        public MessageBrokerService(IServiceScopeFactory serviceScopeFactory, IConfiguration configuration)
        {
            _serviceScopeFactory = serviceScopeFactory;

            // Read RabbitMQ configuration
            var rabbitConfig = configuration.GetSection("RabbitMQ");
            var factory = new ConnectionFactory()
            {
                HostName = rabbitConfig["Host"] ?? "localhost",
                Port = int.Parse(rabbitConfig["Port"] ?? "5672"),
                UserName = rabbitConfig["Username"] ?? "guest",
                Password = rabbitConfig["Password"] ?? "guest",
                VirtualHost = rabbitConfig["VirtualHost"] ?? "/"
            };

            _exchangeName = rabbitConfig["ExchangeName"] ?? "chat.exchange";
            _queuePrefix = rabbitConfig["QueuePrefix"] ?? "chat.queue";

            try
            {
                // Create connection and channel
                _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

                // Declare exchange
                _channel.ExchangeDeclareAsync(
                    exchange: _exchangeName,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false
                ).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to RabbitMQ: {ex.Message}");
                // Continue without RabbitMQ - will use DB only
            }
        }

        public async Task<Message?> ReceiveMessage(int chatId, long lastDeliveryId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var message = await context.Messages
                                        .Where(m => m.ChatId == chatId && m.Id > lastDeliveryId)
                                        .OrderBy(m => m.Id)
                                        .FirstOrDefaultAsync();
            return message;
        }

        public async Task<List<Message>> GetChatHistory(int chatId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            return await context.Messages
                                 .Where(m => m.ChatId == chatId)
                                 .OrderBy(m => m.Id)
                                 .ToListAsync();
        }

        public async Task SendMessage(Message message)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Message ID will be set by the database
            message.Timestamp = DateTime.UtcNow;
            
            context.Messages.Add(message);
            await context.SaveChangesAsync();

            // After saving, the database will assign an Id. Use this as DeliveryId.
            message.DeliveryId = message.Id;

            // Publish to RabbitMQ if connected
            if (_channel != null && !_disposed)
            {
                try
                {
                    await PublishToRabbitMQ(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to publish to RabbitMQ: {ex.Message}");
                    // Continue - message is already in DB
                }
            }
        }

        private async Task PublishToRabbitMQ(Message message)
        {
            // Get queue name for this chat
            string queueName = $"{_queuePrefix}.{message.ChatId}";
            string routingKey = $"chat.{message.ChatId}";

            // Declare queue if not exists
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

            // Serialize message (only encrypted content)
            var messageDto = new
            {
                message.Id,
                message.ChatId,
                message.SenderId,
                message.Content,
                message.DeliveryId,
                message.Timestamp
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageDto));

            // Publish message to RabbitMQ
            var properties = new BasicProperties
            {
                Persistent = true,
                DeliveryMode = DeliveryModes.Persistent,
                ContentType = "application/json",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _channel.BasicPublishAsync(
                exchange: _exchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body
            );

            Console.WriteLine($"Published message {message.Id} to RabbitMQ queue {queueName}");
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
}
