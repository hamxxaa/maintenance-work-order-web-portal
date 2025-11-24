using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using mwowp.Web.Models;

namespace mwowp.Web.Data
{
    public static class DbInitializer
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Roller
            string[] roles = { "User", "Technician", "Manager" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Manager kullanıcı
            var adminEmail = "manager@test.com";
            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Default Manager",
                    IsManager = true
                };
                await userManager.CreateAsync(admin, "Password123!");
                await userManager.AddToRoleAsync(admin, "Manager");
            }

            // Test User
            var userEmail = "user@test.com";
            var user = await userManager.FindByEmailAsync(userEmail);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = userEmail,
                    Email = userEmail,
                    FullName = "Test User"
                };
                await userManager.CreateAsync(user, "Password123!");
                await userManager.AddToRoleAsync(user, "User");
            }

            // Test Technician
            var techEmail = "tech@test.com";
            var tech = await userManager.FindByEmailAsync(techEmail);
            if (tech == null)
            {
                tech = new ApplicationUser
                {
                    UserName = techEmail,
                    Email = techEmail,
                    FullName = "Test Technician",
                    IsTechnician = true
                };
                await userManager.CreateAsync(tech, "Password123!");
                await userManager.AddToRoleAsync(tech, "Technician");
            }
        }
    }
}
