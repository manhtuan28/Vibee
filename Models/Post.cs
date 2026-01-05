using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vibee.Models
{
    public class Post
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string? Content { get; set; }

        public string? MediaUrl { get; set; }
        public string? MediaType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int PrivacyLevel { get; set; } = 0;

        public string? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        public virtual ICollection<Comment>? Comments { get; set; }
        public virtual ICollection<Like>? Likes { get; set; }
    }
}