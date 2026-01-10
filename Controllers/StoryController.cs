using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Models;
using Vibee.Services;

namespace Vibee.Controllers
{
    [Authorize]
    public class StoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public StoryController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> GetStories()
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var now = DateTime.UtcNow;

            var stories = await _context.Stories
                .Include(s => s.User)
                .Where(s => s.ExpiresAt > now && !s.IsDeleted)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var storyIds = stories.Select(s => s.Id).ToList();

            var allReactions = await _context.StoryReactions
                .Where(r => storyIds.Contains(r.StoryId))
                .Include(r => r.User)
                .ToListAsync();

            var allViews = await _context.StoryViews
                .Where(v => storyIds.Contains(v.StoryId))
                .Include(v => v.User)
                .ToListAsync();

            var groupedStories = stories.GroupBy(s => s.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    UserName = g.First().User.FullName,
                    UserAvatar = g.First().User.AvatarUrl ?? "/images/default-avatar.png",
                    IsMe = g.Key == currentUserId,
                    Items = g.Select(s => {
                        var views = allViews.Where(v => v.StoryId == s.Id).OrderByDescending(v => v.ViewedAt).ToList();
                        var reactions = allReactions.Where(r => r.StoryId == s.Id).OrderByDescending(r => r.CreatedAt).ToList();

                        return new
                        {
                            s.Id,
                            s.MediaUrl,
                            s.Type,
                            Time = s.CreatedAt,

                            ViewCount = views.Count,
                            Viewers = (g.Key == currentUserId) ? views.Select(v => new {
                                UserId = v.UserId,
                                UserName = v.User.FullName,
                                Avatar = v.User.AvatarUrl ?? "/images/default-avatar.png",
                                Time = v.ViewedAt
                            }).ToList() : null,

                            ReactionCount = reactions.Count,
                            TopReactions = reactions.Take(5).Select(r => new {
                                Type = r.Type,
                                UserName = r.User.FullName
                            }).ToList()
                        };
                    }).ToList()
                })
                .OrderByDescending(g => g.IsMe)
                .ToList();

            return Json(new { success = true, data = groupedStories });
        }

        [HttpPost]
        public async Task<IActionResult> Create(IFormFile file)
        {
            if (file == null || file.Length == 0) return Json(new { success = false, message = "Chưa chọn file" });
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var path = Path.Combine(_env.WebRootPath, "uploads", "stories");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            using (var stream = new FileStream(Path.Combine(path, fileName), FileMode.Create)) { await file.CopyToAsync(stream); }

            var type = file.ContentType.StartsWith("video") ? "Video" : "Image";
            var story = new Story { UserId = userId, MediaUrl = "/uploads/stories/" + fileName, Type = type };

            _context.Stories.Add(story);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var story = await _context.Stories.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
            if (story == null) return Json(new { success = false, message = "Lỗi" });
            story.IsDeleted = true;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> React(int storyId, string type)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var existingReaction = await _context.StoryReactions
                .FirstOrDefaultAsync(r => r.StoryId == storyId && r.UserId == userId);

            if (existingReaction != null)
            {
                existingReaction.Type = type;
                existingReaction.CreatedAt = DateTime.UtcNow;
                _context.StoryReactions.Update(existingReaction);
            }
            else
            {
                var reaction = new StoryReaction
                {
                    StoryId = storyId,
                    UserId = userId,
                    Type = type,
                    CreatedAt = DateTime.UtcNow
                };
                _context.StoryReactions.Add(reaction);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ViewStory(int id)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var exists = await _context.StoryViews.AnyAsync(v => v.StoryId == id && v.UserId == userId);
            if (!exists)
            {
                var view = new StoryView { StoryId = id, UserId = userId, ViewedAt = DateTime.UtcNow };
                _context.StoryViews.Add(view);
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }
    }
}