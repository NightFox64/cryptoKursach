using System;
using System.Collections.Generic;

namespace ChatClient.Shared.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int SenderId { get; set; }
        public string? Content { get; set; }
        public long DeliveryId { get; set; }
        public DateTime Timestamp { get; set; }

        public List<File> Files { get; set; } = new List<File>();

        // Property for UI to determine if the message was sent by the current user
        public bool IsMine { get; set; }
    }
}
