using Microsoft.EntityFrameworkCore;
using ChatClient.Models; // Assuming ChatClient.Models has the entity definitions

namespace ChatClient.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Message> Messages { get; set; } // Client-side message storage

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("Data Source=chatclient.db");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure composite primary key for Contact
            modelBuilder.Entity<Contact>()
                .HasKey(c => new { c.UserId, c.ContactUserId });
        }
    }
}
