using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vibee.Models
{
    public class Wallet
    {
        [Key]
        public int Id { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0;

        public string Currency { get; set; } = "VND";

        [StringLength(6)]
        public string? PinCode { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        public string? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
}