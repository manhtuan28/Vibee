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
    public class PostsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public PostsController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePost(string content, string locationName, string emotion, int privacyLevel, List<IFormFile> mediaFiles, [FromServices] FileService fileService)
        {
            if (string.IsNullOrWhiteSpace(content) && (mediaFiles == null || !mediaFiles.Any()) && string.IsNullOrEmpty(emotion) && string.IsNullOrEmpty(locationName))
            {
                return Json(new { success = false, message = "Nội dung không được trống" });
            }

            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            var post = new Post
            {
                Content = content != null ? System.Net.WebUtility.HtmlEncode(content).Replace("\n", "<br>") : "",
                UserId = _userManager.GetUserId(User),
                CreatedAt = DateTime.Now,
                LocationName = locationName,
                Emotion = emotion,
                PrivacyLevel = privacyLevel,
                IpAddress = remoteIp
            };

            if (mediaFiles != null)
            {
                foreach (var file in mediaFiles)
                {
                    try
                    {
                        var path = await fileService.UploadFileAsync(file);
                        if (path != null)
                        {
                            post.Medias.Add(new PostMedia
                            {
                                Url = path,
                                Type = file.ContentType.StartsWith("video") ? MediaType.Video : MediaType.Image
                            });
                        }
                    }
                    catch { }
                }
            }

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đăng bài thành công!" });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var post = await _context.Posts.FindAsync(id);

            if (post == null) return Json(new { success = false, message = "Bài viết không tồn tại." });

            if (post.UserId != user.Id)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa bài viết này." });
            }

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa bài viết." });
        }

        [HttpGet]
        public async Task<IActionResult> GetPost(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var post = await _context.Posts.Include(p => p.Medias).FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();
            if (post.UserId != user.Id) return Forbid();

            return Json(new
            {
                success = true,
                data = new
                {
                    id = post.Id,
                    content = System.Net.WebUtility.HtmlDecode(post.Content?.Replace("<br>", "\n")),
                    locationName = post.LocationName,
                    emotion = post.Emotion,
                    privacyLevel = post.PrivacyLevel,
                    medias = post.Medias.Select(m => new { m.Id, m.Url, m.Type }).ToList()
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string content, string locationName, string emotion, int privacyLevel,
                                              List<IFormFile> newMediaFiles, string removedMediaIds,
                                              [FromServices] FileService fileService)
        {
            var user = await _userManager.GetUserAsync(User);
            var post = await _context.Posts.Include(p => p.Medias).FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return Json(new { success = false, message = "Bài viết không tồn tại." });
            if (post.UserId != user.Id) return Json(new { success = false, message = "Bạn không có quyền sửa bài này." });

            post.Content = content != null ? System.Net.WebUtility.HtmlEncode(content).Replace("\n", "<br>") : "";
            post.LocationName = locationName;
            post.Emotion = emotion;
            post.PrivacyLevel = privacyLevel;
            post.UpdatedAt = DateTime.Now;

            if (!string.IsNullOrEmpty(removedMediaIds))
            {
                var idsToRemove = removedMediaIds.Split(',').Select(int.Parse).ToList();
                var mediasToDelete = post.Medias.Where(m => idsToRemove.Contains(m.Id)).ToList();

                foreach (var media in mediasToDelete)
                {
                    _context.PostMedias.Remove(media);
                }
            }

            if (newMediaFiles != null)
            {
                foreach (var file in newMediaFiles)
                {
                    try
                    {
                        var path = await fileService.UploadFileAsync(file);
                        if (path != null)
                        {
                            post.Medias.Add(new PostMedia
                            {
                                Url = path,
                                Type = file.ContentType.StartsWith("video") ? MediaType.Video : MediaType.Image
                            });
                        }
                    }
                    catch { }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Cập nhật thành công!" });
        }
    }
}