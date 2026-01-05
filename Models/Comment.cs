using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vibee.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int PostId { get; set; }
        [ForeignKey("PostId")]
        public virtual Post? Post { get; set; }

        public string? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
}