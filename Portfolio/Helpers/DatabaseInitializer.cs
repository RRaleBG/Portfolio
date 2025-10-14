using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portfolio.Database;
using Portfolio.Models;

namespace Portfolio.Helpers
{
    public static class DatabaseInitializer
    {
        public static string EnsureDatabaseDirectory(string contentRootPath, ILogger logger)
        {
            var dataDir = Path.Combine(contentRootPath, "Database");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
                logger?.LogInformation("Created database directory: {DataDir}", dataDir);
            }
            return dataDir;
        }

        public static string GetDatabasePath(string contentRootPath, ILogger logger)
        {
            var dataDir = EnsureDatabaseDirectory(contentRootPath, logger);
            var dbPath = Path.Combine(dataDir, "portfolio.db");
            return dbPath;
        }


        public static async Task EnsureAndSeedDatabaseAsync(ApplicationDbContext db, IServiceProvider sp, ILogger logger, string dbPath)
        {
            // Delete DB file if present
            //if (File.Exists(dbPath))
            //{
            //    File.Delete(dbPath);
            //    logger?.LogInformation("Deleted existing database file: {DbPath}", dbPath);
            //}

            // Apply migrations
            await db.Database.MigrateAsync();
            logger?.LogInformation("Applied migrations to new database.");

            // Seed roles
            var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
            var adminRole = "Admin";
            if (!await roleMgr.RoleExistsAsync(adminRole))
            {
                await roleMgr.CreateAsync(new IdentityRole(adminRole));
                logger?.LogInformation("Created role: {Role}", adminRole);
            }

            // Seed admin user
            var userMgr = sp.GetRequiredService<UserManager<IdentityUser>>();
            const string adminEmail = "rajcicrados@hotmail.com";
            var adminUser = await userMgr.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                };
                var result = await userMgr.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await userMgr.AddToRoleAsync(adminUser, adminRole);
                    logger?.LogInformation("Created and assigned Admin user: {Email}", adminEmail);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger?.LogWarning("Failed to create admin user: {Errors}", errors);
                }
            }

            // Seed projects
            if (!await db.Projects.AnyAsync())
            {
                db.Projects.AddRange(
                    new Project
                    {
                        Title = "AI Portfolio",
                        Summary = "GPT-powered portfolio chat.",
                        Url = "https://cloud.rajcicrados.rs",
                        Tags = "AI,GPT,.NET",
                        Date = DateTime.UtcNow.AddMonths(-1)
                    },
                    new Project
                    {
                        Title = "Razor Pages Site",
                        Summary = "Clean, fast UI.",
                        Url = "https://coolify.rajcicrados.rs",
                        Tags = "Razor,ASP.NET Core",
                        Date = DateTime.UtcNow.AddMonths(-2)
                    }
                );
                await db.SaveChangesAsync();
                logger?.LogInformation("Seeded sample projects.");
            }

            // Seed blog posts
            if (!await db.BlogPosts.AnyAsync())
            {
                db.BlogPosts.AddRange(
                    new BlogPost
                    {
                        Title = "Welcome to My Portfolio",
                        Content = "This is my first blog post. This is my portfolio website where I showcase my projects and share my thoughts.",
                        Slug = "introduction-portfolio",
                        CreatedAt = DateTime.UtcNow.AddMonths(-1)
                    },
                    new BlogPost
                    {
                        Title = "Getting Started with ASP.NET Core",
                        Content = "A beginner's guide to ASP.NET Core. ASP.NET Core is a cross-platform framework for building modern web applications.",
                        Slug = "aspnetcore-web-development",
                        CreatedAt = DateTime.UtcNow.AddMonths(-2)
                    }
                );
                await db.SaveChangesAsync();
                logger?.LogInformation("Seeded sample blog posts.");
            }

            // Seed contacts
            if (!await db.Contacts.AnyAsync())
            {
                db.Contacts.AddRange(
                    new Contact
                    {
                        Name = "John Doe",
                        Email = "user@user.com",
                        Subject = "Hello",
                        Message = "I would like to connect with you."
                    },
                    new Contact
                    {
                        Name = "Harry",
                        Email = "Harry@Harry.com",
                        Subject = "Hello",
                        Message = "I would like to connect with you."
                    },
                    new Contact
                    {
                        Name = "John",
                        Email = "John@John.com",
                        Subject = "Hello",
                        Message = "I would like to connect with you."
                    }
                );
                await db.SaveChangesAsync();
                logger?.LogInformation("Seeded sample contacts.");
            }

            logger?.LogInformation("Database created, migrations applied, and initial data seeded.");
        }

        public static async Task EnsureDatabaseUpToDateAsync(ApplicationDbContext db, IServiceProvider sp,  ILogger logger, string dbPath)
        {
            if (!File.Exists(dbPath))
            {
                logger?.LogInformation("Database file not found at {DbPath}. Creating and seeding...", dbPath);
                await EnsureAndSeedDatabaseAsync(db, sp, logger, dbPath);
                return;
            }

            try
            {
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    logger?.LogInformation("Pending migrations detected: {Migrations}", string.Join(", ", pendingMigrations));
                    await db.Database.MigrateAsync();
                    logger?.LogInformation("Applied pending migrations successfully.");
                }
                else
                {
                    logger?.LogInformation("No pending migrations. Database is already up to date.");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error while applying migrations to existing database.");
                throw;
            }
        }


        public static async Task SetupDatabaseAsync(IServiceProvider services, string contentRootPath)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var dbPath = GetDatabasePath(contentRootPath, logger);

            await EnsureDatabaseUpToDateAsync(db, scope.ServiceProvider, logger, dbPath);
        }

        public static async Task ForceRecreateAndSeedDatabaseAsync(IServiceProvider sp, ILogger logger, string dbPath)
        {
            // Dispose any existing context before deleting the file
            GC.Collect();
            GC.WaitForPendingFinalizers();

            //if (File.Exists(dbPath))
            //{
            //    File.Delete(dbPath);
            //    logger?.LogInformation("Deleted existing database file: {DbPath}", dbPath);
            //}

            // Create a new scope and context for seeding
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await EnsureAndSeedDatabaseAsync(db, scope.ServiceProvider, logger, dbPath);
        }
    }
}