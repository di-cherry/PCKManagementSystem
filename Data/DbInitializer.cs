using Microsoft.AspNetCore.Identity;
using PCKManagementSystem.Models;
using Microsoft.Extensions.Logging;

namespace PCKManagementSystem.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("=== НАЧАЛО ИНИЦИАЛИЗАЦИИ БАЗЫ ДАННЫХ ===");

            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();

            // 1. Создаем роли (работает и в PostgreSQL)
            Console.WriteLine("Создание ролей...");
            string[] roleNames = { "Администратор", "Председатель ПЦК", "Преподаватель" };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole<int> { Name = roleName });
                    if (result.Succeeded)
                        Console.WriteLine($"✓ Роль '{roleName}' создана");
                    else
                        Console.WriteLine($"✗ Ошибка при создании роли '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
                else
                {
                    Console.WriteLine($"→ Роль '{roleName}' уже существует");
                }
            }

            // 2. Создаем администратора
            Console.WriteLine("\nСоздание пользователей...");
            await CreateUserIfNotExists(userManager, "admin@pck.ru", "Admin123!", "Главный Администратор", "Администратор");
            await CreateUserIfNotExists(userManager, "chairman@pck.ru", "Chairman123!", "Петров Петр Петрович", "Председатель ПЦК");
            await CreateUserIfNotExists(userManager, "teacher@pck.ru", "Teacher123!", "Иванов Иван Иванович", "Преподаватель");

            Console.WriteLine("=== ИНИЦИАЛИЗАЦИЯ БАЗЫ ДАННЫХ ЗАВЕРШЕНА ===\n");
        }

        private static async Task CreateUserIfNotExists(
            UserManager<User> userManager,
            string email,
            string password,
            string fullName,
            string role)
        {
            Console.Write($"Проверка пользователя {email}... ");

            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                Console.WriteLine("не найден. Создание...");

                user = new User
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = true   // важно для RequireConfirmedEmail
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    Console.WriteLine($"  ✓ Пользователь {email} создан");
                    var roleResult = await userManager.AddToRoleAsync(user, role);
                    if (roleResult.Succeeded)
                        Console.WriteLine($"  ✓ Роль {role} назначена");
                    else
                        Console.WriteLine($"  ✗ Ошибка при назначении роли: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                }
                else
                {
                    Console.WriteLine($"  ✗ Ошибка при создании пользователя: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                Console.WriteLine($"уже существует (Id: {user.Id})");
            }
        }
    }
}