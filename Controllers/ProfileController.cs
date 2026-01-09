using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Vibee.Data;
using Vibee.Models;
using Vibee.Services;
using Vibee.ViewModels;

namespace Vibee.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ProfileController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }
        private async Task<ProfileViewModel> LoadProfileBase(string idOrHandle, string currentUserId)
        {
            ApplicationUser targetUser = null;
            var currentUser = await _userManager.GetUserAsync(User);

            if (!string.IsNullOrEmpty(idOrHandle))
            {
                targetUser = await _context.Users.FirstOrDefaultAsync(u => u.UrlHandle == idOrHandle);
            }

            if (targetUser == null)
            {
                string searchId = string.IsNullOrEmpty(idOrHandle) ? currentUserId : idOrHandle;
                targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == searchId);
            }

            if (targetUser == null) return null;

            string targetUserId = targetUser.Id;

            string status = "None";
            if (currentUserId == targetUserId) status = "Self";
            else
            {
                var f = await _context.Friendships.FirstOrDefaultAsync(x =>
                    (x.SenderId == currentUserId && x.ReceiverId == targetUserId) ||
                    (x.SenderId == targetUserId && x.ReceiverId == currentUserId));

                if (f != null)
                {
                    if (f.Status == FriendshipStatus.Accepted) status = "Friends";
                    else if (f.Status == FriendshipStatus.Pending) status = (f.SenderId == currentUserId) ? "PendingSent" : "PendingReceived";
                }
            }

            int friendCount = await _context.Friendships.CountAsync(f =>
                (f.SenderId == targetUserId || f.ReceiverId == targetUserId) && f.Status == FriendshipStatus.Accepted);

            int photoCount = await _context.PostMedias.CountAsync(m => m.Post.UserId == targetUserId);

            var friends = await _context.Friendships
                .Where(f => (f.SenderId == targetUserId || f.ReceiverId == targetUserId) && f.Status == FriendshipStatus.Accepted)
                .Include(f => f.Sender).Include(f => f.Receiver)
                .Select(f => f.SenderId == targetUserId ? f.Receiver : f.Sender)
                .Take(6)
                .ToListAsync();

            return new ProfileViewModel
            {
                User = targetUser,
                CurrentUser = currentUser,
                IsOwner = currentUserId == targetUserId,
                FriendshipStatus = status,
                FriendCount = friendCount,
                PhotoCount = photoCount,
                Friends = friends
            };
        }

        [Route("Profile/{id?}")]
        public async Task<IActionResult> Index(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var model = await LoadProfileBase(id, currentUser.Id);
            if (model == null) return NotFound();

            var targetUser = await _context.Users
                            .FirstOrDefaultAsync(u => u.Id == id || u.UrlHandle == id);

            if (targetUser == null) return NotFound();

            bool isOwner = currentUser != null && targetUser.Id == currentUser.Id;

            var query = _context.Posts
                            .Include(p => p.User)
                            .Include(p => p.Likes)
                            .Include(p => p.Comments)
                            .Include(p => p.Medias)
                            .Include(p => p.SharedPost).ThenInclude(sp => sp.User)
                            .Include(p => p.SharedPost).ThenInclude(sp => sp.Medias)
                            .Where(p => p.UserId == targetUser.Id);

            if (!isOwner)
            {
                bool isFriend = false;
                if (currentUser != null)
                {
                    isFriend = await _context.Friendships.AnyAsync(f =>
                       (f.SenderId == currentUser.Id && f.ReceiverId == targetUser.Id && f.Status == FriendshipStatus.Accepted) ||
                       (f.ReceiverId == currentUser.Id && f.SenderId == targetUser.Id && f.Status == FriendshipStatus.Accepted)
                   );
                }

                if (isFriend)
                {
                    query = query.Where(p => p.PrivacyLevel != 2);
                }
                else
                {
                    query = query.Where(p => p.PrivacyLevel == 0);
                }
            }

            model.Posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.SidebarFriends = model.Friends;

            return View(model);
        }

        [Route("Profile/About/{id?}")]
        public async Task<IActionResult> About(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var model = await LoadProfileBase(id, currentUser.Id);
            if (model == null) return NotFound();
            return View(model);
        }

        [Route("Profile/Friends/{id?}")]
        public async Task<IActionResult> Friends(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var model = await LoadProfileBase(id, currentUser.Id);
            if (model == null) return NotFound();

            string targetUserId = model.User.Id;
            var allFriends = await _context.Friendships
                .Where(f => (f.SenderId == targetUserId || f.ReceiverId == targetUserId) && f.Status == FriendshipStatus.Accepted)
                .Include(f => f.Sender).Include(f => f.Receiver)
                .Select(f => f.SenderId == targetUserId ? f.Receiver : f.Sender)
                .ToListAsync();

            ViewBag.FriendsList = allFriends;
            return View(model);
        }

        [Route("Profile/Photos/{id?}")]
        public async Task<IActionResult> Photos(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var model = await LoadProfileBase(id, currentUser.Id);
            if (model == null) return NotFound();

            string targetUserId = model.User.Id;
            var photoQuery = _context.PostMedias
                .Include(m => m.Post)
                .Where(m => m.Post.UserId == targetUserId && m.Type == MediaType.Image)
                .OrderByDescending(m => m.Id)
                .AsQueryable();

            if (!model.IsOwner)
            {
                if (model.FriendshipStatus == "Friends")
                    photoQuery = photoQuery.Where(m => m.Post.PrivacyLevel == 0 || m.Post.PrivacyLevel == 1);
                else
                    photoQuery = photoQuery.Where(m => m.Post.PrivacyLevel == 0);
            }

            ViewBag.PhotosList = await photoQuery.ToListAsync();
            return View(model);
        }

        [Route("Profile/Edit")]
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }

        [Route("Profile/UpdateInfo")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInfo(string FullName, string Bio, string Job, string LiveAt, string From, string UrlHandle, string Gender) // 1. Thêm tham số Gender
        {
            var user = await _userManager.GetUserAsync(User);

            user.FullName = FullName;
            user.Bio = Bio;
            user.Job = Job;
            user.LiveAt = LiveAt;
            user.From = From;
            user.Gender = Gender;

            if (!string.IsNullOrEmpty(UrlHandle))
            {
                if (user.UrlHandle != UrlHandle)
                {
                    if (!Regex.IsMatch(UrlHandle, "^[a-z0-9.]+$"))
                    {
                        return Content("Lỗi: URL chỉ được chứa chữ thường, số và dấu chấm.");
                    }

                    var exists = await _context.Users.AnyAsync(u => u.UrlHandle == UrlHandle && u.Id != user.Id);
                    if (exists)
                    {
                        return Content("Lỗi: Tên đường dẫn này đã có người sử dụng.");
                    }

                    user.UrlHandle = UrlHandle;
                }
            }

            await _context.SaveChangesAsync();

            return Redirect($"/Profile/{(user.UrlHandle ?? user.Id)}");
        }

        [Route("Profile/UploadAvatar")]
        [HttpPost]
        public async Task<IActionResult> UploadAvatar(IFormFile file, [FromServices] FileService fileService)
        {
            if (file == null) return Json(new { success = false, message = "Chưa chọn ảnh" });
            try
            {
                var path = await fileService.UploadFileAsync(file, "avatars");

                var user = await _userManager.GetUserAsync(User);
                user.AvatarUrl = path;
                await _context.SaveChangesAsync();
                return Json(new { success = true, newUrl = path });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [Route("Profile/UploadCover")]
        [HttpPost]
        public async Task<IActionResult> UploadCover(IFormFile file, [FromServices] FileService fileService)
        {
            if (file == null) return Json(new { success = false, message = "Chưa chọn ảnh" });
            try
            {
                var path = await fileService.UploadFileAsync(file, "cover-photo");

                var user = await _userManager.GetUserAsync(User);
                user.CoverPhotoUrl = path;
                await _context.SaveChangesAsync();
                return Json(new { success = true, newUrl = path });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

    }
}