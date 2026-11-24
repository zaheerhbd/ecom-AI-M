using System.Linq;
using System.Threading.Tasks;
using Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity
{
    public class AppIdentityDbContextSeed
    {
        public static async Task SeedUsersAsync(UserManager<AppUser> userManager)
        {
            if (!userManager.Users.Any())
            {
                var user = new AppUser
                {
                    DisplayName = "Bob",
                    Email = "bob@test.com",
                    UserName = "bob@test.com",
                    Address = new Address
                    {
                        FirstName = "Bob",
                        LastName = "Bobbity",
                        Street = "10 The street",
                        City = "New York",
                        State = "NY",
                        ZipCode = "90210"
                    }
                };

                await userManager.CreateAsync(user, "Pa$$w0rd");
            }
        }

        public const string AdminRole = "Admin";

        /// <summary>
        /// Creates a shareable demo admin (demo@test.com / Demo@1234) and gives it the Admin role, so the
        /// admin-only Runs and Evals screens can be shown in a demo without using a personal account.
        /// Self-contained (does not rely on Agent:AdminEmails) and safe to run again.
        /// </summary>
        public static async Task SeedDemoAdminAsync(RoleManager<IdentityRole> roleManager, UserManager<AppUser> userManager)
        {
            if (!await roleManager.RoleExistsAsync(AdminRole))
                await roleManager.CreateAsync(new IdentityRole(AdminRole));

            var demo = await userManager.FindByEmailAsync("demo@test.com");
            if (demo == null)
            {
                demo = new AppUser
                {
                    DisplayName = "Demo Admin",
                    Email = "demo@test.com",
                    UserName = "demo@test.com",
                    Address = new Address
                    {
                        FirstName = "Demo",
                        LastName = "Admin",
                        Street = "1 Demo Street",
                        City = "Demo City",
                        State = "NY",
                        ZipCode = "10001"
                    }
                };

                await userManager.CreateAsync(demo, "Demo@1234");
            }

            if (!await userManager.IsInRoleAsync(demo, AdminRole))
                await userManager.AddToRoleAsync(demo, AdminRole);
        }

        /// <summary>
        /// Creates the "Admin" role (if missing) and gives it to every listed email that has an account
        /// (Phase 3, decision D4). The list comes from config "Agent:AdminEmails". Safe to run again.
        /// </summary>
        public static async Task SeedAdminRoleAsync(RoleManager<IdentityRole> roleManager,
            UserManager<AppUser> userManager, System.Collections.Generic.IEnumerable<string> adminEmails)
        {
            if (!await roleManager.RoleExistsAsync(AdminRole))
                await roleManager.CreateAsync(new IdentityRole(AdminRole));

            foreach (var email in adminEmails ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(email))
                    continue;

                var user = await userManager.FindByEmailAsync(email.Trim());
                if (user != null && !await userManager.IsInRoleAsync(user, AdminRole))
                    await userManager.AddToRoleAsync(user, AdminRole);
            }
        }
    }
}