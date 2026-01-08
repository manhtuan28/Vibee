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
        public DateTime? UpdatedAt { get; set; }
        public int PrivacyLevel { get; set; } = 0;

        public string? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public virtual ICollection<Like> Likes { get; set; } = new List<Like>();

        public string? LocationName { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? IpAddress { get; set; }
        public string? Emotion { get; set; }

        public virtual ICollection<PostMedia> Medias { get; set; } = new List<PostMedia>();


        public int? SharedPostId { get; set; }
        [ForeignKey("SharedPostId")]
        public virtual Post? SharedPost { get; set; }

        [InverseProperty("SharedPost")]
        public virtual ICollection<Post> Shares { get; set; } = new List<Post>();
    }
}