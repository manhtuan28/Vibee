using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vibee.Data;
using Vibee.Models;

namespace Vibee.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var version = _context.SystemSettings.FirstOrDefault(s => s.Key == "AppVersion");
            return View(version);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateVersion(string versionValue)
        {
            var setting = _context.SystemSettings.FirstOrDefault(s => s.Key == "AppVersion");
            if (setting == null)
            {
                setting = new SystemSetting { Key = "AppVersion", Value = versionValue, Description = "Phiên bản ứng dụng" };
                _context.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = versionValue;
            }
            await _context.SaveChangesAsync();

            TempData["Message"] = "Cập nhật phiên bản thành công!";
            return RedirectToAction("Index");
        }
    }
}