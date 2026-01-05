using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vibee.Models
{
    public class Like
    {
        public int PostId { get; set; }
        public virtual Post? Post { get; set; }

        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}