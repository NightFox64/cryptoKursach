using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using ChatClient.Models; // For User model
using ChatClient.Shared.Models; // For Chat, Contact, File, Message models

namespace ChatClient.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Message> Messages { get; set; } // Client-side message storage
        public DbSet<Chat> Chats { get; set; } // Local chat storage
        public DbSet<File> Files { get; set; } // Local file metadata storage

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=chatclient_db;Username=postgres;Password=Ichiho64");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure composite primary key for Contact
            modelBuilder.Entity<Contact>()
                .HasKey(c => new { c.UserId, c.ContactUserId }); // Use ContactUserId as defined in client Contact model

            // Configure Message to Chat relationship
            modelBuilder.Entity<Message>()
                .HasOne<Chat>() // A message belongs to one Chat
                .WithMany() // A Chat can have many Messages
                .HasForeignKey(m => m.ChatId); // Foreign key is ChatId in Message

            // Configure File to Message relationship
            modelBuilder.Entity<File>()
                .HasOne<Message>() // A File belongs to one Message
                .WithMany(m => m.Files) // A Message can have many Files
                .HasForeignKey(f => f.MessageId); // Foreign key is MessageId in File
        }
    }
}
