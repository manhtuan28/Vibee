using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vibee.Models
{
    public class Message
    {
        public int Id { get; set; }

        [Required]
        public string SenderId { get; set; }
        [ForeignKey("SenderId")]
        public virtual ApplicationUser Sender { get; set; }

        [Required]
        public string ReceiverId { get; set; }
        [ForeignKey("ReceiverId")]
        public virtual ApplicationUser Receiver { get; set; }

        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        public string Type { get; set; } = "Text";
        public bool IsDeletedForEveryone { get; set; } = false;
        public bool DeletedBySender { get; set; } = false;
        public bool DeletedByReceiver { get; set; } = false;

        public int? ReplyToMessageId { get; set; }
        [ForeignKey("ReplyToMessageId")]
        public virtual Message? ReplyToMessage { get; set; }
    }
}