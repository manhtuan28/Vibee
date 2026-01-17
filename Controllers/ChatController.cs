using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Models;
using Vibee.Services;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace Vibee.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly EncryptionService _encryptionService;

        public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, EncryptionService encryptionService)
        {
            _context = context;
            _userManager = userManager;
            _encryptionService = encryptionService;
        }
        public async Task<IActionResult> Index(string? userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            ViewData["CurrentUser"] = currentUser;

            if (!string.IsNullOrEmpty(userId))
            {
                var targetUser = await _context.Users.FindAsync(userId);
                if (targetUser != null)
                {
                    ViewBag.TargetUser = targetUser;
                }
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSidebarUsers()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var userId = currentUser.Id;

            var allMessages = await _context.Messages
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            var conversations = allMessages
                .GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
                .Select(g => new
                {
                    UserId = g.Key,
                    LastMessage = g.First(),
                    UnreadCount = g.Count(m => m.ReceiverId == userId && !m.IsRead)
                })
                .OrderByDescending(x => x.LastMessage.CreatedAt)
                .ToList();

            var result = new List<object>();

            foreach (var item in conversations)
            {
                var friend = await _context.Users.FindAsync(item.UserId);
                if (friend == null) continue;

                string preview = "Đã gửi một tệp đính kèm";

                if (item.LastMessage.IsDeletedForEveryone)
                {
                    preview = "Tin nhắn đã bị thu hồi";
                }
                else
                {
                    bool isDeletedByMe = (item.LastMessage.SenderId == userId && item.LastMessage.DeletedBySender) ||
                                         (item.LastMessage.ReceiverId == userId && item.LastMessage.DeletedByReceiver);

                    if (isDeletedByMe)
                    {
                        preview = "";
                    }
                    else if (item.LastMessage.Type == "Text")
                    {
                        try
                        {
                            preview = _encryptionService.Decrypt(item.LastMessage.Content);
                        }
                        catch
                        {
                            preview = "Tin nhắn lỗi";
                        }
                    }
                    else if (item.LastMessage.Type == "Image") preview = "Đã gửi một ảnh";
                    else if (item.LastMessage.Type == "Video") preview = "Đã gửi một video";
                }

                result.Add(new
                {
                    friend.Id,
                    friend.FullName,
                    AvatarUrl = friend.AvatarUrl ?? "/images/default-avatar.png",
                    friend.IsOnline,
                    LastMessage = preview,
                    LastTime = item.LastMessage.CreatedAt,
                    UnreadCount = item.UnreadCount,
                    IsRead = (item.LastMessage.SenderId == userId) || item.LastMessage.IsRead
                });
            }

            return Json(new { success = true, data = result });
        }

        [HttpPost]
        public async Task<IActionResult> UploadChatFile(IFormFile file, [FromServices] FileService fileService)
        {
            if (file == null) return Json(new { success = false });
            try
            {
                var path = await fileService.UploadFileAsync(file, "chat-media");
                string type = file.ContentType.StartsWith("image") ? "Image" : "Video";
                return Json(new { success = true, url = path, type = type });
            }
            catch { return Json(new { success = false }); }
        }

        [HttpGet]
        public IActionResult GetIceServers()
        {
            const string accountSid = "ACa59b0bf8046725c046890e874554597f";
            const string authToken = "86cbc69481e963f7fde29f62354cc490";

            try
            {
                TwilioClient.Init(accountSid, authToken);

                var token = TokenResource.Create();

                return Json(new { success = true, iceServers = token.IceServers });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}