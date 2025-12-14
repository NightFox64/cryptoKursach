using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; // Required for configuration
using ChatServer.Models; // Using the server-specific models like User and SessionKey
using System.Collections.Generic; // Added for List
using System.Text.Json; // Added for JsonSerializer
using Microsoft.EntityFrameworkCore.Storage.ValueConversion; // Added for ValueConverter

namespace ChatServer.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IConfiguration _configuration;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IConfiguration configuration) : base(options)
        {
            _configuration = configuration;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<SessionKey> SessionKeys { get; set; } // Add DbSet for SessionKey
        public DbSet<Chat> Chats { get; set; } // Added DbSet for Chat

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Fallback connection string (e.g., for migrations outside of WebHost context)
                optionsBuilder.UseSqlite(_configuration.GetConnectionString("DefaultConnection") ?? "Data Source=chatserver.db");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure User entity
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Login)
                .IsUnique();

            // Configure SessionKey entity
            modelBuilder.Entity<SessionKey>()
                .HasKey(sk => sk.Id);

            // Configure Chat entity
            modelBuilder.Entity<Chat>()
                .HasKey(c => c.Id);

            // Configure ValueConverter for List<int> UserIds to string
            var listConverter = new ValueConverter<List<int>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<int>>(v, (JsonSerializerOptions?)null) ?? new List<int>());

            modelBuilder.Entity<Chat>()
                .Property(c => c.UserIds)
                .HasConversion(listConverter);

            // Configure other server-side entities as needed
        }
    }
}