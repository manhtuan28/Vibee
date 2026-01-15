using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vibee.Data;
using Vibee.Models;
using Vibee.Services;

namespace Vibee.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SpamController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SpamProtectionService _spamService;

        public SpamController(ApplicationDbContext context, SpamProtectionService spamService)
        {
            _context = context;
            _spamService = spamService;
        }

        public IActionResult Index()
        {
            var blocks = _context.IpBlocks.OrderByDescending(b => b.BlockedAt).ToList();

            ViewBag.IsSystemActive = GetSetting("Spam_System_Active", "1") == "1";

            ViewBag.RegLimit = GetSetting("Spam_Register_Limit", "3");
            ViewBag.RegBlockTime = GetSetting("Spam_Register_BlockTime", "30");

            ViewBag.PostLimit = GetSetting("Spam_Post_Limit", "5");
            ViewBag.PostBlockTime = GetSetting("Spam_Post_BlockTime", "10");

            return View(blocks);
        }

        [HttpPost]
        public IActionResult ToggleSystem(bool isActive)
        {
            SaveSetting("Spam_System_Active", isActive ? "1" : "0");
            TempData["Message"] = isActive ? "Đã BẬT hệ thống chống Spam!" : "Đã TẮT hệ thống chống Spam (Cảnh báo rủi ro!)";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Unlock(int id)
        {
            _spamService.UnlockIp(id);
            TempData["Message"] = "Đã mở khóa IP thành công!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult UpdateSettings(int regLimit, int regTime, int postLimit, int postTime)
        {
            SaveSetting("Spam_Register_Limit", regLimit.ToString());
            SaveSetting("Spam_Register_BlockTime", regTime.ToString());

            SaveSetting("Spam_Post_Limit", postLimit.ToString());
            SaveSetting("Spam_Post_BlockTime", postTime.ToString());

            TempData["Message"] = "Đã cập nhật cấu hình chống Spam!";
            return RedirectToAction("Index");
        }

        private string GetSetting(string key, string def)
        {
            var s = _context.SystemSettings.FirstOrDefault(x => x.Key == key);
            return s?.Value ?? def;
        }

        private void SaveSetting(string key, string val)
        {
            var s = _context.SystemSettings.FirstOrDefault(x => x.Key == key);
            if (s == null)
            {
                _context.SystemSettings.Add(new SystemSetting
                {
                    Key = key,
                    Value = val,
                    Description = "Cấu hình tự động"
                });
            }
            else
            {
                s.Value = val;
            }
            _context.SaveChanges();
        }
    }
}