using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Models;

namespace Vibee.Controllers
{
    [Authorize]
    public class FriendsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public FriendsController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> AddFriend(string targetId)
        {
            var sender = await _userManager.GetUserAsync(User);
            if (sender.Id == targetId) return Json(new { success = false, message = "Không thể kết bạn với chính mình" });

            var exists = await _context.Friendships.AnyAsync(f =>
                (f.SenderId == sender.Id && f.ReceiverId == targetId) ||
                (f.SenderId == targetId && f.ReceiverId == sender.Id));

            if (exists) return Json(new { success = false, message = "Đã gửi lời mời hoặc đã là bạn bè." });

            var friendship = new Friendship
            {
                SenderId = sender.Id,
                ReceiverId = targetId,
                Status = FriendshipStatus.Pending,
                ActionDate = DateTime.Now
            };

            _context.Friendships.Add(friendship);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> CancelRequest(string targetId)
        {
            var sender = await _userManager.GetUserAsync(User);

            var request = await _context.Friendships.FirstOrDefaultAsync(f =>
                f.SenderId == sender.Id && f.ReceiverId == targetId && f.Status == FriendshipStatus.Pending);

            if (request == null) return Json(new { success = false, message = "Không tìm thấy lời mời." });

            _context.Friendships.Remove(request);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> AcceptRequest(string targetId)
        {
            var receiver = await _userManager.GetUserAsync(User);

            var request = await _context.Friendships.FirstOrDefaultAsync(f =>
                f.SenderId == targetId && f.ReceiverId == receiver.Id && f.Status == FriendshipStatus.Pending);

            if (request == null) return Json(new { success = false, message = "Yêu cầu không tồn tại." });

            request.Status = FriendshipStatus.Accepted;
            request.ActionDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Unfriend(string targetId)
        {
            var me = await _userManager.GetUserAsync(User);

            var friendship = await _context.Friendships.FirstOrDefaultAsync(f =>
                (f.SenderId == me.Id && f.ReceiverId == targetId) ||
                (f.SenderId == targetId && f.ReceiverId == me.Id));

            if (friendship == null) return Json(new { success = false });

            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}