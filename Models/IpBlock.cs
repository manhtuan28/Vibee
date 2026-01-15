using System.ComponentModel.DataAnnotations;

namespace Vibee.Models
{
    public class IpBlock
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string IpAddress { get; set; }

        public string Type { get; set; }

        public DateTime BlockedAt { get; set; }

        public DateTime UnlockTime { get; set; }

        public string Reason { get; set; }
    }
}