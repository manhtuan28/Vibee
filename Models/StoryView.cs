using System.ComponentModel.DataAnnotations.Schema;

namespace Vibee.Models
{
    public class StoryView
    {
        public int Id { get; set; }
        public int StoryId { get; set; }
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
    }
}