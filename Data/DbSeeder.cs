using Microsoft.AspNetCore.Identity;
using Vibee.Models;

namespace Vibee.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider service)
        {
            var userManager = service.GetService<UserManager<ApplicationUser>>();
            var roleManager = service.GetService<RoleManager<IdentityRole>>();

            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }
            if (!await roleManager.RoleExistsAsync("User"))
            {
                await roleManager.CreateAsync(new IdentityRole("User"));
            }

            var adminEmail = "buimanhtuan2k4@gmail.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                var newAdmin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Bùi Mạnh Tuấn",
                    EmailConfirmed = true,
                    AvatarUrl = "/images/default-avatar.png"
                };

                var result = await userManager.CreateAsync(newAdmin, Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD") ?? "Admin@123!");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                }
            }
        }
    }
}