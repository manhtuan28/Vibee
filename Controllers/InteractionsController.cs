using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Hubs;
using Vibee.Models;

namespace Vibee.Controllers
{
    [Authorize]
    public class InteractionsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public InteractionsController(UserManager<ApplicationUser> userManager, ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _userManager = userManager;
            _context = context;
            _hubContext = hubContext;
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
            _context.Notifications.Add(noti);
            await _context.SaveChangesAsync();

            var sender = await _context.Users.FindAsync(senderId);
            await _hubContext.Clients.User(receiverId).SendAsync("ReceiveNotification", new
            {
                id = noti.Id,
                avatar = sender.AvatarUrl ?? "/images/default-avatar.png",
                content = $"<strong>{sender.FullName}</strong> {content}",
                link = link,
                time = "Vừa xong",
                isRead = false
            });
        }

        [HttpPost]
        public async Task<IActionResult> React(int postId, string type)
        {
            var user = await _userManager.GetUserAsync(User);
            var existing = await _context.Likes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == user.Id);

            string action = "";

            if (existing != null)
            {
                if (existing.Type == type)
                {
                    _context.Likes.Remove(existing);
                    action = "removed";
                }
                else
                {
                    existing.Type = type;
                    action = "updated";
                }
            }
            else
            {
                _context.Likes.Add(new Like { PostId = postId, UserId = user.Id, Type = type });
                action = "added";
            }

            await _context.SaveChangesAsync();

            if (action == "added")
            {
                var post = await _context.Posts.FindAsync(postId);
                if (post != null)
                {
                    string link = $"/Profile/{post.UserId}#post-{postId}";

                    await CreateNotification(
                        receiverId: post.UserId,
                        senderId: user.Id,
                        content: "đã bày tỏ cảm xúc về bài viết của bạn.",
                        link: link,
                        type: 1
                    );
                }
            }

            return Json(new { success = true, action = action });
        }

        [HttpPost]
        public async Task<IActionResult> Comment(int postId, string content, int? parentId = null)
        {
            if (string.IsNullOrWhiteSpace(content)) return Json(new { success = false });

            content = System.Net.WebUtility.HtmlEncode(content);

            var user = await _userManager.GetUserAsync(User);
            var post = await _context.Posts.FindAsync(postId);
            if (post == null) return Json(new { success = false });

            var comment = new Comment
            {
                PostId = postId,
                UserId = user.Id,
                Content = content,
                ParentCommentId = parentId,
                CreatedAt = DateTime.Now
            };

            _context.Comments.Add(comment);

            await _context.SaveChangesAsync();
            string link = $"/Profile/{post.UserId}?postId={postId}&commentId={comment.Id}";

            if (parentId != null)
            {
                var parentComment = await _context.Comments.FindAsync(parentId);
                if (parentComment != null)
                {
                    await CreateNotification(parentComment.UserId, user.Id, "đã trả lời bình luận của bạn.", link, 2);
                }
            }
            else
            {
                if (post.UserId != user.Id)
                {
                    await CreateNotification(post.UserId, user.Id, "đã bình luận về bài viết của bạn.", link, 2);
                }
            }

            return Json(new
            {
                success = true,
                id = comment.Id,
                parentId = comment.ParentCommentId,
                userName = user.FullName,
                userUrl = user.UrlHandle ?? user.Id,
                avatar = user.AvatarUrl ?? "/images/default-avatar.png",
                time = "Vừa xong",
                content = content,
                isOwner = true
            });
        }

        [HttpPost]
        public async Task<IActionResult> Share(int postId, string content)
        {
            var user = await _userManager.GetUserAsync(User);
            var originalPost = await _context.Posts.FindAsync(postId);
            if (originalPost == null) return Json(new { success = false });
            int realOriginalId = originalPost.SharedPostId ?? originalPost.Id;

            await CreateNotification(originalPost.UserId, user.Id, "đã chia sẻ bài viết của bạn.", $"/Profile/{user.Id}", 3);

            var newPost = new Post
            {
                UserId = user.Id,
                Content = content,
                SharedPostId = realOriginalId,
                CreatedAt = DateTime.Now,
                PrivacyLevel = 0
            };

            _context.Posts.Add(newPost);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Vui lòng đăng nhập" });

            var comment = await _context.Comments
                .Include(c => c.Replies)
                .Include(c => c.Post)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null) return Json(new { success = false, message = "Không tìm thấy bình luận" });

            bool isCommentOwner = comment.UserId == user.Id;
            bool isPostOwner = comment.Post?.UserId == user.Id;

            if (!isCommentOwner && !isPostOwner)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa bình luận này" });
            }

            try
            {
                if (comment.Replies != null && comment.Replies.Any())
                {
                    _context.Comments.RemoveRange(comment.Replies);
                }

                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi server: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetComments(int postId, int skip = 0, int take = 5)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var currentUserId = user?.Id;

                var postOwnerId = await _context.Posts
                    .Where(p => p.Id == postId)
                    .Select(p => p.UserId)
                    .FirstOrDefaultAsync();

                var totalRootComments = await _context.Comments.CountAsync(c => c.PostId == postId && c.ParentCommentId == null);

                var comments = await _context.Comments
                    .Where(c => c.PostId == postId && c.ParentCommentId == null)
                    .Include(c => c.User)
                    .Include(c => c.Replies).ThenInclude(r => r.User)
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip(skip).Take(take)
                    .ToListAsync();

                var result = comments.Select(c => new
                {
                    id = c.Id,
                    userParams = c.User.UrlHandle ?? c.User.Id,
                    userName = c.User.FullName,
                    avatar = c.User.AvatarUrl ?? "/images/default-avatar.png",
                    content = c.Content,
                    time = c.CreatedAt.ToString("dd/MM HH:mm"),

                    isOwner = (currentUserId != null) && (c.UserId == currentUserId || postOwnerId == currentUserId),

                    parentId = c.ParentCommentId,

                    replies = c.Replies.OrderBy(r => r.CreatedAt).Select(r => new
                    {
                        id = r.Id,
                        parentId = c.Id,
                        userParams = r.User.UrlHandle ?? r.User.Id,
                        userName = r.User.FullName,
                        avatar = r.User.AvatarUrl ?? "/images/default-avatar.png",
                        content = r.Content,
                        time = r.CreatedAt.ToString("dd/MM HH:mm"),

                        isOwner = (currentUserId != null) && (r.UserId == currentUserId || postOwnerId == currentUserId)
                    }).ToList()
                });

                return Json(new { success = true, data = result, hasMore = (skip + take) < totalRootComments });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}