using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Models;

namespace Vibee.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class WalletsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WalletsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int page = 1, string type = "all")
        {
            int pageSize = 15;
            var query = _context.Transactions
                .Include(t => t.SenderWallet).ThenInclude(w => w.User)
                .Include(t => t.ReceiverWallet).ThenInclude(w => w.User)
                .AsNoTracking()
                .AsQueryable();

            if (type == "deposit") query = query.Where(t => t.Type == TransactionType.Deposit);
            else if (type == "transfer") query = query.Where(t => t.Type == TransactionType.Transfer);

            query = query.OrderByDescending(t => t.CreatedAt);

            var totalDeposit = await _context.Transactions
                .Where(t => t.Type == TransactionType.Deposit && t.Status == TransactionStatus.Completed)
                .SumAsync(t => t.Amount);

            var totalSystemBalance = await _context.Wallets.SumAsync(w => w.Balance);

            int totalItems = await query.CountAsync();
            var transactions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new WalletAdminViewModel
            {
                Transactions = transactions,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                TotalDeposit = totalDeposit,
                TotalSystemBalance = totalSystemBalance,
                CurrentType = type
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AdjustBalance(string userEmail, decimal amount, string type, string note)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null) return Json(new { success = false, message = "Không tìm thấy Email người dùng này." });

                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == user.Id);
                if (wallet == null) return Json(new { success = false, message = "Người dùng này chưa kích hoạt ví." });

                if (type == "add")
                {
                    wallet.Balance += amount;
                }
                else if (type == "minus")
                {
                    if (wallet.Balance < amount)
                        return Json(new { success = false, message = "Số dư ví không đủ để trừ." });

                    wallet.Balance -= amount;
                }
                else return Json(new { success = false, message = "Loại giao dịch không hợp lệ." });

                wallet.LastUpdated = DateTime.Now;

                var logTrans = new Transaction
                {
                    Id = Guid.NewGuid(),
                    SenderWalletId = null,
                    ReceiverWalletId = wallet.Id,
                    Amount = amount,
                    Type = type == "add" ? TransactionType.Deposit : TransactionType.Transfer,
                    Status = TransactionStatus.Completed,
                    Message = $"SYSTEM: {(type == "add" ? "Cộng tiền" : "Trừ tiền")} - {note}",
                    CreatedAt = DateTime.Now
                };

                _context.Transactions.Add(logTrans);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new { success = true, message = $"Đã {(type == "add" ? "cộng" : "trừ")} {amount:N0}đ thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTransaction(Guid id)
        {
            var trans = await _context.Transactions.FindAsync(id);
            if (trans == null) return Json(new { success = false, message = "Giao dịch không tồn tại." });

            _context.Transactions.Remove(trans);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa lịch sử giao dịch." });
        }
    }



    public class WalletAdminViewModel
    {
        public List<Transaction> Transactions { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public decimal TotalDeposit { get; set; }
        public decimal TotalSystemBalance { get; set; }
        public string CurrentType { get; set; }
    }
}