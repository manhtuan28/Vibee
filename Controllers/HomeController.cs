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

        public HomeController(ILogger<HomeController> logger, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _logger = logger;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account", new { area = "Identity" });

            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Medias)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                .Where(p =>
                    p.PrivacyLevel == 0 ||
                    p.PrivacyLevel == 1 ||
                    (p.PrivacyLevel == 2 && p.UserId == currentUser.Id)
                )
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var otherUsers = await _context.Users
                .Where(u => u.Id != currentUser.Id)
                .Take(10)
                .ToListAsync();

            var model = new HomeViewModel
            {
                CurrentUser = currentUser,
                Posts = posts,
                OnlineFriends = otherUsers,
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
    }
}