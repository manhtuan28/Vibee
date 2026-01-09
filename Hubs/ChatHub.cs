using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Models;
using Vibee.Services;
using System.Collections.Concurrent;

namespace Vibee.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly EncryptionService _encryptionService;
        public static ConcurrentDictionary<string, List<string>> OnlineUsers = new ConcurrentDictionary<string, List<string>>();
        public static ConcurrentDictionary<string, DateTime> CallStartTimes = new ConcurrentDictionary<string, DateTime>();
        public static ConcurrentDictionary<string, string> ActiveCalls = new ConcurrentDictionary<string, string>();

        public ChatHub(ApplicationDbContext context, EncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }
        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            var connectionId = Context.ConnectionId;

            if (!string.IsNullOrEmpty(userId))
            {
                OnlineUsers.AddOrUpdate(userId,
                    new List<string> { connectionId },
                    (key, list) =>
                    {
                        lock (list) { list.Add(connectionId); }
                        return list;
                    });

                var user = await _context.Users.FindAsync(userId);
                if (user != null && !user.IsOnline)
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
            var connectionId = Context.ConnectionId;

            if (!string.IsNullOrEmpty(userId))
            {
                if (OnlineUsers.TryGetValue(userId, out var list))
                {
                    lock (list) { list.Remove(connectionId); }
                    if (list.Count == 0)
                    {
                        OnlineUsers.TryRemove(userId, out _);
                        var user = await _context.Users.FindAsync(userId);
                        if (user != null) { user.IsOnline = false; user.LastActive = DateTime.UtcNow; await _context.SaveChangesAsync(); }
                    }
                }

                if (ActiveCalls.TryRemove(userId, out string partnerId))
                {
                    ActiveCalls.TryRemove(partnerId, out _);

                    if (OnlineUsers.TryGetValue(partnerId, out var partnerConnections))
                    {
                        await Clients.Clients(partnerConnections).SendAsync("CallEnded");
                    }
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
                    try
                    {
                        if (originalMsg.Type == "Text") decryptedOriginal = _encryptionService.Decrypt(originalMsg.Content);
                        else decryptedOriginal = originalMsg.Type == "Image" ? "[Hình ảnh]" : "[Video]";
                    }
                    catch { decryptedOriginal = "..."; }

                    replyData = new { Content = decryptedOriginal, Type = originalMsg.Type, SenderName = originalMsg.Sender.FullName, SenderId = originalMsg.SenderId };
                }
            }

            await Clients.Caller.SendAsync("MessageSent", receiverId, content, message.CreatedAt, message.Id, type, replyData);

            if (OnlineUsers.TryGetValue(receiverId, out var connections))
            {
                await Clients.Clients(connections).SendAsync("ReceiveMessage", senderId, content, message.CreatedAt, message.Id, type, replyData);
            }
        }
        public async Task CallUser(string targetUserId)
        {
            var callerId = Context.UserIdentifier;
            CallStartTimes.AddOrUpdate(callerId, DateTime.Now, (key, old) => DateTime.Now);

            var caller = await _context.Users.FindAsync(callerId);
            string callerName = caller?.FullName ?? "Người dùng Vibee";
            string callerAvatar = caller?.AvatarUrl ?? "/images/default-avatar.png";

            if (OnlineUsers.TryGetValue(targetUserId, out var connections) && connections.Count > 0)
            {
                await Clients.Clients(connections).SendAsync("IncomingCall", callerId, callerName, callerAvatar);
            }
            else
            {
                var msg = new Message
                {
                    SenderId = callerId,
                    ReceiverId = targetUserId,
                    Content = "Người dùng hiện không liên lạc được",
                    Type = "Call",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                _context.Messages.Add(msg);
                await _context.SaveChangesAsync();

                await Clients.Caller.SendAsync("MessageSent", targetUserId, msg.Content, msg.CreatedAt, msg.Id, "Call", null);

                await Clients.Caller.SendAsync("CallRejected");
            }
        }

        public async Task AnswerCall(string callerId)
        {
            var receiverId = Context.UserIdentifier;

            ActiveCalls.TryAdd(callerId, receiverId);
            ActiveCalls.TryAdd(receiverId, callerId);

            if (OnlineUsers.TryGetValue(callerId, out var connections))
            {
                await Clients.Clients(connections).SendAsync("CallAccepted", receiverId);
            }
        }

        public async Task RejectCall(string callerId)
        {
            var receiverId = Context.UserIdentifier;

            var msg = new Message
            {
                SenderId = receiverId,
                ReceiverId = callerId,
                Content = "Đã từ chối cuộc gọi",
                Type = "Call",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            _context.Messages.Add(msg);
            await _context.SaveChangesAsync();

            if (OnlineUsers.TryGetValue(callerId, out var callerConns))
            {
                await Clients.Clients(callerConns).SendAsync("ReceiveMessage", receiverId, msg.Content, msg.CreatedAt, msg.Id, "Call", null);
                await Clients.Clients(callerConns).SendAsync("CallRejected");
            }

            await Clients.Caller.SendAsync("MessageSent", callerId, msg.Content, msg.CreatedAt, msg.Id, "Call", null);
        }

        public async Task EndCall(string targetUserId)
        {
            var senderId = Context.UserIdentifier;

            ActiveCalls.TryRemove(senderId, out _);
            ActiveCalls.TryRemove(targetUserId, out _);

            if (OnlineUsers.TryGetValue(targetUserId, out var connections))
            {
                await Clients.Clients(connections).SendAsync("CallEnded");
            }
        }

        public async Task SendSignal(string targetUserId, string data)
        {
            var senderId = Context.UserIdentifier;
            if (OnlineUsers.TryGetValue(targetUserId, out var connections))
            {
                await Clients.Clients(connections).SendAsync("ReceiveSignal", senderId, data);
            }
        }

        public async Task SaveCallHistory(string targetUserId, string status, int durationSeconds)
        {
            var senderId = Context.UserIdentifier;
            string content = "";
            if (status == "missed") content = "Cuộc gọi bị nhỡ";
            else if (status == "busy") content = "Người dùng bận";
            else
            {
                TimeSpan t = TimeSpan.FromSeconds(durationSeconds);
                content = $"Cuộc gọi video - {(t.Hours > 0 ? t.Hours + " giờ " : "")}{(t.Minutes > 0 ? t.Minutes + " phút " : "")}{t.Seconds} giây";
            }

            var msg = new Message { SenderId = senderId, ReceiverId = targetUserId, Content = content, Type = "Call", CreatedAt = DateTime.UtcNow, IsRead = false };
            _context.Messages.Add(msg);
            await _context.SaveChangesAsync();

            await Clients.Caller.SendAsync("MessageSent", targetUserId, content, msg.CreatedAt, msg.Id, "Call", null);

            if (OnlineUsers.TryGetValue(targetUserId, out var connections))
            {
                await Clients.Clients(connections).SendAsync("ReceiveMessage", senderId, content, msg.CreatedAt, msg.Id, "Call", null);
                await Clients.Clients(connections).SendAsync("ReloadSidebar");
            }
        }
        public async Task MarkAsRead(string senderId)
        {
            var receiverId = Context.UserIdentifier;
            var messages = await _context.Messages.Where(m => m.SenderId == senderId && m.ReceiverId == receiverId && !m.IsRead).ToListAsync();
            if (messages.Any())
            {
                foreach (var msg in messages) { msg.IsRead = true; msg.ReadAt = DateTime.UtcNow; }
                await _context.SaveChangesAsync();
                if (OnlineUsers.TryGetValue(senderId, out var connections)) await Clients.Clients(connections).SendAsync("MessagesRead", receiverId);
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
                    if (OnlineUsers.TryGetValue(message.ReceiverId, out var connections)) await Clients.Clients(connections).SendAsync("MessageDeleted", messageId, "everyone");
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