using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PCKManagementSystem.Data;
using PCKManagementSystem.Hubs;
using PCKManagementSystem.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace PCKManagementSystem.Controllers
{
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DocumentsController> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserManager<User> _userManager;

        private const string StorageBasePath = "/data/uploads";
        public DocumentsController(ApplicationDbContext context, ILogger<DocumentsController> logger,
            IHubContext<NotificationHub> hubContext, UserManager<User> userManager)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _userManager = userManager;
        }

        // Вспомогательный метод для безопасного получения ID текущего пользователя
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim) : 0;
        }

        // Вспомогательный метод для удаления файла
        private void DeleteFileIfExists(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                var fullPath = Path.Combine(StorageBasePath, filePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
        }

        // GET: Documents
        public async Task<IActionResult> Index(string? statusFilter, int? disciplineId, string? sortOrder, string? searchString)
        {
            var documents = _context.Documents
                .Include(d => d.Discipline)
                .Include(d => d.Author)
                .Include(d => d.ApprovedBy)
                .AsQueryable();

            // Фильтрация по статусу
            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<DocumentStatus>(statusFilter, out var status))
                documents = documents.Where(d => d.Status == status);

            // Фильтрация по дисциплине
            if (disciplineId.HasValue)
                documents = documents.Where(d => d.DisciplineId == disciplineId.Value);

            // Поиск
            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                documents = documents.Where(d => d.Name.ToLower().Contains(searchString) ||
                                                 d.Author.FullName.ToLower().Contains(searchString) ||
                                                 d.Discipline.Name.ToLower().Contains(searchString));
            }

            // Для преподавателя показываем только его документы
            if (User.IsInRole("Преподаватель") && !User.IsInRole("Администратор") && !User.IsInRole("Председатель ПЦК"))
            {
                var userId = GetCurrentUserId();
                documents = documents.Where(d => d.AuthorId == userId);
            }

            // Сортировка
            ViewData["NameSortParam"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParam"] = sortOrder == "date" ? "date_desc" : "date";
            ViewData["StatusSortParam"] = sortOrder == "status" ? "status_desc" : "status";
            ViewData["AuthorSortParam"] = sortOrder == "author" ? "author_desc" : "author";
            ViewData["CurrentSort"] = sortOrder;
            ViewData["CurrentSearch"] = searchString;

            documents = sortOrder switch
            {
                "name_desc" => documents.OrderByDescending(d => d.Name),
                "date" => documents.OrderBy(d => d.CreatedAt),
                "date_desc" => documents.OrderByDescending(d => d.CreatedAt),
                "status" => documents.OrderBy(d => d.Status),
                "status_desc" => documents.OrderByDescending(d => d.Status),
                "author" => documents.OrderBy(d => d.Author.FullName),
                "author_desc" => documents.OrderByDescending(d => d.Author.FullName),
                _ => documents.OrderBy(d => d.Name)
            };

            // Сохраняем фильтры в ViewBag для формы
            ViewBag.Statuses = Enum.GetValues(typeof(DocumentStatus))
                .Cast<DocumentStatus>()
                .Select(s => new SelectListItem
                {
                    Value = s.ToString(),
                    Text = GetStatusDisplayName(s),
                    Selected = s.ToString() == statusFilter
                });
            ViewBag.Disciplines = new SelectList(await _context.Disciplines.ToListAsync(), "Id", "Name", disciplineId);
            ViewBag.StatusFilter = statusFilter;
            ViewBag.DisciplineId = disciplineId;

            return View(await documents.ToListAsync());
        }

        // GET: Documents/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var document = await _context.Documents
                .Include(d => d.Discipline)
                .Include(d => d.Author)
                .Include(d => d.ApprovedBy)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (document == null)
            {
                return NotFound();
            }

            // Проверка прав на просмотр
            var userId = GetCurrentUserId();
            if (!User.IsInRole("Администратор") && !User.IsInRole("Председатель ПЦК") && document.AuthorId != userId)
            {
                return Forbid();
            }

            return View(document);
        }

        // GET: Documents/Create
        [Authorize(Roles = "Администратор,Председатель ПЦК,Преподаватель")]
        public IActionResult Create()
        {
            ViewData["DisciplineId"] = new SelectList(_context.Disciplines, "Id", "Name");
            return View();
        }

        // POST: Documents/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК,Преподаватель")]
        public async Task<IActionResult> Create([Bind("Name,DocumentType,Version,DisciplineId")] Document document, IFormFile? file)
        {
            var userId = GetCurrentUserId();
            document.AuthorId = userId;
            document.CreatedAt = DateTime.UtcNow;
            document.Status = DocumentStatus.Draft;

            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("file", "Необходимо загрузить файл документа");
                ViewData["DisciplineId"] = new SelectList(_context.Disciplines, "Id", "Name", document.DisciplineId);
                return View(document);
            }

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                ModelState.AddModelError("file", "Недопустимый тип файла");
                ViewData["DisciplineId"] = new SelectList(_context.Disciplines, "Id", "Name", document.DisciplineId);
                return View(document);
            }

            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var uploadPath = Path.Combine(StorageBasePath, "uploads", "documents");
            var filePath = Path.Combine(uploadPath, fileName);

            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            document.FilePath = $"/uploads/documents/{fileName}";

            if (!ModelState.IsValid)
            {
                ViewData["DisciplineId"] = new SelectList(_context.Disciplines, "Id", "Name", document.DisciplineId);
                return View(document);
            }

            try
            {
                _context.Add(document);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Документ успешно создан";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                TempData["Error"] = "Ошибка базы данных: " + ex.Message;
                if (ex.InnerException != null)
                    TempData["Error"] += " | " + ex.InnerException.Message;
                ViewData["DisciplineId"] = new SelectList(_context.Disciplines, "Id", "Name", document.DisciplineId);
                return View(document);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ошибка: " + ex.Message;
                ViewData["DisciplineId"] = new SelectList(_context.Disciplines, "Id", "Name", document.DisciplineId);
                return View(document);
            }
        }

        // GET: Documents/Edit/5
        [Authorize(Roles = "Администратор,Председатель ПЦК,Преподаватель")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var document = await _context.Documents.FindAsync(id);
            if (document == null) return NotFound();

            var userId = GetCurrentUserId();
            if (document.Status != DocumentStatus.Draft)
            {
                TempData["Error"] = "Нельзя редактировать документ после отправки на согласование";
                return RedirectToAction(nameof(Index));
            }
            if (document.AuthorId != userId && !User.IsInRole("Администратор"))
                return Forbid();

            ViewData["DisciplineId"] = new SelectList(_context.Disciplines, "Id", "Name", document.DisciplineId);
            return View(document);
        }

        // POST: Documents/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК,Преподаватель")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,DocumentType,Version,DisciplineId")] Document document, IFormFile? file)
        {
            if (id != document.Id) return NotFound();

            var existingDocument = await _context.Documents.FindAsync(id);
            if (existingDocument == null) return NotFound();

            var userId = GetCurrentUserId();
            if (existingDocument.Status != DocumentStatus.Draft)
            {
                TempData["Error"] = "Нельзя редактировать документ после отправки на согласование";
                return RedirectToAction(nameof(Index));
            }
            if (existingDocument.AuthorId != userId && !User.IsInRole("Администратор"))
                return Forbid();

            existingDocument.Name = document.Name;
            existingDocument.DocumentType = document.DocumentType;
            existingDocument.Version = document.Version;
            existingDocument.DisciplineId = document.DisciplineId;

            if (file != null && file.Length > 0)
            {
                if (!string.IsNullOrEmpty(existingDocument.FilePath))
                    DeleteFileIfExists(existingDocument.FilePath);

                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("file", "Недопустимый тип файла");
                    ViewData["DisciplineId"] = new SelectList(_context.Disciplines, "Id", "Name", document.DisciplineId);
                    return View(document);
                }

                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var uploadPath = Path.Combine(StorageBasePath, "uploads", "documents");
                var filePath = Path.Combine(uploadPath, fileName);
                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream);
                existingDocument.FilePath = $"/uploads/documents/{fileName}";
            }

            if (!ModelState.IsValid)
            {
                ViewData["DisciplineId"] = new SelectList(_context.Disciplines, "Id", "Name", document.DisciplineId);
                return View(document);
            }

            try
            {
                _context.Update(existingDocument);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Документ успешно обновлён";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DocumentExists(document.Id))
                    return NotFound();
                throw;
            }
        }

        // POST: Documents/SendToReview/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК,Преподаватель")]
        public async Task<IActionResult> SendToReview(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            // Проверка: только автор может отправить
            var userId = GetCurrentUserId();
            if (document.AuthorId != userId && !User.IsInRole("Администратор"))
            {
                return Forbid();
            }

            if (document.Status != DocumentStatus.Draft)
            {
                TempData["Error"] = "Документ уже отправлен на согласование";
                return RedirectToAction(nameof(Index));
            }

            document.Status = DocumentStatus.Review;
            _context.Update(document);
            await _context.SaveChangesAsync();

            // Получаем всех председателей
            var chairmen = await _userManager.GetUsersInRoleAsync("Председатель ПЦК");
            var message = $"Преподаватель {User.Identity?.Name} отправил документ «{document.Name}» на рассмотрение";
            var url = Url.Action("Details", "Documents", new { id = document.Id });

            foreach (var chairman in chairmen)
            {
                await _hubContext.Clients.User(chairman.Id.ToString())
                    .SendAsync("ReceiveNotification", message, url);
            }

            TempData["Success"] = "Документ отправлен на согласование председателю ПЦК";
            return RedirectToAction(nameof(Index));
        }

        // POST: Documents/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Approve(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            if (document.Status != DocumentStatus.Review)
            {
                TempData["Error"] = "Можно утверждать только документы, находящиеся на рассмотрении";
                return RedirectToAction(nameof(Index));
            }

            var userId = GetCurrentUserId();
            document.Status = DocumentStatus.Approved;
            document.ApprovedAt = DateTime.UtcNow;
            document.ApprovedById = userId;

            _context.Update(document);
            await _context.SaveChangesAsync();

            var message = $"Ваш документ «{document.Name}» утверждён";
            var url = Url.Action("Details", "Documents", new { id = document.Id });

            await _hubContext.Clients.User(document.AuthorId.ToString())
                .SendAsync("ReceiveNotification", message, url);

            TempData["Success"] = "Документ утвержден";
            return RedirectToAction(nameof(Index));
        }

        // POST: Documents/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Reject(int id, string comment)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null) return NotFound();

            if (document.Status != DocumentStatus.Review)
            {
                TempData["Error"] = "Можно отклонить только документы, находящиеся на рассмотрении";
                return RedirectToAction(nameof(Index));
            }

            document.Status = DocumentStatus.Rejected;
            document.RejectionReason = comment;

            _context.Update(document);

            var userId = GetCurrentUserId();
            // Получаем текущего пользователя для FullName
            var currentUser = await _userManager.FindByIdAsync(userId.ToString());
            var userFullName = currentUser?.FullName ?? currentUser?.Email ?? "Unknown";

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var userAgent = Request.Headers["User-Agent"].ToString() ?? "";

            var auditLog = new AuditLog
            {
                UserId = userId,
                UserEmail = User.Identity?.Name ?? "Unknown",
                UserFullName = userFullName,            // ← обязательно заполняем
                ActionType = "Отклонение",
                ActionDescription = $"Отклонен документ '{document.Name}'",
                EntityType = "Document",
                EntityId = document.Id,
                ActionDate = DateTime.UtcNow,
                AdditionalInfo = comment,
                OldValuesJson = "",
                NewValuesJson = "",
                IpAddress = ipAddress,
                UserAgent = userAgent
            };
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            // *** ОТПРАВКА УВЕДОМЛЕНИЯ ***
            // Формируем сообщение и URL
            var message = $"Ваш документ «{document.Name}» отклонён. Причина: {comment}";
            var url = Url.Action("Details", "Documents", new { id = document.Id }); // ссылка на детали документа

            // Отправляем автору документа (AuthorId)
            await _hubContext.Clients.User(document.AuthorId.ToString())
                .SendAsync("ReceiveNotification", message, url);

            TempData["Success"] = "Документ отклонён";
            return RedirectToAction(nameof(Index));
        }

        // GET: Documents/Delete/5
        [Authorize(Roles = "Администратор,Председатель ПЦК,Преподаватель")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var document = await _context.Documents
                .Include(d => d.Discipline)
                .Include(d => d.Author)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (document == null)
            {
                return NotFound();
            }

            // Проверка: удалять можно только черновики и только свой документ
            var userId = GetCurrentUserId();
            if (document.Status != DocumentStatus.Draft)
            {
                TempData["Error"] = "Нельзя удалить документ после отправки на согласование";
                return RedirectToAction(nameof(Index));
            }
            if (document.AuthorId != userId && !User.IsInRole("Администратор"))
            {
                return Forbid();
            }

            return View(document);
        }

        // POST: Documents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК,Преподаватель")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            // Проверка прав
            var userId = GetCurrentUserId();
            if (document.Status != DocumentStatus.Draft)
            {
                TempData["Error"] = "Нельзя удалить документ после отправки на согласование";
                return RedirectToAction(nameof(Index));
            }
            if (document.AuthorId != userId && !User.IsInRole("Администратор"))
            {
                return Forbid();
            }

            // Удаляем файл
            DeleteFileIfExists(document.FilePath);

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Документ успешно удален";
            return RedirectToAction(nameof(Index));
        }

        // GET: Documents/Download/5
        public async Task<IActionResult> Download(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null || string.IsNullOrEmpty(document.FilePath))
                return NotFound();

            var filePath = Path.Combine(StorageBasePath, document.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var fileName = Path.GetFileName(document.FilePath);
            return File(fileBytes, "application/octet-stream", $"{document.Name}_{document.Version}{Path.GetExtension(fileName)}");
        }

        private bool DocumentExists(int id)
        {
            return _context.Documents.Any(e => e.Id == id);
        }

        private string GetStatusDisplayName(DocumentStatus status)
        {
            return status switch
            {
                DocumentStatus.Draft => "Черновик",
                DocumentStatus.Review => "На рассмотрении",
                DocumentStatus.Approved => "Утвержден",
                DocumentStatus.Rejected => "Отклонен",
                _ => status.ToString()
            };
        }
    }
}