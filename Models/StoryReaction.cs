using System.ComponentModel.DataAnnotations.Schema;

namespace Vibee.Models
{
    public class StoryReaction
    {
        public int Id { get; set; }
        public int StoryId { get; set; }
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
        public string Type { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}