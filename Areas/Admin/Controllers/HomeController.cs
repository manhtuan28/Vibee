using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Vibee.Data;
using Vibee.Models;

namespace Vibee.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var process = Process.GetCurrentProcess();
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name == Path.GetPathRoot(Environment.SystemDirectory))
                        ?? DriveInfo.GetDrives().First();

            var serverStats = new SystemStatsViewModel
            {
                OsDescription = RuntimeInformation.OSDescription,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessUptime = DateTime.Now - process.StartTime,

                UsedMemoryBytes = process.WorkingSet64,

                TotalDiskSpace = drive.TotalSize,
                FreeDiskSpace = drive.AvailableFreeSpace
            };

            var viewModel = new DashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalSystemMoney = await _context.Wallets.SumAsync(w => w.Balance),
                TotalPosts = await _context.Posts.CountAsync(),
                PendingTransactions = await _context.Transactions.CountAsync(t => t.Status == TransactionStatus.Pending),

                NewestUsers = await _context.Users
                    .OrderByDescending(u => u.JoinDate)
                    .Take(5)
                    .Select(u => new UserSummary
                    {
                        Email = u.Email,
                        FullName = u.FullName ?? "Chưa đặt tên",
                        JoinDate = u.JoinDate
                    })
                    .ToListAsync(),

                SystemStats = serverStats
            };

            return View(viewModel);
        }
    }

    public class DashboardViewModel
    {
        public int TotalUsers { get; set; }
        public decimal TotalSystemMoney { get; set; }
        public int TotalPosts { get; set; }
        public int PendingTransactions { get; set; }
        public List<UserSummary> NewestUsers { get; set; } = new();
        public SystemStatsViewModel SystemStats { get; set; }
    }

    public class UserSummary
    {
        public string Email { get; set; }
        public string FullName { get; set; }
        public DateTime JoinDate { get; set; }
    }

    public class SystemStatsViewModel
    {
        public string OsDescription { get; set; }
        public string FrameworkDescription { get; set; }
        public TimeSpan ProcessUptime { get; set; }
        public long UsedMemoryBytes { get; set; }
        public long TotalDiskSpace { get; set; }
        public long FreeDiskSpace { get; set; }

        public int DiskUsagePercent => (int)((double)(TotalDiskSpace - FreeDiskSpace) / TotalDiskSpace * 100);

        public string UptimeDisplay => $"{ProcessUptime.Days}d {ProcessUptime.Hours}h {ProcessUptime.Minutes}m";
        public string MemoryDisplay => $"{(UsedMemoryBytes / 1024 / 1024)} MB";
    }
}