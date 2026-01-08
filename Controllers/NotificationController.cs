using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Models;

namespace Vibee.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications(string filter = "all", int skip = 0, int take = 5)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                var query = _context.Notifications.AsNoTracking().Where(n => n.ReceiverId == user.Id);

                if (filter == "unread") query = query.Where(n => !n.IsRead);

                int total = await query.CountAsync();

                var rawNotis = await query
                    .Include(n => n.Sender)
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                var data = rawNotis.Select(n => new
                {
                    id = n.Id,
                    avatar = (n.Sender != null && !string.IsNullOrEmpty(n.Sender.AvatarUrl))
                             ? n.Sender.AvatarUrl
                             : "/images/default-avatar.png",

                    senderName = n.Sender?.FullName ?? "Người dùng",

                    actionText = n.Content,

                    link = n.Link,
                    isRead = n.IsRead,
                    time = CalculateTimeAgo(n.CreatedAt),
                    type = n.Type
                }).ToList();

                var unreadCount = await _context.Notifications.CountAsync(n => n.ReceiverId == user.Id && !n.IsRead);

                return Json(new { success = true, data = data, total = total, unreadCount = unreadCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            var n = await _context.Notifications.FindAsync(id);
            if (n != null) { n.IsRead = true; await _context.SaveChangesAsync(); }
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var user = await _userManager.GetUserAsync(User);
            await _context.Notifications
                .Where(n => n.ReceiverId == user.Id && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

            return Json(new { success = true });
        }

        private string CalculateTimeAgo(DateTime d)
        {
            var span = DateTime.Now - d;
            if (span.TotalMinutes < 1) return "Vừa xong";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays} ngày trước";
            return d.ToString("dd/MM/yyyy");
        }
    }
}