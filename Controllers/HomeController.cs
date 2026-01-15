using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;
using Vibee.Models;
using Vibee.ViewModels;

namespace Vibee.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public HomeController(ILogger<HomeController> logger, UserManager<ApplicationUser> userManager, ApplicationDbContext context, SignInManager<ApplicationUser> signInManager)
        {
            _logger = logger;
            _userManager = userManager;
            _context = context;
            _signInManager = signInManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account", new { area = "Identity" });

            if (currentUser == null)
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var friendIds = await _context.Friendships
                .Where(f => (f.SenderId == currentUser.Id || f.ReceiverId == currentUser.Id)
                             && f.Status == FriendshipStatus.Accepted)
                .Select(f => f.SenderId == currentUser.Id ? f.ReceiverId : f.SenderId)
                .ToListAsync();

            var posts = await _context.Posts
                    .Include(p => p.User).Include(p => p.Medias).Include(p => p.Likes).Include(p => p.Comments)
                    .Where(p => p.User.LockoutEnd == null || p.User.LockoutEnd <= DateTimeOffset.UtcNow)
                    .Include(p => p.SharedPost)
                        .ThenInclude(sp => sp.User)
                    .Include(p => p.SharedPost)
                        .ThenInclude(sp => sp.Medias)
                    .Where(p =>
                                p.PrivacyLevel != 2

                                && (
                                    p.PrivacyLevel == 0 ||
                                    (p.PrivacyLevel == 1 && (p.UserId == currentUser.Id || friendIds.Contains(p.UserId)))
                                )
                            )
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

            var otherUsers = await _context.Users
                .Where(u => u.Id != currentUser.Id)
                .Take(10)
                .ToListAsync();

            var onlineFriends = await _context.Friendships
                .Where(f => (f.SenderId == currentUser.Id || f.ReceiverId == currentUser.Id)
                            && f.Status == FriendshipStatus.Accepted)
                .Include(f => f.Sender)
                .Include(f => f.Receiver)
                .Select(f => f.SenderId == currentUser.Id ? f.Receiver : f.Sender)
                .ToListAsync();

            var model = new HomeViewModel
            {
                CurrentUser = currentUser,
                Posts = posts,
                OnlineFriends = onlineFriends,
                SuggestFriends = otherUsers.Take(3).ToList()
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return RedirectToAction("Index");

            var currentUser = await _userManager.GetUserAsync(User);

            var users = await _context.Users
                .Where(u => u.FullName.Contains(query) && u.Id != currentUser.Id)
                .ToListAsync();

            ViewBag.Query = query;
            return View(users);
        }
    }
}