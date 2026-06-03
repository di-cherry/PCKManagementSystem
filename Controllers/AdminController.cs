using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PCKManagementSystem.Data;
using PCKManagementSystem.Hubs;
using PCKManagementSystem.Models;
using PCKManagementSystem.Models.ViewModels;
using System.Security.Claims;

namespace PCKManagementSystem.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;
        private readonly ILogger<AdminController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        public AdminController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            ILogger<AdminController> logger, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _hubContext = hubContext;
        }

        // Вспомогательный метод для получения ID текущего пользователя
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return !string.IsNullOrEmpty(userIdClaim) ? int.Parse(userIdClaim) : 0;
        }

        // Вспомогательный метод для добавления записи в аудит
        private async Task AddAuditLogAsync(string actionType, string description, string? entityType = null, int? entityId = null)
        {
            var userId = GetCurrentUserId();
            var user = await _userManager.FindByIdAsync(userId.ToString());

            var auditLog = new AuditLog
            {
                UserId = userId,
                UserEmail = User.Identity?.Name ?? "Unknown",
                UserFullName = user?.FullName ?? "Unknown",
                ActionType = actionType,
                ActionDescription = description,
                EntityType = entityType,
                EntityId = entityId,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers["User-Agent"].ToString(),
                ActionDate = DateTime.UtcNow,
                AdditionalInfo = string.Empty, 
                OldValuesJson = string.Empty,   
                NewValuesJson = string.Empty
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        #region Главная панель

        // GET: Admin
        public async Task<IActionResult> Index()
        {
            var stats = new DashboardStatisticsViewModel();

            // Пользователи
            stats.TotalUsers = await _context.Users.CountAsync();
            stats.ActiveUsers = await _context.Users.CountAsync(u => u.IsActive);

            var teachers = await _userManager.GetUsersInRoleAsync("Преподаватель");
            stats.TeachersCount = teachers.Count;

            var chairmen = await _userManager.GetUsersInRoleAsync("Председатель ПЦК");
            stats.ChairmenCount = chairmen.Count;

            var admins = await _userManager.GetUsersInRoleAsync("Администратор");
            stats.AdminsCount = admins.Count;

            // Документы
            stats.TotalDocuments = await _context.Documents.CountAsync();
            stats.DraftDocuments = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.Draft);
            stats.ReviewDocuments = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.Review);
            stats.ApprovedDocuments = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.Approved);
            stats.RejectedDocuments = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.Rejected);

            stats.DocumentsByStatus = new Dictionary<string, int>
            {
                ["Черновики"] = stats.DraftDocuments,
                ["На рассмотрении"] = stats.ReviewDocuments,
                ["Утвержденные"] = stats.ApprovedDocuments,
                ["Отклоненные"] = stats.RejectedDocuments
            };

            // Дисциплины и специальности
            stats.TotalDisciplines = await _context.Disciplines.CountAsync();
            stats.TotalSpecialties = await _context.Specialties.CountAsync();

            // Нагрузка
            stats.TotalWorkloadRecords = await _context.Workloads.CountAsync();
            stats.TotalWorkloadHours = await _context.Workloads.SumAsync(w => w.Hours);

            // Задачи
            stats.TotalTasks = await _context.Tasks.CountAsync();
            stats.PendingTasks = await _context.Tasks.CountAsync(t => t.Status == Models.TaskStatus.Pending);
            stats.CompletedTasks = await _context.Tasks.CountAsync(t => t.Status == Models.TaskStatus.Completed);
            stats.OverdueTasks = await _context.Tasks.CountAsync(t => t.Status == Models.TaskStatus.Overdue);

            stats.TasksByStatus = new Dictionary<string, int>
            {
                ["Ожидают"] = stats.PendingTasks,
                ["В работе"] = await _context.Tasks.CountAsync(t => t.Status == Models.TaskStatus.InProgress),
                ["Выполнены"] = stats.CompletedTasks,
                ["Просрочены"] = stats.OverdueTasks,
                ["Отменены"] = await _context.Tasks.CountAsync(t => t.Status == Models.TaskStatus.Cancelled)
            };

            // Самые активные пользователи
            var activeUsers = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => new
                {
                    UserId = u.Id,
                    UserName = u.FullName ?? u.Email,
                    UserEmail = u.Email,
                    DocumentsCount = _context.Documents.Count(d => d.AuthorId == u.Id),
                    TasksAssignedCount = _context.Tasks.Count(t => t.AssignedById == u.Id),
                    TasksCompletedCount = _context.Tasks.Count(t => t.AssignedToId == u.Id && t.Status == Models.TaskStatus.Completed)
                })
                .ToListAsync();

            stats.MostActiveUsers = activeUsers
                .Select(u => new UserActivityViewModel
                {
                    UserName = u.UserName,
                    UserEmail = u.UserEmail,
                    DocumentsCreated = u.DocumentsCount,
                    TasksAssigned = u.TasksAssignedCount,
                    TasksCompleted = u.TasksCompletedCount
                })
                .OrderByDescending(u => u.DocumentsCreated + u.TasksAssigned + u.TasksCompleted)
                .Take(5)
                .ToList();

            // Последние действия
            var recentLogs = await _context.AuditLogs
                .OrderByDescending(a => a.ActionDate)
                .Take(10)
                .Select(a => new
                {
                    a.Id,
                    a.UserFullName,
                    a.UserEmail,
                    a.ActionType,
                    a.ActionDescription,
                    a.ActionDate
                })
                .ToListAsync();

            stats.RecentActivities = recentLogs
                .Select(a => new RecentActivityViewModel
                {
                    Id = a.Id,
                    UserName = a.UserFullName ?? a.UserEmail ?? "Неизвестно",
                    Action = a.ActionType ?? "Действие",
                    Details = a.ActionDescription ?? "",
                    Time = a.ActionDate,
                    Icon = GetIconForAction(a.ActionType),
                    Color = GetColorForAction(a.ActionType)
                })
                .ToList();

            await AddAuditLogAsync("Просмотр", "Администратор просмотрел панель управления", "Dashboard");

            return View(stats);
        }

        private string GetIconForAction(string actionType)
        {
            return actionType?.ToLower() switch
            {
                "создание" => "bi-plus-circle",
                "редактирование" => "bi-pencil",
                "удаление" => "bi-trash",
                "вход" => "bi-box-arrow-in-right",
                "выход" => "bi-box-arrow-right",
                "блокировка" => "bi-lock",
                "разблокировка" => "bi-unlock",
                "назначение роли" => "bi-person-badge",
                _ => "bi-info-circle"
            };
        }

        private string GetColorForAction(string actionType)
        {
            return actionType?.ToLower() switch
            {
                "создание" => "success",
                "редактирование" => "warning",
                "удаление" => "danger",
                "вход" => "info",
                "выход" => "secondary",
                "блокировка" => "dark",
                "разблокировка" => "primary",
                "назначение роли" => "info",
                _ => "primary"
            };
        }

        #endregion

        #region Управление пользователями

        // GET: Admin/Users
        public async Task<IActionResult> Users(string sortOrder, string searchString)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = sortOrder == "name" ? "name_desc" : "name";
            ViewData["EmailSortParm"] = sortOrder == "email" ? "email_desc" : "email";
            ViewData["StatusSortParm"] = sortOrder == "status" ? "status_desc" : "status";
            ViewData["CreatedSortParm"] = sortOrder == "created" ? "created_desc" : "created";
            ViewData["LastLoginSortParm"] = sortOrder == "lastlogin" ? "lastlogin_desc" : "lastlogin";
            ViewData["CurrentFilter"] = searchString;

            var usersQuery = _context.Users
                .OrderBy(u => u.FullName)
                .Select(u => new UserListViewModel
                {
                    Id = u.Id,
                    Email = u.Email,
                    UserName = u.UserName,
                    FullName = u.FullName ?? "",
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    LastLoginAt = u.LastLoginAt
                })
                .AsQueryable();

            // Поиск
            if (!string.IsNullOrEmpty(searchString))
            {
                usersQuery = usersQuery.Where(u =>
                    u.FullName.Contains(searchString) ||
                    u.Email.Contains(searchString) ||
                    u.UserName.Contains(searchString));
            }

            // Сортировка
            switch (sortOrder)
            {
                case "name":
                    usersQuery = usersQuery.OrderBy(u => u.FullName);
                    break;
                case "name_desc":
                    usersQuery = usersQuery.OrderByDescending(u => u.FullName);
                    break;
                case "email":
                    usersQuery = usersQuery.OrderBy(u => u.Email);
                    break;
                case "email_desc":
                    usersQuery = usersQuery.OrderByDescending(u => u.Email);
                    break;
                case "status":
                    usersQuery = usersQuery.OrderBy(u => u.IsActive);
                    break;
                case "status_desc":
                    usersQuery = usersQuery.OrderByDescending(u => u.IsActive);
                    break;
                case "created":
                    usersQuery = usersQuery.OrderBy(u => u.CreatedAt);
                    break;
                case "created_desc":
                    usersQuery = usersQuery.OrderByDescending(u => u.CreatedAt);
                    break;
                case "lastlogin":
                    usersQuery = usersQuery.OrderBy(u => u.LastLoginAt);
                    break;
                case "lastlogin_desc":
                    usersQuery = usersQuery.OrderByDescending(u => u.LastLoginAt);
                    break;
                default:
                    usersQuery = usersQuery.OrderBy(u => u.FullName);
                    break;
            }

            var users = await usersQuery.ToListAsync();

            // Загружаем роли для каждого пользователя
            foreach (var user in users)
            {
                var appUser = await _userManager.FindByIdAsync(user.Id.ToString());
                var roles = await _userManager.GetRolesAsync(appUser);
                user.Roles = roles.ToList();
            }

            await AddAuditLogAsync("Просмотр", "Администратор просмотрел список пользователей", "Users");
            return View(users);
        }

        // GET: Admin/Users/Create
        public async Task<IActionResult> CreateUser()
        {
            var model = new UserCreateViewModel();
            model.AllRoles = await GetRolesList();
            return View(model);
        }

        // POST: Admin/Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(UserCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Проверяем, существует ли уже пользователь с таким email
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Пользователь с таким email уже существует");
                    model.AllRoles = await GetRolesList();
                    return View(model);
                }

                var user = new User
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    IsActive = model.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = true // Для тестирования сразу подтверждаем
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // Назначаем роли
                    if (model.SelectedRoles != null && model.SelectedRoles.Any())
                    {
                        await _userManager.AddToRolesAsync(user, model.SelectedRoles);
                    }

                    await AddAuditLogAsync("Создание", $"Создан пользователь {model.Email}", "User", user.Id);
                    TempData["Success"] = $"Пользователь {model.Email} успешно создан";
                    return RedirectToAction(nameof(Users));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            model.AllRoles = await GetRolesList();
            return View(model);
        }

        // GET: Admin/Users/Edit/5
        public async Task<IActionResult> EditUser(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new UserEditViewModel
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                FullName = user.FullName,
                IsActive = user.IsActive,
                SelectedRoles = userRoles.ToList(),
                AllRoles = await GetRolesList()
            };

            return View(model);
        }

        // POST: Admin/Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(int id, UserEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(id.ToString());
                if (user == null)
                {
                    return NotFound();
                }

                // Проверяем, не занят ли email другим пользователем
                var userWithSameEmail = await _userManager.FindByEmailAsync(model.Email);
                if (userWithSameEmail != null && userWithSameEmail.Id != id)
                {
                    ModelState.AddModelError("Email", "Этот email уже используется другим пользователем");
                    model.AllRoles = await GetRolesList();
                    return View(model);
                }

                // Обновляем данные пользователя
                user.Email = model.Email;
                user.UserName = model.Email; // Обычно UserName = Email
                user.FullName = model.FullName;
                user.IsActive = model.IsActive;

                var updateResult = await _userManager.UpdateAsync(user);
                if (updateResult.Succeeded)
                {
                    // Обновляем роли
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    var rolesToAdd = model.SelectedRoles?.Except(currentRoles).ToList() ?? new();
                    var rolesToRemove = currentRoles.Except(model.SelectedRoles ?? new()).ToList();

                    if (rolesToAdd.Any())
                    {
                        await _userManager.AddToRolesAsync(user, rolesToAdd);
                    }
                    if (rolesToRemove.Any())
                    {
                        await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                    }

                    await AddAuditLogAsync("Редактирование", $"Отредактирован пользователь {model.Email}", "User", user.Id);
                    TempData["Success"] = $"Пользователь {model.Email} успешно обновлен";
                    return RedirectToAction(nameof(Users));
                }

                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            model.AllRoles = await GetRolesList();
            return View(model);
        }

        // POST: Admin/Users/ToggleBlock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBlock(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return NotFound();
            }

            // Не даем заблокировать самого себя
            if (user.Id == GetCurrentUserId())
            {
                TempData["Error"] = "Вы не можете заблокировать самого себя";
                return RedirectToAction(nameof(Users));
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            var action = user.IsActive ? "разблокирован" : "заблокирован";
            var message = $"Ваш аккаунт был {action} администратором.";
            string? url = null; // можно не давать ссылку

            await _hubContext.Clients.User(user.Id.ToString())
                .SendAsync("ReceiveNotification", message, url);

            await AddAuditLogAsync(action, $"{action} пользователя {user.Email}", "User", user.Id);
            TempData["Success"] = $"Пользователь {user.Email} {(user.IsActive ? "разблокирован" : "заблокирован")}";

            return RedirectToAction(nameof(Users));
        }
        // GET: Admin/DeleteUser/5
        public async Task<IActionResult> DeleteUser(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.Users
                .Include(u => u.Documents)
                .Include(u => u.Workloads)
                .Include(u => u.AssignedTasks)
                .Include(u => u.CreatedTasks)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            // Проверяем наличие связанных данных
            bool hasDocuments = user.Documents.Any();
            bool hasWorkload = user.Workloads.Any();
            bool hasTasks = user.AssignedTasks.Any() || user.CreatedTasks.Any();

            ViewBag.HasRelatedData = hasDocuments || hasWorkload || hasTasks;
            ViewBag.DocumentsCount = user.Documents.Count;
            ViewBag.WorkloadCount = user.Workloads.Count;
            ViewBag.TasksCount = user.AssignedTasks.Count + user.CreatedTasks.Count;

            return View(user);
        }
        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserConfirmed(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound();

            // Не даем удалить самого себя
            if (user.Id == GetCurrentUserId())
            {
                TempData["Error"] = "Вы не можете удалить самого себя.";
                return RedirectToAction(nameof(Users));
            }

            // Повторная проверка связанных данных
            bool hasDocuments = await _context.Documents.AnyAsync(d => d.AuthorId == id);
            bool hasWorkload = await _context.Workloads.AnyAsync(w => w.TeacherId == id);
            bool hasTasks = await _context.Tasks.AnyAsync(t => t.AssignedToId == id || t.AssignedById == id);

            if (hasDocuments || hasWorkload || hasTasks)
            {
                TempData["Error"] = "Нельзя удалить пользователя, у которого есть связанные данные (документы, нагрузка, задачи). Сначала удалите или переназначьте их.";
                return RedirectToAction(nameof(Users));
            }

            var email = user.Email;
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                await AddAuditLogAsync("Удаление", $"Удален пользователь {email}", "User", id);
                TempData["Success"] = $"Пользователь {email} удален";
            }
            else
            {
                TempData["Error"] = "Ошибка при удалении пользователя";
            }

            return RedirectToAction(nameof(Users));
        }

        private async Task<List<SelectListItem>> GetRolesList()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            return roles.Select(r => new SelectListItem
            {
                Value = r.Name,
                Text = r.Name
            }).ToList();
        }

        #endregion

        #region Журнал аудита

        // GET: Admin/AuditLog
        public async Task<IActionResult> AuditLog(AuditLogFilterViewModel filter)
        {
            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            // Применяем фильтры
            if (!string.IsNullOrEmpty(filter.ActionType))
            {
                query = query.Where(a => a.ActionType == filter.ActionType);
            }

            if (!string.IsNullOrEmpty(filter.EntityType))
            {
                query = query.Where(a => a.EntityType == filter.EntityType);
            }

            if (filter.UserId.HasValue)
            {
                query = query.Where(a => a.UserId == filter.UserId.Value);
            }

            if (filter.StartDate.HasValue)
            {
                query = query.Where(a => a.ActionDate >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                var endDate = filter.EndDate.Value.AddDays(1);
                query = query.Where(a => a.ActionDate <= endDate);
            }

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(a =>
                    a.ActionDescription.ToLower().Contains(search) ||
                    a.UserEmail.ToLower().Contains(search) ||
                    a.UserFullName.ToLower().Contains(search));
            }

            var logs = await query
                .OrderByDescending(a => a.ActionDate)
                .Select(a => new AuditLogViewModel
                {
                    Id = a.Id,
                    UserEmail = a.UserEmail,
                    UserFullName = a.UserFullName,
                    ActionType = a.ActionType,
                    ActionDescription = a.ActionDescription,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    IpAddress = a.IpAddress,
                    ActionDate = a.ActionDate,
                    AdditionalInfo = a.AdditionalInfo
                })
                .ToListAsync();

            // Подготавливаем данные для фильтров
            filter.ActionTypes = await _context.AuditLogs
                .Select(a => a.ActionType)
                .Distinct()
                .OrderBy(t => t)
                .Select(t => new SelectListItem { Value = t, Text = t })
                .ToListAsync();

            filter.EntityTypes = await _context.AuditLogs
                .Select(a => a.EntityType)
                .Distinct()
                .OrderBy(t => t)
                .Select(t => new SelectListItem { Value = t, Text = t })
                .ToListAsync();

            filter.Users = await _context.Users
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = u.FullName ?? u.Email
                })
                .ToListAsync();

            ViewBag.Filter = filter;
            return View(logs);
        }

        // GET: Admin/AuditLog/Details/5
        public async Task<IActionResult> AuditLogDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var log = await _context.AuditLogs
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (log == null)
            {
                return NotFound();
            }

            return View(log);
        }

        #endregion

        #region Объявления

        // GET: Admin/Announcements
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Announcements()
        {
            var announcements = await _context.Announcements
                .Include(a => a.CreatedBy)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AnnouncementViewModel
                {
                    Id = a.Id,
                    Title = a.Title,
                    Content = a.Content,
                    IsActive = a.IsActive,
                    CreatedAt = a.CreatedAt,
                    CreatedByName = a.CreatedBy.FullName ?? a.CreatedBy.Email,
                    CreatedById = a.CreatedById
                })
                .ToListAsync();

            return View(announcements);
        }

        // GET: Admin/Announcements/Create
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public IActionResult CreateAnnouncement()
        {
            return View();
        }

        // POST: Admin/Announcements/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAnnouncement(AnnouncementCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var announcement = new Announcement
                {
                    Title = model.Title,
                    Content = model.Content,
                    IsActive = model.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = GetCurrentUserId()
                };

                _context.Announcements.Add(announcement);
                await _context.SaveChangesAsync();

                var message = $"Новое объявление: «{model.Title}»";
                var url = Url.Action("Index", "Home"); 

                await _hubContext.Clients.All.SendAsync("ReceiveNotification", message, url);

                await AddAuditLogAsync("Создание", $"Создано объявление: {model.Title}", "Announcement", announcement.Id);
                TempData["Success"] = "Объявление успешно создано";
                return RedirectToAction(nameof(Announcements));
            }

            return View(model);
        }

        // GET: Admin/Announcements/Edit/5
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> EditAnnouncement(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null)
            {
                return NotFound();
            }

            var model = new AnnouncementCreateViewModel
            {
                Title = announcement.Title,
                Content = announcement.Content,
                IsActive = announcement.IsActive
            };

            return View(model);
        }

        // POST: Admin/Announcements/Edit/5
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAnnouncement(int id, AnnouncementCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var announcement = await _context.Announcements.FindAsync(id);
                if (announcement == null)
                {
                    return NotFound();
                }

                announcement.Title = model.Title;
                announcement.Content = model.Content;
                announcement.IsActive = model.IsActive;

                _context.Update(announcement);
                await _context.SaveChangesAsync();

                await AddAuditLogAsync("Редактирование", $"Отредактировано объявление: {model.Title}", "Announcement", id);
                TempData["Success"] = "Объявление успешно обновлено";
                return RedirectToAction(nameof(Announcements));
            }

            return View(model);
        }

        // POST: Admin/Announcements/Delete/5
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null)
            {
                return NotFound();
            }

            _context.Announcements.Remove(announcement);
            await _context.SaveChangesAsync();

            await AddAuditLogAsync("Удаление", $"Удалено объявление: {announcement.Title}", "Announcement", id);
            TempData["Success"] = "Объявление удалено";
            return RedirectToAction(nameof(Announcements));
        }

        #endregion

        [HttpGet]
        public async Task<IActionResult> ExportUsersToExcel(string? role, bool? isActive)
        {
            var usersQuery = _userManager.Users.AsQueryable();

            // Фильтр по статусу
            if (isActive.HasValue)
            {
                usersQuery = usersQuery.Where(u => u.IsActive == isActive.Value);
            }

            var users = await usersQuery.ToListAsync();

            // Фильтр по роли 
            if (!string.IsNullOrEmpty(role))
            {
                var filteredUsers = new List<User>();
                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains(role))
                        filteredUsers.Add(user);
                }
                users = filteredUsers;
            }

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Пользователи");

            worksheet.Cells[1, 1].Value = "Email";
            worksheet.Cells[1, 2].Value = "ФИО";
            worksheet.Cells[1, 3].Value = "Активен";
            worksheet.Cells[1, 4].Value = "Дата регистрации";
            worksheet.Cells[1, 5].Value = "Последний вход";
            worksheet.Cells[1, 6].Value = "Роли";
            worksheet.Cells[1, 1, 1, 6].Style.Font.Bold = true;

            int row = 2;
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                worksheet.Cells[row, 1].Value = user.Email;
                worksheet.Cells[row, 2].Value = user.FullName;
                worksheet.Cells[row, 3].Value = user.IsActive ? "Да" : "Нет";
                worksheet.Cells[row, 4].Value = user.CreatedAt.ToString("dd.MM.yyyy HH:mm");
                worksheet.Cells[row, 5].Value = user.LastLoginAt?.ToString("dd.MM.yyyy HH:mm") ?? "—";
                worksheet.Cells[row, 6].Value = string.Join(", ", roles);
                row++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            var stream = new MemoryStream(package.GetAsByteArray());
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Users.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportAuditLogToExcel(string? actionType, string? entityType, int? userId, DateTime? startDate, DateTime? endDate, string? searchTerm)
        {
            var query = _context.AuditLogs.Include(a => a.User).AsQueryable();

            if (!string.IsNullOrEmpty(actionType))
                query = query.Where(a => a.ActionType == actionType);
            if (!string.IsNullOrEmpty(entityType))
                query = query.Where(a => a.EntityType == entityType);
            if (userId.HasValue)
                query = query.Where(a => a.UserId == userId.Value);
            if (startDate.HasValue)
                query = query.Where(a => a.ActionDate >= startDate.Value);
            if (endDate.HasValue)
            {
                var end = endDate.Value.AddDays(1);
                query = query.Where(a => a.ActionDate <= end);
            }
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var search = searchTerm.ToLower();
                query = query.Where(a => a.ActionDescription.ToLower().Contains(search) ||
                                         a.UserEmail.ToLower().Contains(search) ||
                                         a.UserFullName.ToLower().Contains(search));
            }

            var logs = await query.OrderByDescending(a => a.ActionDate).ToListAsync();

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Журнал аудита");

            worksheet.Cells[1, 1].Value = "Дата и время";
            worksheet.Cells[1, 2].Value = "Пользователь";
            worksheet.Cells[1, 3].Value = "Email";
            worksheet.Cells[1, 4].Value = "Тип действия";
            worksheet.Cells[1, 5].Value = "Описание";
            worksheet.Cells[1, 6].Value = "Сущность";
            worksheet.Cells[1, 7].Value = "ID сущности";
            worksheet.Cells[1, 8].Value = "IP адрес";
            worksheet.Cells[1, 1, 1, 8].Style.Font.Bold = true;

            int row = 2;
            foreach (var log in logs)
            {
                worksheet.Cells[row, 1].Value = log.ActionDate.ToString("dd.MM.yyyy HH:mm:ss");
                worksheet.Cells[row, 2].Value = log.UserFullName;
                worksheet.Cells[row, 3].Value = log.UserEmail;
                worksheet.Cells[row, 4].Value = log.ActionType;
                worksheet.Cells[row, 5].Value = log.ActionDescription;
                worksheet.Cells[row, 6].Value = log.EntityType;
                worksheet.Cells[row, 7].Value = log.EntityId;
                worksheet.Cells[row, 8].Value = log.IpAddress;
                row++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            var stream = new MemoryStream(package.GetAsByteArray());
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "AuditLog.xlsx");
        }
    }
}