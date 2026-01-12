using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Models;

namespace Vibee.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PostsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PostsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string keyword, int page = 1)
        {
            int pageSize = 10;
            var query = _context.Posts
                .Include(p => p.User)
                .Include(p => p.Medias)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                .Include(p => p.Shares)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(p => p.Content.ToLower().Contains(keyword) ||
                                         p.User.FullName.ToLower().Contains(keyword) ||
                                         p.User.Email.ToLower().Contains(keyword));
            }

            int totalPosts = await query.CountAsync();
            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PostAdminViewModel
                {
                    Id = p.Id,
                    AuthorName = p.User.FullName ?? "Người dùng ẩn",
                    AuthorAvatar = p.User.AvatarUrl ?? "/images/default-avatar.png",
                    AuthorEmail = p.User.Email,
                    Content = System.Net.WebUtility.HtmlDecode(p.Content),
                    CreatedAt = p.CreatedAt,
                    PrivacyLevel = p.PrivacyLevel,
                    MediaCount = p.Medias.Count,
                    FirstMediaUrl = p.Medias.FirstOrDefault() != null ? p.Medias.FirstOrDefault().Url : null,
                    MediaType = p.Medias.FirstOrDefault() != null ? p.Medias.FirstOrDefault().Type.ToString() : null,
                    LikeCount = p.Likes.Count + p.FakeLikeCount,
                    CommentCount = p.Comments.Count,
                    ShareCount = p.Shares.Count + p.FakeShareCount,
                    IsShared = p.SharedPostId != null
                })
                .ToListAsync();

            var model = new PostListAdminViewModel
            {
                Posts = posts,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalPosts / (double)pageSize),
                Keyword = keyword
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetPostDetails(int id)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Medias)
                .Include(p => p.Comments).ThenInclude(c => c.User)
                .Include(p => p.Likes).ThenInclude(l => l.User)
                .Include(p => p.Shares).ThenInclude(s => s.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            var likesList = post.Likes.Select(l => new {
                userName = l.User.FullName,
                avatar = l.User.AvatarUrl,
                type = l.Type,
                isBot = false
            }).ToList();

            for (int i = 0; i < post.FakeLikeCount; i++)
            {
                likesList.Add(new { userName = "Bot Hệ Thống", avatar = "/images/default-avatar.png", type = "Like", isBot = true });
            }

            var sharesList = post.Shares.Select(s => new {
                userName = s.User.FullName,
                avatar = s.User.AvatarUrl,
                time = s.CreatedAt.ToString("dd/MM HH:mm"),
                isBot = false
            }).ToList();

            for (int i = 0; i < post.FakeShareCount; i++)
            {
                sharesList.Add(new { userName = "Bot Hệ Thống", avatar = "/images/default-avatar.png", time = "Vừa xong", isBot = true });
            }

            var data = new
            {
                id = post.Id,
                content = System.Net.WebUtility.HtmlDecode(post.Content),
                createdAt = post.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                privacy = post.PrivacyLevel == 0 ? "Công khai" : (post.PrivacyLevel == 1 ? "Bạn bè" : "Riêng tư"),
                author = new { name = post.User.FullName, avatar = post.User.AvatarUrl },
                medias = post.Medias.Select(m => new { url = m.Url, type = m.Type.ToString() }).ToList(),

                comments = post.Comments.OrderByDescending(c => c.CreatedAt).Select(c => new {
                    id = c.Id,
                    userId = c.UserId,
                    userName = c.User.FullName,
                    userAvatar = c.User.AvatarUrl,
                    content = System.Net.WebUtility.HtmlDecode(c.Content),
                    time = c.CreatedAt.ToString("dd/MM HH:mm")
                }).ToList(),

                likes = likesList,
                shares = sharesList,

                fakeLikes = post.FakeLikeCount,
                fakeShares = post.FakeShareCount
            };

            return Json(new { success = true, data });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, string content, int privacy, int fakeLikes, int fakeShares)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null) return Json(new { success = false, message = "Bài viết không tồn tại." });

            post.Content = System.Net.WebUtility.HtmlEncode(content);
            post.PrivacyLevel = privacy;

            post.FakeLikeCount = fakeLikes >= 0 ? fakeLikes : 0;
            post.FakeShareCount = fakeShares >= 0 ? fakeShares : 0;

            post.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Cập nhật bài viết thành công!" });
        }
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.Posts
                .Include(p => p.Comments)
                .Include(p => p.Likes)
                .Include(p => p.Medias)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return Json(new { success = false, message = "Bài viết không tồn tại." });

            _context.Comments.RemoveRange(post.Comments);
            _context.Likes.RemoveRange(post.Likes);
            _context.PostMedias.RemoveRange(post.Medias);

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa bài viết." });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return Json(new { success = false });

            var replies = _context.Comments.Where(c => c.ParentCommentId == id);
            _context.Comments.RemoveRange(replies);

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
    public class PostListAdminViewModel
    {
        public List<PostAdminViewModel> Posts { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string Keyword { get; set; }
    }
    public class PostAdminViewModel
    {
        public int Id { get; set; }
        public string AuthorName { get; set; }
        public string AuthorAvatar { get; set; }
        public string AuthorEmail { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public int PrivacyLevel { get; set; }
        public int MediaCount { get; set; }
        public string? FirstMediaUrl { get; set; }
        public string? MediaType { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public int ShareCount { get; set; }
        public bool IsShared { get; set; }
    }
}