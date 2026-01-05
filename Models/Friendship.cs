using System.ComponentModel.DataAnnotations.Schema;

namespace Vibee.Models
{
    public enum FriendshipStatus
    {
        Pending,
        Accepted,
        Blocked
    }

    public class Friendship
    {
        public string? SenderId { get; set; }
        [ForeignKey("SenderId")]
        public virtual ApplicationUser? Sender { get; set; }

        public string? ReceiverId { get; set; }
        [ForeignKey("ReceiverId")]
        public virtual ApplicationUser? Receiver { get; set; }

        public FriendshipStatus Status { get; set; }
        public DateTime ActionDate { get; set; } = DateTime.Now;
    }
}