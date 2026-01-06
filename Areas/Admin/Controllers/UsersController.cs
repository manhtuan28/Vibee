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
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index(string keyword, int page = 1)
        {
            int pageSize = 10;
            var query = _context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(u => u.Email.ToLower().Contains(keyword)
                                      || (u.FullName != null && u.FullName.ToLower().Contains(keyword)));
            }

            int totalUsers = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.JoinDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userList = new List<UserViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName ?? "Chưa đặt tên",
                    Email = user.Email,
                    AvatarUrl = user.AvatarUrl ?? "/images/default-avatar.png",
                    Roles = roles.ToList(),
                    IsLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow,
                    WalletBalance = user.Wallet?.Balance ?? 0
                });
            }

            var model = new UserListViewModel
            {
                Users = userList,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalUsers / (double)pageSize),
                Keyword = keyword
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserDetails(string id)
        {
            var user = await _context.Users
                .Include(u => u.Wallet)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            return Json(new
            {
                success = true,
                data = new
                {
                    fullName = user.FullName,
                    email = user.Email,
                    avatar = user.AvatarUrl ?? "/images/default-avatar.png",
                    joinDate = user.JoinDate.ToString("dd/MM/yyyy HH:mm"),
                    roles = string.Join(", ", roles),
                    balance = user.Wallet?.Balance.ToString("N0") + " " + user.Wallet?.Currency,
                    bio = user.Bio ?? "Chưa có giới thiệu",
                    isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return Json(new { success = false, message = "Không tìm thấy user." });

            var currentUser = await _userManager.GetUserAsync(User);
            if (user.Id == currentUser.Id) return Json(new { success = false, message = "Không thể tự khóa chính mình!" });

            bool isLockedNow;
            if (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                user.LockoutEnd = null;
                isLockedNow = false;
            }
            else
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                isLockedNow = true;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, isLocked = isLockedNow, message = isLockedNow ? "Đã khóa tài khoản!" : "Đã mở khóa!" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return Json(new { success = false, message = "User không tồn tại." });

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return Json(new { success = false, message = "Không thể xóa Admin." });
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
                return Json(new { success = true, message = "Đã xóa thành công!" });

            return Json(new { success = false, message = "Lỗi khi xóa user." });
        }
    }
    public class UserListViewModel
    {
        public List<UserViewModel> Users { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string Keyword { get; set; }
    }

    public class UserViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string AvatarUrl { get; set; }
        public List<string> Roles { get; set; }
        public bool IsLocked { get; set; }
        public decimal WalletBalance { get; set; }
    }
}