using Microsoft.AspNetCore.Mvc;
using PCKManagementSystem.Data;
using PCKManagementSystem.Models;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PCKManagementSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace PCKManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                // Для авторизованных пользователей показываем дашборд
                var viewModel = await GetDashboardViewModel();
                return View("Dashboard", viewModel);
            }

            // Для неавторизованных - лендинг
            var landingViewModel = new LandingPageViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalDocuments = await _context.Documents.CountAsync(),
                TotalTasks = await _context.Tasks.CountAsync(),
                TotalDisciplines = await _context.Disciplines.CountAsync(),
                RecentAnnouncements = await _context.Announcements
                    .Where(a => a.IsActive)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(3)
                    .Include(a => a.CreatedBy)
                    .ToListAsync()
            };

            return View("Landing", landingViewModel);
        }

        public IActionResult About()
        {
            ViewData["Title"] = "О системе";

            var model = new AboutViewModel
            {
                SystemName = "АИС ПЦК",
                Version = "1.0.0",
                Description = "Автоматизированная информационная система для организации учебно-методической работы предметно-цикловой комиссии колледжа",
                Features = new List<string>
        {
            "Управление учебно-методической документацией",
            "Учет учебной нагрузки преподавателей",
            "Система задач и поручений",
            "Формирование отчетности",
            "Журнал аудита действий пользователей",
            "Разграничение прав доступа"
        },
                Technologies = new List<string>
        {
            "ASP.NET Core MVC",
            "Entity Framework Core",
            "SQL Lite",
            "Bootstrap 5",
            "ASP.NET Core Identity"
        },
                Developer = "Студент(ка) группы ...",
                Year = DateTime.UtcNow.Year
            };

            return View(model);
        }

        private async Task<DashboardViewModel> GetDashboardViewModel()
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);

            // данные для графика за последние 12 месяцев
            var today = DateTime.UtcNow.Date;
            var startDate = today.AddMonths(-11);
            var startOfMonth = new DateTime(startDate.Year, startDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            //var startOfMonth = new DateTime(startDate.Year, startDate.Month, 1);

            // Группировка документов по месяцам
            var docsByMonth = await _context.Documents
                .Where(d => d.CreatedAt >= startOfMonth)
                .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToDictionaryAsync(x => $"{x.Year}-{x.Month:00}", x => x.Count);

            // Группировка всех задач по месяцам 
            var tasksByMonth = await _context.Tasks
                .Where(t => t.DueDate >= startOfMonth)
                .GroupBy(t => new { t.DueDate.Year, t.DueDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToDictionaryAsync(x => $"{x.Year}-{x.Month:00}", x => x.Count);

            // Группировка выполненных задач по месяцам
            var completedTasksByMonth = await _context.Tasks
                .Where(t => t.Status == Models.TaskStatus.Completed && t.DueDate >= startOfMonth)
                .GroupBy(t => new { t.DueDate.Year, t.DueDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToDictionaryAsync(x => $"{x.Year}-{x.Month:00}", x => x.Count);

            // Запрос документов с фильтром по роли
            var documentsQuery = _context.Documents
                .Include(d => d.Discipline)
                .Include(d => d.Author)
                .AsQueryable();

            // Для преподавателя показываем только его документы
            if (!User.IsInRole("Администратор") && !User.IsInRole("Председатель ПЦК"))
            {
                documentsQuery = documentsQuery.Where(d => d.AuthorId == userId);
            }

            var months = new List<string>();
            var docsCount = new List<int>();
            var tasksCount = new List<int>();
            var completedCount = new List<int>();

            for (int i = 0; i < 12; i++)
            {
                var month = startDate.AddMonths(i);
                var key = $"{month.Year}-{month.Month:00}";
                months.Add(month.ToString("MMM yyyy"));
                docsCount.Add(docsByMonth.GetValueOrDefault(key, 0));
                tasksCount.Add(tasksByMonth.GetValueOrDefault(key, 0));
                completedCount.Add(completedTasksByMonth.GetValueOrDefault(key, 0));
            }

            var viewModel = new DashboardViewModel
            {
                //присвоение всех существующих свойств
                UserName = user?.FullName ?? user?.Email ?? "Пользователь",
                UserRole = GetUserRole(),
                TotalDocuments = await _context.Documents.CountAsync(),
                TotalTasks = await _context.Tasks.CountAsync(),
                MyDocuments = await _context.Documents.CountAsync(d => d.AuthorId == userId),
                MyTasks = await _context.Tasks.CountAsync(t => t.AssignedToId == userId),
                MyPendingTasks = await _context.Tasks.CountAsync(t => t.AssignedToId == userId &&
                                                                      t.Status != Models.TaskStatus.Completed &&
                                                                      t.Status != Models.TaskStatus.Cancelled),
                RecentDocuments = await documentsQuery
                    .OrderByDescending(d => d.CreatedAt)
                    .Take(5)
                    .ToListAsync(),
                RecentTasks = await _context.Tasks
                    .Include(t => t.Discipline)
                    .Include(t => t.AssignedTo)
                    .Include(t => t.AssignedBy)
                    .Where(t => t.AssignedToId == userId || t.AssignedById == userId)
                    .OrderByDescending(t => t.DueDate)
                    .Take(5)
                    .ToListAsync(),
                Announcements = await _context.Announcements
                    .Where(a => a.IsActive)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(3)
                    .Include(a => a.CreatedBy)
                    .ToListAsync(),
                DisciplineProgress = await GetDisciplineProgress(userId),
                UpcomingTasks = await _context.Tasks
                    .Where(t => t.AssignedToId == userId &&
                                t.Status != Models.TaskStatus.Completed &&
                                t.Status != Models.TaskStatus.Cancelled &&
                                t.DueDate >= today && 
                                t.DueDate <= today.AddDays(2))
                    .OrderBy(t => t.DueDate)
                    .Take(3)
                    .ToListAsync(),
                // Новые поля
                Months = months,
                DocumentsCreated = docsCount,
                TasksCreated = tasksCount,
                TasksCompleted = completedCount
            };

            return viewModel;
        }

        private async Task<List<DisciplineProgressViewModel>> GetDisciplineProgress(int userId)
        {
            // Для преподавателя — прогресс по его документам в разрезе дисциплин
            // Если администратор/председатель — можно показывать общую статистику или по всем дисциплинам
            var query = _context.Documents
                .Where(d => d.AuthorId == userId)
                .GroupBy(d => d.DisciplineId)
                .Select(g => new DisciplineProgressViewModel
                {
                    DisciplineName = g.First().Discipline.Name,
                    TotalDocuments = g.Count(),
                    ApprovedDocuments = g.Count(d => d.Status == DocumentStatus.Approved)
                })
                .OrderByDescending(x => x.TotalDocuments)
                .Take(4);

            return await query.ToListAsync();
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }

        private string GetUserRole()
        {
            if (User.IsInRole("Администратор")) return "Администратор";
            if (User.IsInRole("Председатель ПЦК")) return "Председатель ПЦК";
            if (User.IsInRole("Преподаватель")) return "Преподаватель";
            return "Пользователь";
        }

        public IActionResult Privacy()
        {
            ViewData["Title"] = "Политика конфиденциальности";

            var model = new PrivacyViewModel
            {
                LastUpdated = new DateTime(2026, 1, 1),
                CompanyName = "АИС ПЦК",
                Email = "privacy@pck.ru"
            };

            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}