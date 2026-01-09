using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Models;
using Vibee.Services;

namespace Vibee.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly EncryptionService _encryptionService;

        public ChatHub(ApplicationDbContext context, EncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.IsOnline = true;
                    user.LastActive = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.IsOnline = false;
                    user.LastActive = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string receiverId, string content, string type = "Text", int? replyToId = null)
        {
            var senderId = Context.UserIdentifier;
            var encryptedContent = _encryptionService.Encrypt(content);

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = encryptedContent,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                Type = type,
                ReplyToMessageId = replyToId
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            object replyData = null;
            if (replyToId != null)
            {
                var originalMsg = await _context.Messages.Include(m => m.Sender).FirstOrDefaultAsync(m => m.Id == replyToId);
                if (originalMsg != null)
                {
                    string decryptedOriginal = "";
                    if (originalMsg.Type == "Text") decryptedOriginal = _encryptionService.Decrypt(originalMsg.Content);
                    else decryptedOriginal = originalMsg.Type == "Image" ? "[Hình ảnh]" : "[Video]";

                    replyData = new
                    {
                        Content = decryptedOriginal,
                        Type = originalMsg.Type,
                        SenderName = originalMsg.Sender.FullName
                    };
                }
            }

            await Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, content, message.CreatedAt, message.Id, type, replyData);
            await Clients.Caller.SendAsync("MessageSent", receiverId, content, message.CreatedAt, message.Id, type, replyData);
        }
        public async Task MarkAsRead(string senderId)
        {
            var receiverId = Context.UserIdentifier;

            var messages = await _context.Messages
                .Where(m => m.SenderId == senderId && m.ReceiverId == receiverId && !m.IsRead)
                .ToListAsync();

            if (messages.Any())
            {
                foreach (var msg in messages)
                {
                    msg.IsRead = true;
                    msg.ReadAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();

                await Clients.User(senderId).SendAsync("MessagesRead", receiverId);
            }
        }
        public async Task DeleteMessage(int messageId, string mode)
        {
            var userId = Context.UserIdentifier;
            var message = await _context.Messages.FindAsync(messageId);

            if (message == null) return;

            if (mode == "everyone")
            {
                if (message.SenderId == userId)
                {
                    message.IsDeletedForEveryone = true;
                    await _context.SaveChangesAsync();

                    await Clients.User(message.ReceiverId).SendAsync("MessageDeleted", messageId, "everyone");
                    await Clients.Caller.SendAsync("MessageDeleted", messageId, "everyone");
                }
            }
            else
            {
                if (message.SenderId == userId) message.DeletedBySender = true;
                else if (message.ReceiverId == userId) message.DeletedByReceiver = true;

                await _context.SaveChangesAsync();
                await Clients.Caller.SendAsync("MessageDeleted", messageId, "me");
            }
        }
    }
}