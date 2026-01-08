using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vibee.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        public string ReceiverId { get; set; }
        public string SenderId { get; set; }

        public string Content { get; set; }
        public string Link { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int Type { get; set; } // 1: Like, 2: Comment, 3: Share, 4: FriendRequest

        [ForeignKey("SenderId")]
        public virtual ApplicationUser Sender { get; set; }
    }
}