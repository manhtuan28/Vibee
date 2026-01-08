using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Models;
using Vibee.ViewModels;
using Vibee.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Vibee.Controllers
{
    [Authorize]
    public class FriendsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public FriendsController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        private async Task CreateNotification(string receiverId, string senderId, string content, string link, int type)
        {
            if (receiverId == senderId) return;

            var noti = new Notification
            {
                ReceiverId = receiverId,
                SenderId = senderId,
                Content = content,
                Link = link,
                Type = type,
                CreatedAt = DateTime.Now,
                IsRead = false
            };
            _context.Add(noti);
            await _context.SaveChangesAsync();

            var sender = await _context.Users.FindAsync(senderId);

            await _hubContext.Clients.User(receiverId).SendAsync("ReceiveNotification", new
            {
                avatar = sender.AvatarUrl ?? "/images/default-avatar.png",
                content = $"<strong>{sender.FullName}</strong> {content}",
                link = link,
                time = "Vừa xong"
            });
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var allFriendships = await _context.Friendships
                .Where(f => f.SenderId == currentUser.Id || f.ReceiverId == currentUser.Id)
                .ToListAsync();

            var requestSenderIds = allFriendships
                .Where(f => f.ReceiverId == currentUser.Id && f.Status == FriendshipStatus.Pending)
                .Select(f => f.SenderId).ToList();
            var friendRequests = await _context.Users.Where(u => requestSenderIds.Contains(u.Id)).ToListAsync();

            var sentReceiverIds = allFriendships
                .Where(f => f.SenderId == currentUser.Id && f.Status == FriendshipStatus.Pending)
                .Select(f => f.ReceiverId).ToList();
            var sentRequests = await _context.Users.Where(u => sentReceiverIds.Contains(u.Id)).ToListAsync();

            var friendIds = allFriendships
                .Where(f => f.Status == FriendshipStatus.Accepted)
                .Select(f => f.SenderId == currentUser.Id ? f.ReceiverId : f.SenderId).ToList();
            var friends = await _context.Users.Where(u => friendIds.Contains(u.Id)).ToListAsync();

            var suggestions = await _context.Users
                .Where(u => u.Id != currentUser.Id && !friendIds.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var model = new FriendsPageViewModel
            {
                CurrentUser = currentUser,
                FriendRequests = friendRequests,
                SentRequests = sentRequests,
                Friends = friends,
                Suggestions = suggestions,
                SentRequestIds = sentReceiverIds
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddFriend(string targetId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
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
            await CreateNotification(targetId, currentUser.Id, "đã gửi cho bạn lời mời kết bạn.", "/Friends", 4);

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