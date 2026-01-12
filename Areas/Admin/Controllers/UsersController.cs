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
                    JoinDate = user.JoinDate
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
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            return Json(new
            {
                success = true,
                data = new
                {
                    id = user.Id,
                    fullName = user.FullName,
                    email = user.Email,
                    avatar = user.AvatarUrl ?? "/images/default-avatar.png",
                    joinDate = user.JoinDate.ToString("dd/MM/yyyy HH:mm"),
                    roles = roles,
                    bio = user.Bio ?? "Chưa có giới thiệu",
                    isLocked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                JoinDate = DateTime.Now,
                AvatarUrl = "/images/default-avatar.png",
                Bio = "Thành viên mới"
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);
                return Json(new { success = true, message = "Tạo thành viên thành công!" });
            }

            var errorMsg = string.Join(", ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, message = errorMsg });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return Json(new { success = false, message = "Không tìm thấy user." });

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, model.Role);

            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (!result.Succeeded) return Json(new { success = false, message = "Lỗi đổi mật khẩu." });
            }

            await _userManager.UpdateAsync(user);
            return Json(new { success = true, message = "Cập nhật thành công!" });
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
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null) return Json(new { success = false, message = "User không tồn tại." });

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                    return Json(new { success = false, message = "Không thể xóa Admin hệ thống." });

                var stories = _context.Stories.Where(s => s.UserId == id);
                _context.Stories.RemoveRange(stories);
                var storyReactions = _context.StoryReactions.Where(r => r.UserId == id);
                _context.StoryReactions.RemoveRange(storyReactions);
                var storyViews = _context.StoryViews.Where(v => v.UserId == id);
                _context.StoryViews.RemoveRange(storyViews);

                var posts = _context.Posts.Where(p => p.UserId == id);
                _context.Posts.RemoveRange(posts);
                var comments = _context.Comments.Where(c => c.UserId == id);
                _context.Comments.RemoveRange(comments);
                var likes = _context.Likes.Where(l => l.UserId == id);
                _context.Likes.RemoveRange(likes);

                await _context.SaveChangesAsync();

                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded) throw new Exception("Lỗi xóa Identity.");

                await transaction.CommitAsync();
                return Json(new { success = true, message = "Đã xóa user và toàn bộ dữ liệu liên quan!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
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
        public DateTime JoinDate { get; set; }
    }

    public class CreateUserViewModel
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }

    public class EditUserViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string NewPassword { get; set; }
    }
}