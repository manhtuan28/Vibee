using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Models;
using Vibee.Services;

namespace Vibee.Controllers
{
    [Authorize]
    public class MessageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EncryptionService _encryptionService;

        public MessageController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, EncryptionService encryptionService)
        {
            _context = context;
            _userManager = userManager;
            _encryptionService = encryptionService;
        }

        [HttpGet]
        [Route("Message/GetConversation/{otherUserId}")]
        public async Task<IActionResult> GetConversation(string otherUserId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var messages = await _context.Messages
                .Include(m => m.ReplyToMessage)
                .ThenInclude(r => r.Sender)
                .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == otherUserId) ||
                            (m.SenderId == otherUserId && m.ReceiverId == currentUser.Id))
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            var result = messages.Select(m => {
                string contentShow = "";

                if (m.Type == "Call")
                {
                    contentShow = m.Content;
                }
                else if (m.IsDeletedForEveryone)
                {
                    contentShow = "Tin nhắn đã bị thu hồi";
                }
                else
                {
                    try
                    {
                        contentShow = _encryptionService.Decrypt(m.Content);

                        if (m.Type == "Image" || m.Type == "Video")
                        {
                            contentShow = contentShow.Replace("\\", "/");
                        }
                    }
                    catch
                    {
                        contentShow = m.Content;
                    }
                }

                object replyData = null;
                if (m.ReplyToMessage != null)
                {
                    string replyContent = "";
                    try
                    {
                        if (m.ReplyToMessage.Type == "Call")
                            replyContent = "Cuộc gọi";
                        else
                        {
                            string decrypted = _encryptionService.Decrypt(m.ReplyToMessage.Content);
                            if (m.ReplyToMessage.Type == "Text") replyContent = decrypted;
                            else replyContent = m.ReplyToMessage.Type == "Image" ? "[Hình ảnh]" : "[Video]";
                        }
                    }
                    catch { replyContent = "..."; }

                    replyData = new
                    {
                        Content = replyContent,
                        SenderName = m.ReplyToMessage.Sender != null ? m.ReplyToMessage.Sender.FullName : "Người dùng",
                        SenderId = m.ReplyToMessage.SenderId
                    };
                }

                return new
                {
                    m.Id,
                    m.SenderId,
                    m.ReceiverId,
                    Content = contentShow,
                    CreatedAt = m.CreatedAt,
                    IsMine = m.SenderId == currentUser.Id,
                    Type = m.Type,
                    ReplyTo = replyData
                };
            });

            return Json(new { success = true, data = result });
        }
    }
}