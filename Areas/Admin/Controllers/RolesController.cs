using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Vibee.Data;

namespace Vibee.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class RolesController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public RolesController(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            var model = new List<RoleViewModel>();

            foreach (var role in roles)
            {
                var claims = await _roleManager.GetClaimsAsync(role);
                model.Add(new RoleViewModel
                {
                    Id = role.Id,
                    Name = role.Name,
                    PermissionCount = claims.Count
                });
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetRole(string id)
        {
            var allPermissions = AppPermissions.GetAll();

            if (string.IsNullOrEmpty(id))
            {
                return Json(new { success = true, roleName = "", selectedPermissions = new List<string>(), allPermissions });
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();

            var currentClaims = await _roleManager.GetClaimsAsync(role);
            var selectedPermissions = currentClaims.Select(c => c.Value).ToList();

            return Json(new
            {
                success = true,
                id = role.Id,
                roleName = role.Name,
                selectedPermissions,
                allPermissions
            });
        }

        [HttpPost]
        public async Task<IActionResult> SaveRole(string id, string roleName, List<string> permissions)
        {
            if (string.IsNullOrWhiteSpace(roleName)) return Json(new { success = false, message = "Tên quyền không được trống." });

            IdentityRole role;
            if (string.IsNullOrEmpty(id))
            {
                if (await _roleManager.RoleExistsAsync(roleName))
                    return Json(new { success = false, message = "Tên quyền đã tồn tại." });

                role = new IdentityRole(roleName);
                await _roleManager.CreateAsync(role);
            }
            else
            {
                role = await _roleManager.FindByIdAsync(id);
                if (role == null) return Json(new { success = false, message = "Không tìm thấy quyền." });

                if (role.Name == "Admin" || role.Name == "User")
                {
                }
                else
                {
                    role.Name = roleName;
                    await _roleManager.UpdateAsync(role);
                }
            }
            var currentClaims = await _roleManager.GetClaimsAsync(role);
            foreach (var claim in currentClaims)
            {
                await _roleManager.RemoveClaimAsync(role, claim);
            }

            if (permissions != null)
            {
                foreach (var p in permissions)
                {
                    await _roleManager.AddClaimAsync(role, new Claim("Permission", p));
                }
            }

            return Json(new { success = true, message = "Lưu thành công!" });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return Json(new { success = false, message = "Không tìm thấy." });

            if (role.Name == "Admin" || role.Name == "User")
                return Json(new { success = false, message = "Không thể xóa quyền hệ thống." });

            await _roleManager.DeleteAsync(role);
            return Json(new { success = true, message = "Đã xóa." });
        }
    }

    public class RoleViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int PermissionCount { get; set; }
    }
}