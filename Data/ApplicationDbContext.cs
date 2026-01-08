using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Vibee.Models;

namespace Vibee.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Post> Posts { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<PostMedia> PostMedias { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Like>()
                    .HasOne(l => l.Post)
                    .WithMany(p => p.Likes)
                    .HasForeignKey(l => l.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Comment>()
                    .HasOne(c => c.ParentComment)
                    .WithMany(c => c.Replies)
                    .HasForeignKey(c => c.ParentCommentId)
                    .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Friendship>()
                .HasKey(k => new { k.SenderId, k.ReceiverId });

            builder.Entity<Friendship>()
                .HasOne(f => f.Sender)
                .WithMany(u => u.SentFriendRequests)
                .HasForeignKey(f => f.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Friendship>(entity =>
            {
                entity.HasKey(f => new { f.SenderId, f.ReceiverId });

                entity.HasOne(f => f.Sender)
                    .WithMany(u => u.SentFriendRequests)
                    .HasForeignKey(f => f.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.Receiver)
                    .WithMany(u => u.ReceivedFriendRequests)
                    .HasForeignKey(f => f.ReceiverId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Transaction>()
                .HasOne(t => t.SenderWallet)
                .WithMany()
                .HasForeignKey(t => t.SenderWalletId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Transaction>()
                .HasOne(t => t.ReceiverWallet)
                .WithMany()
                .HasForeignKey(t => t.ReceiverWalletId)
                .OnDelete(DeleteBehavior.Restrict);

        }
    }
}