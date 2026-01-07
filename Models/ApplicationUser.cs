using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Vibee.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(100)]
        public string? FullName { get; set; }

        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; } = "/images/default-avatar.png";
        public string? CoverPhotoUrl { get; set; }
        public string? Job { get; set; }
        public string? LiveAt { get; set; }
        public string? From { get; set; }

        public DateTime JoinDate { get; set; } = DateTime.Now;
        public bool IsVerified { get; set; } = false;
        [StringLength(30)]
        public string? UrlHandle { get; set; }

        public virtual Wallet? Wallet { get; set; }
        public virtual ICollection<Post>? Posts { get; set; }

        public virtual ICollection<Friendship>? SentFriendRequests { get; set; }
        public virtual ICollection<Friendship>? ReceivedFriendRequests { get; set; }
    }
}