using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vibee.Models
{
    public enum TransactionType
    {
        Deposit,
        Withdraw,
        Transfer
    }

    public enum TransactionStatus
    {
        Pending,
        Completed,
        Failed
    }

    public class Transaction
    {
        [Key]
        public Guid Id { get; set; }

        public int? SenderWalletId { get; set; }
        public int? ReceiverWalletId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public string? Message { get; set; }
        public TransactionType Type { get; set; }
        public TransactionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("SenderWalletId")]
        public virtual Wallet? SenderWallet { get; set; }

        [ForeignKey("ReceiverWalletId")]
        public virtual Wallet? ReceiverWallet { get; set; }
    }
}