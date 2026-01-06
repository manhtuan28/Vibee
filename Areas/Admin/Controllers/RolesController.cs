using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vibee.Data;

namespace Vibee.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class RolesController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public RolesController(RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _roleManager = roleManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var roles = await _context.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
            return View(roles);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string newRoleName)
        {
            if (string.IsNullOrWhiteSpace(newRoleName))
            {
                TempData["StatusMessage"] = "Lỗi: Tên quyền không được để trống.";
                return RedirectToAction(nameof(Index));
            }

            var roleName = newRoleName.Trim();
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                TempData["StatusMessage"] = $"Lỗi: Quyền '{roleName}' đã tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            await _roleManager.CreateAsync(new IdentityRole(roleName));
            TempData["StatusMessage"] = $"Thành công: Đã tạo quyền '{roleName}'.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return RedirectToAction(nameof(Index));

            var role = await _roleManager.FindByIdAsync(id);
            if (role != null)
            {
                if (role.Name == "Admin" || role.Name == "User")
                {
                    TempData["StatusMessage"] = "Lỗi: Không được xóa quyền hệ thống.";
                    return RedirectToAction(nameof(Index));
                }

                await _roleManager.DeleteAsync(role);
                TempData["StatusMessage"] = "Thành công: Đã xóa quyền.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}