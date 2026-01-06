using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        [Route("Profile/{id?}")]
        public async Task<IActionResult> Index(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            string targetUserId = string.IsNullOrEmpty(id) ? currentUser.Id : id;

            var targetUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == targetUserId);

            if (targetUser == null) return NotFound();

            var posts = await _context.Posts
                .Where(p => p.UserId == targetUserId)
                .Include(p => p.User).Include(p => p.Medias).Include(p => p.Likes).Include(p => p.Comments)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var model = new ProfileViewModel
            {
                User = targetUser,
                IsOwner = targetUser.Id == currentUser.Id,
                Posts = posts,
                FriendCount = 1250,
                PhotoCount = 450
            };
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
        public async Task<IActionResult> UpdateInfo(string FullName, string Bio, string Job, string LiveAt, string From)
        {
            var user = await _userManager.GetUserAsync(User);

            user.FullName = FullName;
            user.Bio = Bio;
            user.Job = Job;
            user.LiveAt = LiveAt;
            user.From = From;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Route("Profile/UploadAvatar")]
        [HttpPost]
        public async Task<IActionResult> UploadAvatar(IFormFile file, [FromServices] FileService fileService)
        {
            if (file == null) return Json(new { success = false, message = "Chưa chọn ảnh" });
            try
            {
                var path = await fileService.UploadFileAsync(file);
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
                var path = await fileService.UploadFileAsync(file);
                var user = await _userManager.GetUserAsync(User);
                user.CoverPhotoUrl = path;
                await _context.SaveChangesAsync();
                return Json(new { success = true, newUrl = path });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }
    }
}