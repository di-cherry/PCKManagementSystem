using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PCKManagementSystem.Data;
using PCKManagementSystem.Hubs;
using PCKManagementSystem.Models;
using PCKManagementSystem.Models.ViewModels;
using System.Security.Claims;
// Явно указываем алиас для нашего enum
using TaskStatus = PCKManagementSystem.Models.TaskStatus;

namespace PCKManagementSystem.Controllers
{
    [Authorize]
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TasksController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<User> _userManager;

        // Базовый путь для постоянного хранилища Amvera
        private const string StorageBasePath = "/data/uploads";

        public TasksController(ApplicationDbContext context, ILogger<TasksController> logger, IHubContext<NotificationHub> hubContext, IEmailSender emailSender,
    UserManager<User> userManager)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _emailSender = emailSender;
            _userManager = userManager;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim) : 0;
        }

        private bool CanEditTask(PCKManagementSystem.Models.Tasks task)
        {
            var userId = GetCurrentUserId();
            return User.IsInRole("Администратор") ||
                   User.IsInRole("Председатель ПЦК") ||
                   task.AssignedById == userId;
        }

        private bool CanDeleteTask(PCKManagementSystem.Models.Tasks task)
        {
            var userId = GetCurrentUserId();
            return User.IsInRole("Администратор") ||
                   User.IsInRole("Председатель ПЦК") ||
                   task.AssignedById == userId;
        }

        // GET: Tasks
        public async Task<IActionResult> Index(TasksFilterViewModel filter, string? sortOrder, string? searchString)
        {
            var userId = GetCurrentUserId();
            var query = _context.Tasks
                .Include(t => t.Discipline)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .AsQueryable();

            if (User.IsInRole("Преподаватель") && !User.IsInRole("Администратор") && !User.IsInRole("Председатель ПЦК"))
                query = query.Where(t => t.AssignedToId == userId || t.AssignedById == userId);

            if (filter.Status.HasValue)
                query = query.Where(t => t.Status == (TaskStatus)filter.Status.Value);
            if (filter.DisciplineId.HasValue)
                query = query.Where(t => t.DisciplineId == filter.DisciplineId.Value);
            if (filter.AssignedToId.HasValue && (User.IsInRole("Администратор") || User.IsInRole("Председатель ПЦК")))
                query = query.Where(t => t.AssignedToId == filter.AssignedToId.Value);
            if (filter.ShowOverdueOnly == true)
                query = query.Where(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.Cancelled && t.DueDate < DateTime.UtcNow);

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                query = query.Where(t => t.Title.ToLower().Contains(searchString) ||
                                         (t.Description != null && t.Description.ToLower().Contains(searchString)) ||
                                         t.AssignedTo.FullName.ToLower().Contains(searchString));
            }

            ViewData["TitleSortParam"] = string.IsNullOrEmpty(sortOrder) ? "title_desc" : "";
            ViewData["DueDateSortParam"] = sortOrder == "dueDate" ? "dueDate_desc" : "dueDate";
            ViewData["StatusSortParam"] = sortOrder == "status" ? "status_desc" : "status";
            ViewData["ExecutorSortParam"] = sortOrder == "executor" ? "executor_desc" : "executor";
            ViewData["CurrentSort"] = sortOrder;
            ViewData["CurrentSearch"] = searchString;

            query = sortOrder switch
            {
                "title_desc" => query.OrderByDescending(t => t.Title),
                "dueDate" => query.OrderBy(t => t.DueDate),
                "dueDate_desc" => query.OrderByDescending(t => t.DueDate),
                "status" => query.OrderBy(t => t.Status),
                "status_desc" => query.OrderByDescending(t => t.Status),
                "executor" => query.OrderBy(t => t.AssignedTo.FullName),
                "executor_desc" => query.OrderByDescending(t => t.AssignedTo.FullName),
                _ => query.OrderBy(t => t.Title)
            };

            await UpdateOverdueTasks();

            var tasks = await query
                .Select(t => new TasksListViewModel
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    DueDate = t.DueDate,
                    Status = t.Status,
                    DisciplineName = t.Discipline.Name,
                    DisciplineCode = t.Discipline.Code,
                    AssignedToName = t.AssignedTo.FullName,
                    AssignedByName = t.AssignedBy.FullName,
                    AttachmentFileName = t.AttachmentFileName,
                    AttachmentUrl = t.AttachmentUrl,
                    CompletionComment = t.CompletionComment,
                    CompletionAttachmentFileName = t.CompletionAttachmentFileName,
                    CompletionUrl = t.CompletionUrl
                })
                .ToListAsync();

            await PrepareFilterViewBag(filter);
            ViewBag.SearchString = searchString;
            return View(tasks);
        }

        // GET: Tasks/MyTasks
        [Authorize(Roles = "Преподаватель")]
        public async Task<IActionResult> MyTasks()
        {
            var userId = GetCurrentUserId();
            await UpdateOverdueTasks();

            var tasks = await _context.Tasks
                .Include(t => t.Discipline)
                .Include(t => t.AssignedBy)
                .Where(t => t.AssignedToId == userId)
                .OrderBy(t => t.Status == TaskStatus.Completed ? 1 : 0)
                .ThenBy(t => t.DueDate)
                .Select(t => new TasksListViewModel
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    DueDate = t.DueDate,
                    Status = t.Status,
                    DisciplineName = t.Discipline.Name,
                    DisciplineCode = t.Discipline.Code,
                    AssignedByName = t.AssignedBy.FullName,
                    AttachmentFileName = t.AttachmentFileName,
                    AttachmentUrl = t.AttachmentUrl,
                    CompletionComment = t.CompletionComment,
                    CompletionAttachmentFileName = t.CompletionAttachmentFileName,
                    CompletionUrl = t.CompletionUrl
                })
                .ToListAsync();

            ViewBag.IsMyTasks = true;
            return View("Index", tasks);
        }

        // GET: Tasks/Create
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Create()
        {
            var model = new TasksCreateViewModel();
            await PopulateDropdowns(model);
            return View(model);
        }

        // POST: Tasks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Create(TasksCreateViewModel model, IFormFile? AttachmentFile)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View(model);
            }

            var task = new PCKManagementSystem.Models.Tasks
            {
                Title = model.Title,
                Description = model.Description,
                DueDate = model.DueDate,
                DisciplineId = model.DisciplineId,
                AssignedToId = model.AssignedToId,
                AssignedById = GetCurrentUserId(),
                Status = TaskStatus.Pending,
                AttachmentUrl = model.AttachmentUrl
            };

            // Сохранение файла в постоянное хранилище
            if (AttachmentFile != null && AttachmentFile.Length > 0)
            {
                var uploadDir = Path.Combine(StorageBasePath, "uploads", "tasks");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(AttachmentFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await AttachmentFile.CopyToAsync(stream);
                task.AttachmentFilePath = $"/uploads/tasks/{fileName}";
                task.AttachmentFileName = AttachmentFile.FileName;
            }

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            var message = $"Вам назначена новая задача: «{task.Title}»";
            var url = Url.Action("Details", "Tasks", new { id = task.Id });
            await _hubContext.Clients.User(task.AssignedToId.ToString())
                .SendAsync("ReceiveNotification", message, url);

            var assignedUser = await _userManager.FindByIdAsync(task.AssignedToId.ToString());
            if (assignedUser != null && !string.IsNullOrEmpty(assignedUser.Email))
            {
                var subject = $"Новая задача: {task.Title}";
                var body = $@"
                    <h3>Вам назначена новая задача</h3>
                    <p><strong>Название:</strong> {task.Title}</p>
                    <p><strong>Описание:</strong> {task.Description}</p>
                    <p><strong>Срок выполнения:</strong> {task.DueDate:dd.MM.yyyy}</p>
                    <p><strong>Постановщик:</strong> {User.Identity?.Name}</p>
                    <a href='{Url.Action("Details", "Tasks", new { id = task.Id }, "https")}'>Перейти к задаче</a>
                ";
                await _emailSender.SendEmailAsync(assignedUser.Email, subject, body);
            }

            TempData["Success"] = "Задача успешно создана";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tasks/Edit/5
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound();
            if (!CanEditTask(task)) return Forbid();

            var model = new TasksEditViewModel
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                DueDate = task.DueDate,
                DisciplineId = task.DisciplineId,
                AssignedToId = task.AssignedToId,
                Status = task.Status,
                AttachmentUrl = task.AttachmentUrl,
                ExistingAttachmentFileName = task.AttachmentFileName,
                ExistingAttachmentFilePath = task.AttachmentFilePath
            };

            await PopulateDropdowns(model);
            return View(model);
        }

        // POST: Tasks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Edit(int id, TasksEditViewModel model, IFormFile? NewAttachmentFile, bool RemoveAttachmentFile)
        {
            if (id != model.Id) return NotFound();
            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound();
            if (!CanEditTask(task)) return Forbid();

            if (ModelState.IsValid)
            {
                task.Title = model.Title;
                task.Description = model.Description;
                task.DueDate = model.DueDate;
                task.DisciplineId = model.DisciplineId;
                task.AssignedToId = model.AssignedToId;
                task.Status = model.Status;
                task.AttachmentUrl = model.AttachmentUrl;

                // Управление файлом
                if (RemoveAttachmentFile && !string.IsNullOrEmpty(task.AttachmentFilePath))
                {
                    var oldPath = Path.Combine(StorageBasePath, task.AttachmentFilePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                    task.AttachmentFilePath = null;
                    task.AttachmentFileName = null;
                }

                if (NewAttachmentFile != null && NewAttachmentFile.Length > 0)
                {
                    if (!string.IsNullOrEmpty(task.AttachmentFilePath))
                    {
                        var oldPath = Path.Combine(StorageBasePath, task.AttachmentFilePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }
                    var uploadDir = Path.Combine(StorageBasePath, "uploads", "tasks");
                    if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);
                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(NewAttachmentFile.FileName)}";
                    var filePath = Path.Combine(uploadDir, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await NewAttachmentFile.CopyToAsync(stream);
                    task.AttachmentFilePath = $"/uploads/tasks/{fileName}";
                    task.AttachmentFileName = NewAttachmentFile.FileName;
                }

                _context.Update(task);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Задача успешно обновлена";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        // POST: Tasks/ChangeStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, TaskStatus newStatus)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound();
            var userId = GetCurrentUserId();

            if (task.AssignedToId != userId && task.AssignedById != userId && !User.IsInRole("Администратор"))
                return Forbid();

            if (task.Status == TaskStatus.Completed || task.Status == TaskStatus.Cancelled)
            {
                TempData["Error"] = "Нельзя изменить статус завершённой или отменённой задачи";
                return RedirectToAction(nameof(Index));
            }

            if (newStatus == TaskStatus.InProgress && task.DueDate < DateTime.UtcNow)
                task.Status = TaskStatus.InProgress;
            else
                task.Status = newStatus;

            _context.Update(task);
            await _context.SaveChangesAsync();

            var statusDisplay = GetStatusDisplayName(newStatus);
            var message = $"Статус задачи «{task.Title}» изменён на «{statusDisplay}»";
            var url = Url.Action("Details", "Tasks", new { id = task.Id });

            if (task.AssignedById != userId)
                await _hubContext.Clients.User(task.AssignedById.ToString()).SendAsync("ReceiveNotification", message, url);
            if (task.AssignedToId != userId)
                await _hubContext.Clients.User(task.AssignedToId.ToString()).SendAsync("ReceiveNotification", message, url);

            TempData["Success"] = $"Статус задачи изменён на '{GetStatusDisplayName(newStatus)}'";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tasks/Delete/5
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var task = await _context.Tasks
                .Include(t => t.Discipline)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (task == null) return NotFound();
            if (!CanDeleteTask(task)) return Forbid();
            return View(task);
        }

        // POST: Tasks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound();
            if (!CanDeleteTask(task)) return Forbid();

            if (!string.IsNullOrEmpty(task.AttachmentFilePath))
            {
                var filePath = Path.Combine(StorageBasePath, task.AttachmentFilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Задача успешно удалена";
            return RedirectToAction(nameof(Index));
        }

        // GET: Tasks/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var task = await _context.Tasks
                .Include(t => t.Discipline)
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (task == null) return NotFound();

            var userId = GetCurrentUserId();
            if (!User.IsInRole("Администратор") &&
                !User.IsInRole("Председатель ПЦК") &&
                task.AssignedToId != userId &&
                task.AssignedById != userId)
                return Forbid();

            return View(task);
        }

        private async Task UpdateOverdueTasks()
        {
            var overdueTasks = await _context.Tasks
                .Where(t => t.Status != TaskStatus.Completed &&
                            t.Status != TaskStatus.Cancelled &&
                            t.DueDate < DateTime.UtcNow)
                .ToListAsync();
            foreach (var task in overdueTasks)
                task.Status = TaskStatus.Overdue;
            if (overdueTasks.Any())
                await _context.SaveChangesAsync();
        }

        private async Task PopulateDropdowns(TasksCreateViewModel model)
        {
            model.Disciplines = await _context.Disciplines
                .OrderBy(d => d.Code)
                .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = $"{d.Code} - {d.Name}" })
                .ToListAsync();

            model.Teachers = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.FullName })
                .ToListAsync();
        }

        private async Task PopulateDropdowns(TasksEditViewModel model)
        {
            model.Disciplines = await _context.Disciplines
                .OrderBy(d => d.Code)
                .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = $"{d.Code} - {d.Name}" })
                .ToListAsync();

            model.Teachers = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.FullName })
                .ToListAsync();
        }

        private async Task PrepareFilterViewBag(TasksFilterViewModel filter)
        {
            filter.Statuses = Enum.GetValues(typeof(TaskStatus))
                .Cast<TaskStatus>()
                .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = GetStatusDisplayName(s) })
                .ToList();
            filter.Statuses.Insert(0, new SelectListItem { Value = "", Text = "Все статусы" });

            filter.Disciplines = await _context.Disciplines
                .OrderBy(d => d.Code)
                .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = $"{d.Code} - {d.Name}" })
                .ToListAsync();
            filter.Disciplines.Insert(0, new SelectListItem { Value = "", Text = "Все дисциплины" });

            if (User.IsInRole("Администратор") || User.IsInRole("Председатель ПЦК"))
            {
                filter.Teachers = await _context.Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.FullName })
                    .ToListAsync();
                filter.Teachers.Insert(0, new SelectListItem { Value = "", Text = "Все преподаватели" });
            }
            ViewBag.Filter = filter;
        }

        private string GetStatusDisplayName(TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Pending => "Ожидает",
                TaskStatus.InProgress => "В работе",
                TaskStatus.Completed => "Выполнено",
                TaskStatus.Overdue => "Просрочено",
                TaskStatus.Cancelled => "Отменена",
                _ => status.ToString()
            };
        }

        private bool TaskExists(int id) => _context.Tasks.Any(e => e.Id == id);

        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null || string.IsNullOrEmpty(task.AttachmentFilePath))
                return NotFound();

            var filePath = Path.Combine(StorageBasePath, task.AttachmentFilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, "application/octet-stream", task.AttachmentFileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteTask(int id, TaskCompletionViewModel model)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound();

            var userId = GetCurrentUserId();
            if (task.AssignedToId != userId && !User.IsInRole("Администратор") && !User.IsInRole("Председатель ПЦК"))
                return Forbid();

            if (task.Status == TaskStatus.Completed || task.Status == TaskStatus.Cancelled)
            {
                TempData["Error"] = "Задача уже завершена или отменена";
                return RedirectToAction(nameof(Details), new { id });
            }

            task.Status = TaskStatus.Completed;
            task.CompletionComment = model.Comment;
            task.CompletionUrl = model.ResultUrl;

            if (model.AttachmentFile != null && model.AttachmentFile.Length > 0)
            {
                var uploadDir = Path.Combine(StorageBasePath, "uploads", "task_completions");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.AttachmentFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await model.AttachmentFile.CopyToAsync(stream);
                task.CompletionAttachmentFilePath = $"/uploads/task_completions/{fileName}";
                task.CompletionAttachmentFileName = model.AttachmentFile.FileName;
            }

            _context.Update(task);
            await _context.SaveChangesAsync();

            var message = $"Задача «{task.Title}» выполнена исполнителем {User.Identity?.Name}.";
            if (!string.IsNullOrEmpty(model.Comment))
                message += $" Комментарий: {model.Comment}";
            await _hubContext.Clients.User(task.AssignedById.ToString())
                .SendAsync("ReceiveNotification", message, Url.Action("Details", "Tasks", new { id }));

            var creator = await _userManager.FindByIdAsync(task.AssignedById.ToString());
            if (creator != null && !string.IsNullOrEmpty(creator.Email))
            {
                var subject = $"Задача выполнена: {task.Title}";
                var body = $@"
                    <h3>Задача выполнена</h3>
                    <p>Исполнитель <strong>{User.Identity.Name}</strong> завершил задачу <strong>{task.Title}</strong>.</p>
                    <a href='{Url.Action("Details", "Tasks", new { id = task.Id }, "https")}'>Посмотреть задачу</a>
                ";
                await _emailSender.SendEmailAsync(creator.Email, subject, body);
            }

            TempData["Success"] = "Задача отмечена выполненной. Отправлен отчёт постановщику.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadCompletionAttachment(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null || string.IsNullOrEmpty(task.CompletionAttachmentFilePath))
                return NotFound();

            var filePath = Path.Combine(StorageBasePath, task.CompletionAttachmentFilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, "application/octet-stream", task.CompletionAttachmentFileName);
        }
    }
}