using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vibee.Models
{
    public enum MediaType { Image, Video }

    public class PostMedia
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public MediaType Type { get; set; }

        public int PostId { get; set; }
        [ForeignKey("PostId")]
        public virtual Post Post { get; set; }
    }
}