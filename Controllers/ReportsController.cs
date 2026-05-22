using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PCKManagementSystem.Data;
using PCKManagementSystem.Models;
using PCKManagementSystem.Models.ViewModels;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using BorderValues = DocumentFormat.OpenXml.Wordprocessing.BorderValues;
using JustificationValues = DocumentFormat.OpenXml.Wordprocessing.JustificationValues;
using Bold = DocumentFormat.OpenXml.Wordprocessing.Bold;

namespace PCKManagementSystem.Controllers
{
    [Authorize(Roles = "Администратор,Председатель ПЦК")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<ReportsController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // Вспомогательный метод для получения ID текущего пользователя
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return !string.IsNullOrEmpty(userIdClaim) ? int.Parse(userIdClaim) : 0;
        }

        // GET: Reports
        public async Task<IActionResult> Index(string searchString, string reportType, string format, DateTime? startDate, DateTime? endDate, string sortOrder)
        {
            var query = _context.Reports
                .Include(r => r.CreatedBy)
                .AsQueryable();

            // Поиск по названию
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(r => r.Title.Contains(searchString));
            }

            // Фильтр по типу отчёта
            if (!string.IsNullOrEmpty(reportType))
            {
                query = query.Where(r => r.ReportType == reportType);
            }

            // Фильтр по формату
            if (!string.IsNullOrEmpty(format))
            {
                query = query.Where(r => r.Format == format);
            }

            // Фильтр по дате создания
            if (startDate.HasValue)
            {
                query = query.Where(r => r.CreatedAt >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                var end = endDate.Value.AddDays(1);
                query = query.Where(r => r.CreatedAt <= end);
            }

            // Сортировка
            ViewData["CurrentSort"] = sortOrder;
            ViewData["TitleSortParm"] = sortOrder == "title" ? "title_desc" : "title";
            ViewData["DateSortParm"] = sortOrder == "date" ? "date_desc" : "date";
            ViewData["TypeSortParm"] = sortOrder == "type" ? "type_desc" : "type";
            ViewData["FormatSortParm"] = sortOrder == "format" ? "format_desc" : "format";
            ViewData["AuthorSortParm"] = sortOrder == "author" ? "author_desc" : "author";

            switch (sortOrder)
            {
                case "title":
                    query = query.OrderBy(r => r.Title);
                    break;
                case "title_desc":
                    query = query.OrderByDescending(r => r.Title);
                    break;
                case "date":
                    query = query.OrderBy(r => r.CreatedAt);
                    break;
                case "date_desc":
                    query = query.OrderByDescending(r => r.CreatedAt);
                    break;
                case "type":
                    query = query.OrderBy(r => r.ReportType);
                    break;
                case "type_desc":
                    query = query.OrderByDescending(r => r.ReportType);
                    break;
                case "format":
                    query = query.OrderBy(r => r.Format);
                    break;
                case "format_desc":
                    query = query.OrderByDescending(r => r.Format);
                    break;
                case "author":
                    query = query.OrderBy(r => r.CreatedBy.FullName);
                    break;
                case "author_desc":
                    query = query.OrderByDescending(r => r.CreatedBy.FullName);
                    break;
                default:
                    query = query.OrderByDescending(r => r.CreatedAt);
                    break;
            }

            var reports = await query
                .Select(r => new SavedReportViewModel
                {
                    Id = r.Id,
                    Title = r.Title,
                    ReportType = r.ReportType,
                    CreatedAt = r.CreatedAt,
                    CreatedBy = r.CreatedBy.FullName ?? r.CreatedBy.Email,
                    Format = r.Format,
                    FilePath = r.FilePath ?? string.Empty
                })
                .ToListAsync();

            // Подготовка данных для фильтров (списки типов и форматов)
            ViewBag.ReportTypes = await _context.Reports
                .Select(r => r.ReportType)
                .Distinct()
                .ToListAsync();
            ViewBag.Formats = await _context.Reports
                .Select(r => r.Format)
                .Distinct()
                .ToListAsync();

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentReportType"] = reportType;
            ViewData["CurrentFormat"] = format;
            ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
            ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");

            return View(reports);
        }

        // GET: Reports/Create
        public async Task<IActionResult> Create()
        {
            var model = new ReportParameterViewModel();
            await PopulateDropdowns(model);
            return View(model);
        }

        // POST: Reports/Generate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate(ReportParameterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View("Create", model);
            }

            try
            {
                // Генерируем отчет в зависимости от типа
                switch (model.ReportType)
                {
                    case ReportType.Documents:
                        return await GenerateDocumentsReport(model);
                    case ReportType.Workload:
                        return await GenerateWorkloadReport(model);
                    case ReportType.Tasks:
                        return await GenerateTasksReport(model);
                    case ReportType.Activity:
                        return await GenerateActivityReport(model);
                    default:
                        TempData["Error"] = "Неизвестный тип отчета";
                        return RedirectToAction(nameof(Create));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации отчета");
                TempData["Error"] = "Произошла ошибка при генерации отчета";
                return RedirectToAction(nameof(Create));
            }
        }

        // GET: Reports/ViewReport/5
        public async Task<IActionResult> ViewReport(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports.FindAsync(id);
            if (report == null) return NotFound();

            // Если формат не HTML, то лучше скачать файл
            if (report.Format != "HTML")
            {
                if (!string.IsNullOrEmpty(report.FilePath))
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, report.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                        var mimeType = report.Format switch
                        {
                            "Excel" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            "CSV" => "text/csv",
                            "PDF" => "application/pdf",
                            "Word" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            _ => "application/octet-stream"
                        };
                        return File(fileBytes, mimeType, $"{report.Title}.{report.Format.ToLower()}");
                    }
                }
                TempData["Error"] = "Файл отчета не найден";
                return RedirectToAction(nameof(Index));
            }

            // Для HTML – десериализуем данные отчета и показываем
            if (string.IsNullOrEmpty(report.ReportDataJson))
            {
                TempData["Error"] = "Данные отчета не сохранены";
                return RedirectToAction(nameof(Index));
            }

            object reportData = null;
            switch (report.ReportType)
            {
                case "документам":
                    reportData = JsonSerializer.Deserialize<DocumentsReportViewModel>(report.ReportDataJson);
                    break;
                case "нагрузке":
                    reportData = JsonSerializer.Deserialize<WorkloadReportViewModel>(report.ReportDataJson);
                    break;
                case "задачам":
                    reportData = JsonSerializer.Deserialize<TasksReportViewModel>(report.ReportDataJson);
                    break;
                case "деятельности":
                    reportData = JsonSerializer.Deserialize<ActivityReportViewModel>(report.ReportDataJson);
                    break;
                default:
                    TempData["Error"] = "Неизвестный тип отчета";
                    return RedirectToAction(nameof(Index));
            }

            ViewBag.ReportId = report.Id;
            return View("ReportResult", reportData);
        }

        #region Генерация отчетов

        private async Task<IActionResult> GenerateDocumentsReport(ReportParameterViewModel model)
        {
            // Заполняем отображаемые поля
            if (model.TeacherId.HasValue)
            {
                var teacher = await _context.Users.FindAsync(model.TeacherId.Value);
                model.TeacherName = teacher?.FullName ?? teacher?.Email;
            }
            if (model.DisciplineId.HasValue)
            {
                var discipline = await _context.Disciplines.FindAsync(model.DisciplineId.Value);
                model.DisciplineName = discipline?.Name;
            }
            if (model.DocumentStatus.HasValue)
            {
                model.DocumentStatusName = GetDocumentStatusDisplay(model.DocumentStatus.Value);
            }
            model.PeriodDisplay = (model.StartDate.HasValue || model.EndDate.HasValue)
                ? $"с {model.StartDate?.ToString("dd.MM.yyyy") ?? "∞"} по {model.EndDate?.ToString("dd.MM.yyyy") ?? "∞"}"
                : "Не указан";
            // Базовый запрос
            var query = _context.Documents
                .Include(d => d.Author)
                .Include(d => d.Discipline)
                .AsQueryable();

            // Применяем фильтры
            if (model.StartDate.HasValue)
                query = query.Where(d => d.CreatedAt >= model.StartDate.Value);

            if (model.EndDate.HasValue)
                query = query.Where(d => d.CreatedAt <= model.EndDate.Value);

            if (model.DisciplineId.HasValue)
                query = query.Where(d => d.DisciplineId == model.DisciplineId.Value);

            if (model.TeacherId.HasValue)
                query = query.Where(d => d.AuthorId == model.TeacherId.Value);

            if (model.DocumentStatus.HasValue)
                query = query.Where(d => d.Status == model.DocumentStatus.Value);

            var documents = await query.ToListAsync();

            // Загружаем все дисциплины (этот запрос выполнится в БД)
            var disciplines = await _context.Disciplines.ToListAsync();

            // Формируем отчет
            var report = new DocumentsReportViewModel
            {
                GeneratedAt = DateTime.UtcNow,
                Parameters = GetParametersDisplay(model),

                // Общая статистика
                TotalDocuments = documents.Count,
                DraftDocuments = documents.Count(d => d.Status == DocumentStatus.Draft),
                ReviewDocuments = documents.Count(d => d.Status == DocumentStatus.Review),
                ApprovedDocuments = documents.Count(d => d.Status == DocumentStatus.Approved),
                RejectedDocuments = documents.Count(d => d.Status == DocumentStatus.Rejected),

                // Статистика по статусам
                DocumentsByStatus = new Dictionary<string, int>
                {
                    ["Черновики"] = documents.Count(d => d.Status == DocumentStatus.Draft),
                    ["На рассмотрении"] = documents.Count(d => d.Status == DocumentStatus.Review),
                    ["Утвержденные"] = documents.Count(d => d.Status == DocumentStatus.Approved),
                    ["Отклоненные"] = documents.Count(d => d.Status == DocumentStatus.Rejected)
                },

                // По дисциплинам (теперь всё вычисляется в памяти)
                DocumentsByDiscipline = disciplines
                    .Select(d => new DocumentsByDisciplineViewModel
                    {
                        DisciplineName = d.Name,
                        DisciplineCode = d.Code,
                        TotalDocuments = documents.Count(doc => doc.DisciplineId == d.Id),
                        ApprovedDocuments = documents.Count(doc => doc.DisciplineId == d.Id && doc.Status == DocumentStatus.Approved),
                        DraftDocuments = documents.Count(doc => doc.DisciplineId == d.Id && doc.Status == DocumentStatus.Draft)
                    })
                    .Where(d => d.TotalDocuments > 0)
                    .ToList(),

                // По авторам
                DocumentsByAuthor = documents
                    .GroupBy(d => d.AuthorId)
                    .Select(g => new DocumentsByAuthorViewModel
                    {
                        AuthorName = g.First().Author?.FullName ?? "Неизвестно",
                        TotalDocuments = g.Count(),
                        ApprovedDocuments = g.Count(d => d.Status == DocumentStatus.Approved),
                        DraftDocuments = g.Count(d => d.Status == DocumentStatus.Draft)
                    })
                    .ToList(),

                // Последние документы
                RecentDocuments = documents
                    .OrderByDescending(d => d.CreatedAt)
                    .Take(10)
                    .Select(d => new DocumentItemViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        DocumentType = d.DocumentType,
                        Version = d.Version,
                        CreatedAt = d.CreatedAt,
                        AuthorName = d.Author?.FullName ?? "Неизвестно",
                        DisciplineName = d.Discipline?.Name ?? "Не указано",
                        Status = GetDocumentStatusDisplay(d.Status)
                    })
                    .ToList()
            };

            return await ExportReport(report, model.ReportFormat, "Documents", model); 
        }

        private async Task<IActionResult> GenerateWorkloadReport(ReportParameterViewModel model)
        {
            // Заполняем отображаемые поля
            if (model.TeacherId.HasValue)
            {
                var teacher = await _context.Users.FindAsync(model.TeacherId.Value);
                model.TeacherName = teacher?.FullName ?? teacher?.Email;
            }
            if (model.DisciplineId.HasValue)
            {
                var discipline = await _context.Disciplines.FindAsync(model.DisciplineId.Value);
                model.DisciplineName = discipline?.Name;
            }
            if (model.DocumentStatus.HasValue)
            {
                model.DocumentStatusName = GetDocumentStatusDisplay(model.DocumentStatus.Value);
            }
            model.PeriodDisplay = (model.StartDate.HasValue || model.EndDate.HasValue)
                ? $"с {model.StartDate?.ToString("dd.MM.yyyy") ?? "∞"} по {model.EndDate?.ToString("dd.MM.yyyy") ?? "∞"}"
                : "Не указан";
            // Базовый запрос
            var query = _context.Workloads
                .Include(w => w.Teacher)
                .Include(w => w.Discipline)
                .AsQueryable();

            // Применяем фильтры
            if (!string.IsNullOrEmpty(model.AcademicYear))
                query = query.Where(w => w.AcademicYear == model.AcademicYear);

            if (model.Semester.HasValue)
                query = query.Where(w => w.Semester == model.Semester.Value);

            if (model.TeacherId.HasValue)
                query = query.Where(w => w.TeacherId == model.TeacherId.Value);

            if (model.DisciplineId.HasValue)
                query = query.Where(w => w.DisciplineId == model.DisciplineId.Value);

            var workloads = await query.ToListAsync();

            // Формируем отчет
            var report = new WorkloadReportViewModel
            {
                GeneratedAt = DateTime.UtcNow,
                Parameters = GetParametersDisplay(model),

                // Общая статистика
                TotalTeachers = workloads.Select(w => w.TeacherId).Distinct().Count(),
                TotalDisciplines = workloads.Select(w => w.DisciplineId).Distinct().Count(),
                TotalHours = workloads.Sum(w => w.Hours),
                TotalGroups = workloads.Sum(w => w.GroupsCount),

                // Статистика по типам нагрузки
                HoursByLoadType = workloads
                    .GroupBy(w => w.LoadType)
                    .ToDictionary(g => g.Key, g => g.Sum(w => w.Hours)),

                // По преподавателям
                WorkloadByTeacher = workloads
                    .GroupBy(w => w.TeacherId)
                    .Select(g => new WorkloadByTeacherViewModel
                    {
                        TeacherName = g.First().Teacher?.FullName ?? "Неизвестно",
                        TotalHours = g.Sum(w => w.Hours),
                        DisciplinesCount = g.Select(w => w.DisciplineId).Distinct().Count(),
                        HoursByType = g.GroupBy(w => w.LoadType)
                                      .ToDictionary(tg => tg.Key, tg => tg.Sum(w => w.Hours))
                    })
                    .OrderByDescending(w => w.TotalHours)
                    .ToList(),

                // По дисциплинам
                WorkloadByDiscipline = workloads
                    .GroupBy(w => w.DisciplineId)
                    .Select(g => new WorkloadByDisciplineViewModel
                    {
                        DisciplineName = g.First().Discipline?.Name ?? "Неизвестно",
                        DisciplineCode = g.First().Discipline?.Code ?? "",
                        TotalHours = g.Sum(w => w.Hours),
                        TeachersCount = g.Select(w => w.TeacherId).Distinct().Count()
                    })
                    .OrderByDescending(w => w.TotalHours)
                    .ToList(),

                // Детальная нагрузка
                WorkloadDetails = workloads
                    .Select(w => new WorkloadItemViewModel
                    {
                        Id = w.Id,
                        TeacherName = w.Teacher?.FullName ?? "Неизвестно",
                        DisciplineName = w.Discipline?.Name ?? "Неизвестно",
                        DisciplineCode = w.Discipline?.Code ?? "",
                        AcademicYear = w.AcademicYear,
                        Semester = w.Semester,
                        Hours = w.Hours,
                        LoadType = w.LoadType,
                        GroupsCount = w.GroupsCount,
                        Comments = w.Comments ?? "",
                        ControlType = w.ControlType ?? ""
                    })
                    .ToList()
            };

            return await ExportReport(report, model.ReportFormat, "Workload", model);
        }

        private async Task<IActionResult> GenerateTasksReport(ReportParameterViewModel model)
        {
            // Заполняем отображаемые поля
            if (model.TeacherId.HasValue)
            {
                var teacher = await _context.Users.FindAsync(model.TeacherId.Value);
                model.TeacherName = teacher?.FullName ?? teacher?.Email;
            }
            if (model.DisciplineId.HasValue)
            {
                var discipline = await _context.Disciplines.FindAsync(model.DisciplineId.Value);
                model.DisciplineName = discipline?.Name;
            }
            if (model.DocumentStatus.HasValue)
            {
                model.DocumentStatusName = GetDocumentStatusDisplay(model.DocumentStatus.Value);
            }
            model.PeriodDisplay = (model.StartDate.HasValue || model.EndDate.HasValue)
                ? $"с {model.StartDate?.ToString("dd.MM.yyyy") ?? "∞"} по {model.EndDate?.ToString("dd.MM.yyyy") ?? "∞"}"
                : "Не указан";
            // Базовый запрос
            var query = _context.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .Include(t => t.Discipline)
                .AsQueryable();

            // Применяем фильтры
            if (model.StartDate.HasValue)
                query = query.Where(t => t.DueDate >= model.StartDate.Value ||
                                        (t.Status == Models.TaskStatus.Completed && t.DueDate >= model.StartDate.Value));

            if (model.EndDate.HasValue)
                query = query.Where(t => t.DueDate <= model.EndDate.Value);

            if (model.TeacherId.HasValue)
                query = query.Where(t => t.AssignedToId == model.TeacherId.Value ||
                                        t.AssignedById == model.TeacherId.Value);

            if (model.DisciplineId.HasValue)
                query = query.Where(t => t.DisciplineId == model.DisciplineId.Value);

            var tasks = await query.ToListAsync();

            // Обновляем статус просроченных задач
            var overdueTasks = tasks.Where(t => t.Status != Models.TaskStatus.Completed &&
                                                t.Status != Models.TaskStatus.Cancelled &&
                                                t.DueDate < DateTime.UtcNow).ToList();

            foreach (var task in overdueTasks)
            {
                task.Status = Models.TaskStatus.Overdue;
            }

            if (overdueTasks.Any())
            {
                _context.UpdateRange(overdueTasks);
                await _context.SaveChangesAsync();
            }

            // Формируем отчет
            var report = new TasksReportViewModel
            {
                GeneratedAt = DateTime.UtcNow,
                Parameters = GetParametersDisplay(model),

                // Общая статистика
                TotalTasks = tasks.Count,
                PendingTasks = tasks.Count(t => t.Status == Models.TaskStatus.Pending),
                InProgressTasks = tasks.Count(t => t.Status == Models.TaskStatus.InProgress),
                CompletedTasks = tasks.Count(t => t.Status == Models.TaskStatus.Completed),
                OverdueTasks = tasks.Count(t => t.Status == Models.TaskStatus.Overdue),
                CancelledTasks = tasks.Count(t => t.Status == Models.TaskStatus.Cancelled),

                // Статистика по статусам
                TasksByStatus = new Dictionary<string, int>
                {
                    ["Ожидают"] = tasks.Count(t => t.Status == Models.TaskStatus.Pending),
                    ["В работе"] = tasks.Count(t => t.Status == Models.TaskStatus.InProgress),
                    ["Выполнены"] = tasks.Count(t => t.Status == Models.TaskStatus.Completed),
                    ["Просрочены"] = tasks.Count(t => t.Status == Models.TaskStatus.Overdue),
                    ["Отменены"] = tasks.Count(t => t.Status == Models.TaskStatus.Cancelled)
                },

                // По исполнителям
                TasksByExecutor = tasks
                    .GroupBy(t => t.AssignedToId)
                    .Select(g => new TasksByExecutorViewModel
                    {
                        ExecutorName = g.First().AssignedTo?.FullName ?? "Неизвестно",
                        TotalTasks = g.Count(),
                        CompletedTasks = g.Count(t => t.Status == Models.TaskStatus.Completed),
                        OverdueTasks = g.Count(t => t.Status == Models.TaskStatus.Overdue)
                    })
                    .OrderByDescending(t => t.TotalTasks)
                    .ToList(),

                // Просроченные задачи
                OverdueTasksList = tasks
                    .Where(t => t.Status == Models.TaskStatus.Overdue)
                    .OrderBy(t => t.DueDate)
                    .Select(t => new TaskItemViewModel
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Description = t.Description ?? "",
                        DueDate = t.DueDate,
                        Status = GetTaskStatusDisplay(t.Status),
                        ExecutorName = t.AssignedTo?.FullName ?? "Неизвестно",
                        CreatorName = t.AssignedBy?.FullName ?? "Система",
                        DisciplineName = t.Discipline?.Name ?? "Не указано"
                    })
                    .ToList(),

                // Последние задачи
                RecentTasks = tasks
                    .OrderByDescending(t => t.DueDate)
                    .Take(10)
                    .Select(t => new TaskItemViewModel
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Description = t.Description ?? "",
                        DueDate = t.DueDate,
                        Status = GetTaskStatusDisplay(t.Status),
                        ExecutorName = t.AssignedTo?.FullName ?? "Неизвестно",
                        CreatorName = t.AssignedBy?.FullName ?? "Система",
                        DisciplineName = t.Discipline?.Name ?? "Не указано"
                    })
                    .ToList()
            };

            return await ExportReport(report, model.ReportFormat, "Tasks", model);
        }

        private async Task<IActionResult> GenerateActivityReport(ReportParameterViewModel model)
        {
            // Заполняем отображаемые поля
            if (model.TeacherId.HasValue)
            {
                var teacher = await _context.Users.FindAsync(model.TeacherId.Value);
                model.TeacherName = teacher?.FullName ?? teacher?.Email;
            }
            if (model.DisciplineId.HasValue)
            {
                var discipline = await _context.Disciplines.FindAsync(model.DisciplineId.Value);
                model.DisciplineName = discipline?.Name;
            }
            if (model.DocumentStatus.HasValue)
            {
                model.DocumentStatusName = GetDocumentStatusDisplay(model.DocumentStatus.Value);
            }
            model.PeriodDisplay = (model.StartDate.HasValue || model.EndDate.HasValue)
                ? $"с {model.StartDate?.ToString("dd.MM.yyyy") ?? "∞"} по {model.EndDate?.ToString("dd.MM.yyyy") ?? "∞"}"
                : "Не указан";
            // Устанавливаем период по умолчанию (текущий учебный год/семестр)
            var startDate = model.StartDate ?? new DateTime(DateTime.UtcNow.Year, 9, 1); // Начало учебного года
            var endDate = model.EndDate ?? DateTime.UtcNow;

            // Получаем данные за период
            var documents = await _context.Documents
                .Include(d => d.Author)
                .Where(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                .ToListAsync();

            var tasks = await _context.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.AssignedBy)
                .Where(t => t.DueDate >= startDate && t.DueDate <= endDate)
                .ToListAsync();

            var workloads = await _context.Workloads
                .Include(w => w.Teacher)
                .Where(w => w.CreatedAt >= startDate && w.CreatedAt <= endDate)
                .ToListAsync();

            // Получаем всех преподавателей
            var teachers = await _context.Users
                .Where(u => u.IsActive)
                .ToListAsync();

            // Формируем отчет
            var report = new ActivityReportViewModel
            {
                GeneratedAt = DateTime.UtcNow,
                Period = $"{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}",
                Parameters = GetParametersDisplay(model),

                // Сводная статистика
                TotalDocuments = documents.Count,
                ApprovedDocuments = documents.Count(d => d.Status == DocumentStatus.Approved),
                TotalWorkloadHours = workloads.Sum(w => w.Hours),
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(t => t.Status == Models.TaskStatus.Completed),

                // Активность преподавателей
                TeachersActivity = teachers.Select(t => new TeacherActivityViewModel
                {
                    TeacherName = t.FullName ?? t.Email,
                    DocumentsCreated = documents.Count(d => d.AuthorId == t.Id),
                    DocumentsApproved = documents.Count(d => d.ApprovedById == t.Id),
                    WorkloadHours = workloads.Where(w => w.TeacherId == t.Id).Sum(w => w.Hours),
                    TasksAssigned = tasks.Count(ts => ts.AssignedById == t.Id),
                    TasksCompleted = tasks.Count(ts => ts.AssignedToId == t.Id && ts.Status == Models.TaskStatus.Completed)
                })
                .OrderByDescending(t => t.DocumentsCreated + t.TasksAssigned)
                .ToList(),

                // Динамика по месяцам
                MonthlyActivity = Enumerable.Range(0, 12)
                    .Select(i => startDate.AddMonths(i))
                    .Where(d => d <= endDate)
                    .Select(month => new MonthlyActivityViewModel
                    {
                        Month = GetMonthName(month.Month),
                        Year = month.Year,
                        DocumentsCreated = documents.Count(d => d.CreatedAt.Year == month.Year && d.CreatedAt.Month == month.Month),
                        TasksCreated = tasks.Count(t => t.DueDate.Year == month.Year && t.DueDate.Month == month.Month),
                        TasksCompleted = tasks.Count(t => t.Status == Models.TaskStatus.Completed &&
                                                         t.DueDate.Year == month.Year &&
                                                         t.DueDate.Month == month.Month)
                    })
                    .ToList()
            };

            return await ExportReport(report, model.ReportFormat, "Activity", model);
        }

        #endregion

        #region Экспорт отчетов

        private async Task<IActionResult> ExportReport(object report, ReportFormat format, string reportType, ReportParameterViewModel parameters)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                _logger.LogError("ExportReport: userId = 0, пользователь не аутентифицирован");
                TempData["Error"] = "Ошибка аутентификации. Попробуйте выйти и войти снова.";
                return RedirectToAction(nameof(Create));
            }

            var savedReport = new Report
            {
                Title = $"Отчет по {GetReportTypeName(reportType)} от {DateTime.UtcNow:dd.MM.yyyy HH:mm}",
                ReportType = GetReportTypeName(reportType),
                Period = DateTime.UtcNow.ToString("yyyy-MM"),
                CreatedById = userId,
                Format = format.ToString(),
                Status = ReportStatus.Generated,
                ParametersJson = JsonSerializer.Serialize(parameters)   // сохраняем параметры
            };

            // Для HTML сохраняем данные отчета в JSON
            if (format == ReportFormat.HTML)
            {
                savedReport.ReportDataJson = JsonSerializer.Serialize(report);
            }

            _context.Reports.Add(savedReport);
            await _context.SaveChangesAsync();

            // Для Excel и CSV и PDF/Word – генерируем файл и сохраняем путь
            if (format != ReportFormat.HTML)
            {
                byte[] fileBytes = null;
                string fileExtension = "";
                string mimeType = "";

                switch (format)
                {
                    case ReportFormat.Excel:
                        fileBytes = GenerateExcelReport(report, reportType);
                        fileExtension = "xlsx";
                        mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        break;
                    case ReportFormat.CSV:
                        fileBytes = GenerateCsvReport(report, reportType);
                        fileExtension = "csv";
                        mimeType = "text/csv";
                        break;
                    case ReportFormat.PDF:
                        fileBytes = GeneratePdfReport(report, reportType);
                        fileExtension = "pdf";
                        mimeType = "application/pdf";
                        break;
                    case ReportFormat.Word:
                        fileBytes = GenerateWordReport(report, reportType);
                        fileExtension = "docx";
                        mimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        break;
                }

                if (fileBytes != null)
                {
                    // Сохраняем файл на диск
                    var fileName = $"Report_{reportType}_{savedReport.Id}_{DateTime.UtcNow:yyyyMMdd_HHmm}.{fileExtension}";
                    var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "reports");
                    if (!Directory.Exists(uploadPath))
                        Directory.CreateDirectory(uploadPath);

                    var filePath = Path.Combine(uploadPath, fileName);
                    await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

                    savedReport.FilePath = $"/reports/{fileName}";
                    _context.Update(savedReport);
                    await _context.SaveChangesAsync();

                    // Возвращаем файл для скачивания
                    return File(fileBytes, mimeType, fileName);
                }
            }

            // Для HTML возвращаем представление с данными отчета, передавая ID отчета
            ViewBag.ReportId = savedReport.Id;
            return View("ReportResult", report);
        }

        private byte[] GenerateExcelReport(object report, string reportType)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Отчет");

                switch (report)
                {
                    case DocumentsReportViewModel docReport:
                        FillDocumentsExcel(worksheet, docReport);
                        break;
                    case WorkloadReportViewModel workloadReport:
                        FillWorkloadExcel(worksheet, workloadReport);
                        break;
                    case TasksReportViewModel tasksReport:
                        FillTasksExcel(worksheet, tasksReport);
                        break;
                    case ActivityReportViewModel activityReport:
                        FillActivityExcel(worksheet, activityReport);
                        break;
                }
                if (worksheet.Dimension != null)
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                return package.GetAsByteArray();
            }
        }

        private void FillDocumentsExcel(ExcelWorksheet ws, DocumentsReportViewModel report)
        {
            int row = 1;
            ws.Cells[row, 1].Value = "ОТЧЕТ ПО ДОКУМЕНТАМ";
            ws.Cells[row, 1, row, 6].Merge = true;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 14;
            row += 2;

            ws.Cells[row, 1].Value = "Сгенерирован:";
            ws.Cells[row, 2].Value = report.GeneratedAt.ToString("dd.MM.yyyy HH:mm");
            row += 2;

            // Общая статистика
            ws.Cells[row, 1].Value = "Общая статистика";
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
            ws.Cells[row, 1].Value = "Всего документов";
            ws.Cells[row, 2].Value = report.TotalDocuments;
            row++;
            ws.Cells[row, 1].Value = "Черновики";
            ws.Cells[row, 2].Value = report.DraftDocuments;
            row++;
            ws.Cells[row, 1].Value = "На рассмотрении";
            ws.Cells[row, 2].Value = report.ReviewDocuments;
            row++;
            ws.Cells[row, 1].Value = "Утверждено";
            ws.Cells[row, 2].Value = report.ApprovedDocuments;
            row++;
            ws.Cells[row, 1].Value = "Отклонено";
            ws.Cells[row, 2].Value = report.RejectedDocuments;
            row += 2;

            // Детальный список
            ws.Cells[row, 1].Value = "Последние документы";
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
            ws.Cells[row, 1].Value = "Название";
            ws.Cells[row, 2].Value = "Тип";
            ws.Cells[row, 3].Value = "Автор";
            ws.Cells[row, 4].Value = "Дисциплина";
            ws.Cells[row, 5].Value = "Дата";
            ws.Cells[row, 6].Value = "Статус";
            row++;

            foreach (var doc in report.RecentDocuments)
            {
                ws.Cells[row, 1].Value = doc.Name;
                ws.Cells[row, 2].Value = doc.DocumentType;
                ws.Cells[row, 3].Value = doc.AuthorName;
                ws.Cells[row, 4].Value = doc.DisciplineName;
                ws.Cells[row, 5].Value = doc.CreatedAt.ToString("dd.MM.yyyy");
                ws.Cells[row, 6].Value = doc.Status;
                row++;
            }
        }

        private void FillWorkloadExcel(ExcelWorksheet ws, WorkloadReportViewModel report)
        {
            int row = 1;
            ws.Cells[row, 1].Value = "ОТЧЕТ ПО УЧЕБНОЙ НАГРУЗКЕ";
            ws.Cells[row, 1, row, 5].Merge = true;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 14;
            row += 2;

            ws.Cells[row, 1].Value = "Сгенерирован:";
            ws.Cells[row, 2].Value = report.GeneratedAt.ToString("dd.MM.yyyy HH:mm");
            row += 2;

            ws.Cells[row, 1].Value = "Всего преподавателей";
            ws.Cells[row, 2].Value = report.TotalTeachers;
            row++;
            ws.Cells[row, 1].Value = "Всего дисциплин";
            ws.Cells[row, 2].Value = report.TotalDisciplines;
            row++;
            ws.Cells[row, 1].Value = "Всего часов";
            ws.Cells[row, 2].Value = report.TotalHours;
            row++;
            ws.Cells[row, 1].Value = "Всего групп";
            ws.Cells[row, 2].Value = report.TotalGroups;
            row += 2;

            // Детальная нагрузка
            ws.Cells[row, 1].Value = "Детальная нагрузка";
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
            ws.Cells[row, 1].Value = "Преподаватель";
            ws.Cells[row, 2].Value = "Дисциплина";
            ws.Cells[row, 3].Value = "Уч.год";
            ws.Cells[row, 4].Value = "Сем.";
            ws.Cells[row, 5].Value = "Часы";
            ws.Cells[row, 6].Value = "Тип"; 
            ws.Cells[row, 7].Value = "Форма контроля";  
            ws.Cells[row, 8].Value = "Групп";
            row++;

            foreach (var item in report.WorkloadDetails)
            {
                ws.Cells[row, 1].Value = item.TeacherName;
                ws.Cells[row, 2].Value = item.DisciplineCode + " - " + item.DisciplineName;
                ws.Cells[row, 3].Value = item.AcademicYear;
                ws.Cells[row, 4].Value = item.Semester;
                ws.Cells[row, 5].Value = item.Hours;
                ws.Cells[row, 6].Value = item.LoadType;
                ws.Cells[row, 7].Value = item.ControlType; 
                ws.Cells[row, 8].Value = item.GroupsCount;
                row++;
            }
        }

        private void FillTasksExcel(ExcelWorksheet ws, TasksReportViewModel report)
        {
            int row = 1;
            ws.Cells[row, 1].Value = "ОТЧЕТ ПО ЗАДАЧАМ";
            ws.Cells[row, 1, row, 6].Merge = true;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 14;
            row += 2;

            ws.Cells[row, 1].Value = "Сгенерирован:";
            ws.Cells[row, 2].Value = report.GeneratedAt.ToString("dd.MM.yyyy HH:mm");
            row += 2;

            ws.Cells[row, 1].Value = "Всего задач";
            ws.Cells[row, 2].Value = report.TotalTasks;
            row++;
            ws.Cells[row, 1].Value = "Ожидают";
            ws.Cells[row, 2].Value = report.PendingTasks;
            row++;
            ws.Cells[row, 1].Value = "В работе";
            ws.Cells[row, 2].Value = report.InProgressTasks;
            row++;
            ws.Cells[row, 1].Value = "Выполнено";
            ws.Cells[row, 2].Value = report.CompletedTasks;
            row++;
            ws.Cells[row, 1].Value = "Просрочено";
            ws.Cells[row, 2].Value = report.OverdueTasks;
            row++;
            ws.Cells[row, 1].Value = "Отменено";
            ws.Cells[row, 2].Value = report.CancelledTasks;
            row += 2;

            // Просроченные задачи
            if (report.OverdueTasksList.Any())
            {
                ws.Cells[row, 1].Value = "ПРОСРОЧЕННЫЕ ЗАДАЧИ";
                ws.Cells[row, 1].Style.Font.Bold = true;
                ws.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                row++;
                ws.Cells[row, 1].Value = "Задача";
                ws.Cells[row, 2].Value = "Исполнитель";
                ws.Cells[row, 3].Value = "Дисциплина";
                ws.Cells[row, 4].Value = "Срок";
                ws.Cells[row, 5].Value = "Постановщик";
                row++;
                foreach (var task in report.OverdueTasksList)
                {
                    ws.Cells[row, 1].Value = task.Title;
                    ws.Cells[row, 2].Value = task.ExecutorName;
                    ws.Cells[row, 3].Value = task.DisciplineName;
                    ws.Cells[row, 4].Value = task.DueDate.ToString("dd.MM.yyyy");
                    ws.Cells[row, 5].Value = task.CreatorName;
                    row++;
                }
                row += 2;
            }

            // Последние задачи
            ws.Cells[row, 1].Value = "Последние задачи";
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
            ws.Cells[row, 1].Value = "Задача";
            ws.Cells[row, 2].Value = "Исполнитель";
            ws.Cells[row, 3].Value = "Дисциплина";
            ws.Cells[row, 4].Value = "Срок";
            ws.Cells[row, 5].Value = "Статус";
            row++;
            foreach (var task in report.RecentTasks)
            {
                ws.Cells[row, 1].Value = task.Title;
                ws.Cells[row, 2].Value = task.ExecutorName;
                ws.Cells[row, 3].Value = task.DisciplineName;
                ws.Cells[row, 4].Value = task.DueDate.ToString("dd.MM.yyyy");
                ws.Cells[row, 5].Value = task.Status;
                row++;
            }
        }

        private void FillActivityExcel(ExcelWorksheet ws, ActivityReportViewModel report)
        {
            int row = 1;
            ws.Cells[row, 1].Value = "ОТЧЕТ О ДЕЯТЕЛЬНОСТИ ПЦК";
            ws.Cells[row, 1, row, 5].Merge = true;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 14;
            row += 2;

            ws.Cells[row, 1].Value = "Период:";
            ws.Cells[row, 2].Value = report.Period;
            row++;
            ws.Cells[row, 1].Value = "Сгенерирован:";
            ws.Cells[row, 2].Value = report.GeneratedAt.ToString("dd.MM.yyyy HH:mm");
            row += 2;

            ws.Cells[row, 1].Value = "Сводная статистика";
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
            ws.Cells[row, 1].Value = "Всего документов";
            ws.Cells[row, 2].Value = report.TotalDocuments;
            row++;
            ws.Cells[row, 1].Value = "Утверждено документов";
            ws.Cells[row, 2].Value = report.ApprovedDocuments;
            row++;
            ws.Cells[row, 1].Value = "Всего часов нагрузки";
            ws.Cells[row, 2].Value = report.TotalWorkloadHours;
            row++;
            ws.Cells[row, 1].Value = "Всего задач";
            ws.Cells[row, 2].Value = report.TotalTasks;
            row++;
            ws.Cells[row, 1].Value = "Выполнено задач";
            ws.Cells[row, 2].Value = report.CompletedTasks;
            row += 2;

            // Активность преподавателей
            ws.Cells[row, 1].Value = "Активность преподавателей";
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
            ws.Cells[row, 1].Value = "Преподаватель";
            ws.Cells[row, 2].Value = "Документов";
            ws.Cells[row, 3].Value = "Утверждено";
            ws.Cells[row, 4].Value = "Часов";
            ws.Cells[row, 5].Value = "Задач (поставлено)";
            ws.Cells[row, 6].Value = "Задач (выполнено)";
            row++;
            foreach (var teacher in report.TeachersActivity.Where(t => t.DocumentsCreated > 0 || t.WorkloadHours > 0 || t.TasksAssigned > 0))
            {
                ws.Cells[row, 1].Value = teacher.TeacherName;
                ws.Cells[row, 2].Value = teacher.DocumentsCreated;
                ws.Cells[row, 3].Value = teacher.DocumentsApproved;
                ws.Cells[row, 4].Value = teacher.WorkloadHours;
                ws.Cells[row, 5].Value = teacher.TasksAssigned;
                ws.Cells[row, 6].Value = teacher.TasksCompleted;
                row++;
            }
        }

        private byte[] GenerateCsvReport(object report, string reportType)
        {
            var sb = new StringBuilder();

            switch (report)
            {
                case DocumentsReportViewModel docReport:
                    sb.AppendLine("Отчет по документам");
                    sb.AppendLine($"Сгенерирован: {docReport.GeneratedAt}");
                    sb.AppendLine();
                    sb.AppendLine("Общая статистика");
                    sb.AppendLine($"Всего документов,{docReport.TotalDocuments}");
                    sb.AppendLine($"Черновики,{docReport.DraftDocuments}");
                    sb.AppendLine($"На рассмотрении,{docReport.ReviewDocuments}");
                    sb.AppendLine($"Утверждено,{docReport.ApprovedDocuments}");
                    sb.AppendLine($"Отклонено,{docReport.RejectedDocuments}");
                    break;

                case WorkloadReportViewModel workloadReport:
                    sb.AppendLine("Отчет по учебной нагрузке");
                    sb.AppendLine($"Сгенерирован: {workloadReport.GeneratedAt}");
                    sb.AppendLine();
                    sb.AppendLine("Общая статистика");
                    sb.AppendLine($"Всего преподавателей,{workloadReport.TotalTeachers}");
                    sb.AppendLine($"Всего дисциплин,{workloadReport.TotalDisciplines}");
                    sb.AppendLine($"Всего часов,{workloadReport.TotalHours}");
                    sb.AppendLine($"Всего групп,{workloadReport.TotalGroups}"); 
                    sb.AppendLine();
                    sb.AppendLine("Детальная нагрузка");
                    sb.AppendLine("Преподаватель,Дисциплина,Уч.год,Семестр,Часы,Тип,Форма контроля,Групп");
                    foreach (var item in workloadReport.WorkloadDetails)
                    {
                        sb.AppendLine($"{item.TeacherName},{item.DisciplineName},{item.AcademicYear},{item.Semester},{item.Hours},{item.LoadType},{item.ControlType},{item.GroupsCount}");
                    }
                    break;

                case TasksReportViewModel tasksReport:
                    sb.AppendLine("Отчет по задачам");
                    sb.AppendLine($"Сгенерирован: {tasksReport.GeneratedAt}");
                    sb.AppendLine();
                    sb.AppendLine("Общая статистика");
                    sb.AppendLine($"Всего задач,{tasksReport.TotalTasks}");
                    sb.AppendLine($"Ожидают,{tasksReport.PendingTasks}");
                    sb.AppendLine($"В работе,{tasksReport.InProgressTasks}");
                    sb.AppendLine($"Выполнено,{tasksReport.CompletedTasks}");
                    sb.AppendLine($"Просрочено,{tasksReport.OverdueTasks}");
                    sb.AppendLine($"Отменено,{tasksReport.CancelledTasks}");
                    break;

                case ActivityReportViewModel activityReport:
                    sb.AppendLine("Отчет о деятельности ПЦК");
                    sb.AppendLine($"Сгенерирован: {activityReport.GeneratedAt}");
                    sb.AppendLine($"Период: {activityReport.Period}");
                    sb.AppendLine();
                    sb.AppendLine("Сводная статистика");
                    sb.AppendLine($"Всего документов,{activityReport.TotalDocuments}");
                    sb.AppendLine($"Утверждено документов,{activityReport.ApprovedDocuments}");
                    sb.AppendLine($"Всего часов нагрузки,{activityReport.TotalWorkloadHours}");
                    sb.AppendLine($"Всего задач,{activityReport.TotalTasks}");
                    sb.AppendLine($"Выполнено задач,{activityReport.CompletedTasks}");
                    break;
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        #endregion

        #region PDF Generation

        private byte[] GeneratePdfReport(object report, string reportType)
        {
            return reportType switch
            {
                "Documents" => GenerateDocumentsPdf((DocumentsReportViewModel)report),
                "Workload" => GenerateWorkloadPdf((WorkloadReportViewModel)report),
                "Tasks" => GenerateTasksPdf((TasksReportViewModel)report),
                "Activity" => GenerateActivityPdf((ActivityReportViewModel)report),
                _ => throw new NotSupportedException($"Report type {reportType} not supported for PDF")
            };
        }

        private byte[] GenerateDocumentsPdf(DocumentsReportViewModel model)
        {
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header()
                        .Text(model.Title)
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .Column(col =>
                        {
                            col.Spacing(10);

                            // Дата генерации
                            col.Item().Text($"Сгенерирован: {model.GeneratedAt:dd.MM.yyyy HH:mm}");

                            // Параметры
                            if (model.Parameters.HasParameters)
                            {
                                col.Item().Text(text =>
                                {
                                    text.Span("Параметры: ").Bold();
                                    if (!string.IsNullOrEmpty(model.Parameters.Period))
                                        text.Span($"Период: {model.Parameters.Period} ");
                                    if (!string.IsNullOrEmpty(model.Parameters.AcademicYear))
                                        text.Span($"Учебный год: {model.Parameters.AcademicYear} ");
                                    if (model.Parameters.Semester.HasValue)
                                        text.Span($"Семестр: {model.Parameters.Semester} ");
                                    if (!string.IsNullOrEmpty(model.Parameters.Teacher))
                                        text.Span($"Преподаватель: {model.Parameters.Teacher} ");
                                    if (!string.IsNullOrEmpty(model.Parameters.Discipline))
                                        text.Span($"Дисциплина: {model.Parameters.Discipline} ");
                                });
                            }

                            // Общая статистика
                            col.Item().Row(row => row.RelativeItem().BorderBottom(1).PaddingBottom(5).Text("Статистика").Bold());
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Text("Показатель");
                                    header.Cell().Text("Значение");
                                });
                                table.Cell().Text("Всего документов");
                                table.Cell().Text(model.TotalDocuments.ToString());
                                table.Cell().Text("Черновики");
                                table.Cell().Text(model.DraftDocuments.ToString());
                                table.Cell().Text("На рассмотрении");
                                table.Cell().Text(model.ReviewDocuments.ToString());
                                table.Cell().Text("Утверждено");
                                table.Cell().Text(model.ApprovedDocuments.ToString());
                                table.Cell().Text("Отклонено");
                                table.Cell().Text(model.RejectedDocuments.ToString());
                            });

                            // Распределение по статусам
                            if (model.DocumentsByStatus.Any())
                            {
                                col.Item().Text("Распределение по статусам").Bold();
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Статус");
                                        header.Cell().Text("Количество");
                                        header.Cell().Text("%");
                                    });
                                    foreach (var item in model.DocumentsByStatus)
                                    {
                                        var percent = model.TotalDocuments > 0
                                            ? Math.Round((double)item.Value / model.TotalDocuments * 100, 1)
                                            : 0;
                                        table.Cell().Text(item.Key);
                                        table.Cell().Text(item.Value.ToString());
                                        table.Cell().Text($"{percent}%");
                                    }
                                });
                            }

                            // Топ дисциплин
                            if (model.DocumentsByDiscipline.Any())
                            {
                                col.Item().Text("Топ дисциплин по количеству документов").Bold();
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Дисциплина");
                                        header.Cell().Text("Всего");
                                        header.Cell().Text("Утверждено");
                                    });
                                    foreach (var item in model.DocumentsByDiscipline.OrderByDescending(d => d.TotalDocuments).Take(10))
                                    {
                                        table.Cell().Text($"{item.DisciplineCode} {item.DisciplineName}");
                                        table.Cell().Text(item.TotalDocuments.ToString());
                                        table.Cell().Text(item.ApprovedDocuments.ToString());
                                    }
                                });
                            }

                            // Последние документы
                            if (model.RecentDocuments.Any())
                            {
                                col.Item().Text("Последние документы").Bold();
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Название");
                                        header.Cell().Text("Тип");
                                        header.Cell().Text("Автор");
                                        header.Cell().Text("Дисциплина");
                                        header.Cell().Text("Статус");
                                    });
                                    foreach (var doc in model.RecentDocuments.Take(10))
                                    {
                                        table.Cell().Text(doc.Name);
                                        table.Cell().Text(doc.DocumentType);
                                        table.Cell().Text(doc.AuthorName);
                                        table.Cell().Text(doc.DisciplineName);
                                        table.Cell().Text(doc.Status);
                                    }
                                });
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Страница ");
                            x.CurrentPageNumber();
                            x.Span(" из ");
                            x.TotalPages();
                        });
                });
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return stream.ToArray();
        }

        private byte[] GenerateWorkloadPdf(WorkloadReportViewModel model)
        {
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header()
                        .Text(model.Title)
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .Column(col =>
                        {
                            col.Spacing(10);
                            col.Item().Text($"Сгенерирован: {model.GeneratedAt:dd.MM.yyyy HH:mm}");

                            if (model.Parameters.HasParameters)
                            {
                                col.Item().Text(text =>
                                {
                                    text.Span("Параметры: ").Bold();
                                    if (!string.IsNullOrEmpty(model.Parameters.AcademicYear))
                                        text.Span($"Учебный год: {model.Parameters.AcademicYear} ");
                                    if (model.Parameters.Semester.HasValue)
                                        text.Span($"Семестр: {model.Parameters.Semester} ");
                                });
                            }

                            // Общая статистика
                            col.Item().Row(row => row.RelativeItem().BorderBottom(1).PaddingBottom(5).Text("Статистика").Bold());
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Text("Показатель");
                                    header.Cell().Text("Значение");
                                });
                                table.Cell().Text("Всего преподавателей");
                                table.Cell().Text(model.TotalTeachers.ToString());
                                table.Cell().Text("Всего дисциплин");
                                table.Cell().Text(model.TotalDisciplines.ToString());
                                table.Cell().Text("Всего часов");
                                table.Cell().Text(model.TotalHours.ToString());
                                table.Cell().Text("Всего групп");
                                table.Cell().Text(model.TotalGroups.ToString());
                            });

                            // Нагрузка по преподавателям
                            if (model.WorkloadByTeacher.Any())
                            {
                                col.Item().Text("Нагрузка по преподавателям").Bold();
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Преподаватель");
                                        header.Cell().Text("Часов");
                                        header.Cell().Text("Дисциплин");
                                    });
                                    foreach (var item in model.WorkloadByTeacher.OrderByDescending(w => w.TotalHours).Take(15))
                                    {
                                        table.Cell().Text(item.TeacherName);
                                        table.Cell().Text(item.TotalHours.ToString());
                                        table.Cell().Text(item.DisciplinesCount.ToString());
                                    }
                                });
                            }

                            // Детальная нагрузка
                            if (model.WorkloadDetails.Any())
                            {
                                col.Item().Text("Детальная нагрузка").Bold();
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Преподаватель");
                                        header.Cell().Text("Дисциплина");
                                        header.Cell().Text("Год");
                                        header.Cell().Text("Сем.");
                                        header.Cell().Text("Часы");
                                        header.Cell().Text("Тип");
                                        header.Cell().Text("Форма контроля");
                                    });
                                    foreach (var item in model.WorkloadDetails.Take(20))
                                    {
                                        table.Cell().Text(item.TeacherName);
                                        table.Cell().Text($"{item.DisciplineCode} {item.DisciplineName}");
                                        table.Cell().Text(item.AcademicYear);
                                        table.Cell().Text(item.Semester.ToString());
                                        table.Cell().Text(item.Hours.ToString());
                                        table.Cell().Text(item.LoadType);
                                        table.Cell().Text(item.ControlType ?? "");
                                    }
                                });
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Страница ");
                            x.CurrentPageNumber();
                            x.Span(" из ");
                            x.TotalPages();
                        });
                });
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return stream.ToArray();
        }

        private byte[] GenerateTasksPdf(TasksReportViewModel model)
        {
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header()
                        .Text(model.Title)
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .Column(col =>
                        {
                            col.Spacing(10);
                            col.Item().Text($"Сгенерирован: {model.GeneratedAt:dd.MM.yyyy HH:mm}");

                            // Общая статистика
                            col.Item().Row(row => row.RelativeItem().BorderBottom(1).PaddingBottom(5).Text("Статистика").Bold());
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Text("Показатель");
                                    header.Cell().Text("Значение");
                                });
                                table.Cell().Text("Всего задач");
                                table.Cell().Text(model.TotalTasks.ToString());
                                table.Cell().Text("Ожидают");
                                table.Cell().Text(model.PendingTasks.ToString());
                                table.Cell().Text("В работе");
                                table.Cell().Text(model.InProgressTasks.ToString());
                                table.Cell().Text("Выполнено");
                                table.Cell().Text(model.CompletedTasks.ToString());
                                table.Cell().Text("Просрочено");
                                table.Cell().Text(model.OverdueTasks.ToString());
                                table.Cell().Text("Отменено");
                                table.Cell().Text(model.CancelledTasks.ToString());
                            });

                            // Просроченные задачи
                            if (model.OverdueTasksList.Any())
                            {
                                col.Item().Text("Просроченные задачи").Bold().FontColor(Colors.Red.Medium);
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Задача");
                                        header.Cell().Text("Исполнитель");
                                        header.Cell().Text("Дисциплина");
                                        header.Cell().Text("Срок");
                                    });
                                    foreach (var task in model.OverdueTasksList)
                                    {
                                        table.Cell().Text(task.Title);
                                        table.Cell().Text(task.ExecutorName);
                                        table.Cell().Text(task.DisciplineName);
                                        table.Cell().Text(task.DueDate.ToString("dd.MM.yyyy"));
                                    }
                                });
                            }

                            // Эффективность исполнителей
                            if (model.TasksByExecutor.Any())
                            {
                                col.Item().Text("Эффективность преподавателей").Bold();
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Преподаватель");
                                        header.Cell().Text("Всего задач");
                                        header.Cell().Text("Выполнено");
                                        header.Cell().Text("Просрочено");
                                    });
                                    foreach (var item in model.TasksByExecutor.OrderByDescending(t => t.TotalTasks).Take(15))
                                    {
                                        table.Cell().Text(item.ExecutorName);
                                        table.Cell().Text(item.TotalTasks.ToString());
                                        table.Cell().Text(item.CompletedTasks.ToString());
                                        table.Cell().Text(item.OverdueTasks.ToString());
                                    }
                                });
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Страница ");
                            x.CurrentPageNumber();
                            x.Span(" из ");
                            x.TotalPages();
                        });
                });
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return stream.ToArray();
        }

        private byte[] GenerateActivityPdf(ActivityReportViewModel model)
        {
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header()
                        .Text(model.Title)
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .Column(col =>
                        {
                            col.Spacing(10);
                            col.Item().Text($"Период: {model.Period}");
                            col.Item().Text($"Сгенерирован: {model.GeneratedAt:dd.MM.yyyy HH:mm}");

                            // Сводная статистика
                            col.Item().Row(row => row.RelativeItem().BorderBottom(1).PaddingBottom(5).Text("Сводная статистика").Bold());
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Text("Показатель");
                                    header.Cell().Text("Значение");
                                });
                                table.Cell().Text("Всего документов");
                                table.Cell().Text(model.TotalDocuments.ToString());
                                table.Cell().Text("Утверждено документов");
                                table.Cell().Text(model.ApprovedDocuments.ToString());
                                table.Cell().Text("Всего часов нагрузки");
                                table.Cell().Text(model.TotalWorkloadHours.ToString());
                                table.Cell().Text("Всего задач");
                                table.Cell().Text(model.TotalTasks.ToString());
                                table.Cell().Text("Выполнено задач");
                                table.Cell().Text(model.CompletedTasks.ToString());
                            });

                            // Активность преподавателей
                            if (model.TeachersActivity.Any())
                            {
                                col.Item().Text("Активность преподавателей").Bold();
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Преподаватель");
                                        header.Cell().Text("Документов");
                                        header.Cell().Text("Утверждено");
                                        header.Cell().Text("Часов");
                                        header.Cell().Text("Задач (выполнено)");
                                    });
                                    foreach (var item in model.TeachersActivity.OrderByDescending(t => t.DocumentsCreated + t.TasksCompleted).Take(15))
                                    {
                                        table.Cell().Text(item.TeacherName);
                                        table.Cell().Text(item.DocumentsCreated.ToString());
                                        table.Cell().Text(item.DocumentsApproved.ToString());
                                        table.Cell().Text(item.WorkloadHours.ToString());
                                        table.Cell().Text(item.TasksCompleted.ToString());
                                    }
                                });
                            }

                            // Динамика по месяцам
                            if (model.MonthlyActivity.Any())
                            {
                                col.Item().Text("Динамика по месяцам").Bold();
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(1);
                                    });
                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Месяц");
                                        header.Cell().Text("Документов");
                                        header.Cell().Text("Задач создано");
                                        header.Cell().Text("Задач выполнено");
                                    });
                                    foreach (var item in model.MonthlyActivity)
                                    {
                                        table.Cell().Text($"{item.Month} {item.Year}");
                                        table.Cell().Text(item.DocumentsCreated.ToString());
                                        table.Cell().Text(item.TasksCreated.ToString());
                                        table.Cell().Text(item.TasksCompleted.ToString());
                                    }
                                });
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Страница ");
                            x.CurrentPageNumber();
                            x.Span(" из ");
                            x.TotalPages();
                        });
                });
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return stream.ToArray();
        }

        #endregion

        #region Word Generation

        private byte[] GenerateWordReport(object report, string reportType)
        {
            using var stream = new MemoryStream();
            using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = mainPart.Document.AppendChild(new Body());

                // Добавляем заголовок
                var titlePara = new Paragraph();
                var titleRun = new Run();
                titleRun.AppendChild(new Text($"Отчет по {reportType}"));
                titleRun.RunProperties = new RunProperties(new Bold());
                titlePara.AppendChild(titleRun);
                titlePara.ParagraphProperties = new ParagraphProperties(new Justification() { Val = JustificationValues.Center });
                body.AppendChild(titlePara);

                // Дата
                var datePara = new Paragraph();
                var dateRun = new Run();
                dateRun.AppendChild(new Text($"Сгенерирован: {DateTime.UtcNow:dd.MM.yyyy HH:mm}"));
                datePara.AppendChild(dateRun);
                body.AppendChild(datePara);

                body.AppendChild(new Paragraph()); // пустая строка

                // В зависимости от типа отчёта вызываем соответствующий метод
                switch (reportType)
                {
                    case "Documents":
                        BuildDocumentsWord(body, (DocumentsReportViewModel)report);
                        break;
                    case "Workload":
                        BuildWorkloadWord(body, (WorkloadReportViewModel)report);
                        break;
                    case "Tasks":
                        BuildTasksWord(body, (TasksReportViewModel)report);
                        break;
                    case "Activity":
                        BuildActivityWord(body, (ActivityReportViewModel)report);
                        break;
                }
            }
            return stream.ToArray();
        }

        private void BuildDocumentsWord(Body body, DocumentsReportViewModel model)
        {
            // Заголовок "Статистика"
            AddHeading(body, "Статистика");
            var statsTable = CreateTable(2);
            AddTableRow(statsTable, new[] { "Показатель", "Значение" }, true);
            AddTableRow(statsTable, new[] { "Всего документов", model.TotalDocuments.ToString() });
            AddTableRow(statsTable, new[] { "Черновики", model.DraftDocuments.ToString() });
            AddTableRow(statsTable, new[] { "На рассмотрении", model.ReviewDocuments.ToString() });
            AddTableRow(statsTable, new[] { "Утверждено", model.ApprovedDocuments.ToString() });
            AddTableRow(statsTable, new[] { "Отклонено", model.RejectedDocuments.ToString() });
            body.AppendChild(statsTable);
            body.AppendChild(new Paragraph());

            // Топ дисциплин
            if (model.DocumentsByDiscipline.Any())
            {
                AddHeading(body, "Топ дисциплин по количеству документов");
                var disciplineTable = CreateTable(3);
                AddTableRow(disciplineTable, new[] { "Дисциплина", "Всего", "Утверждено" }, true);
                foreach (var item in model.DocumentsByDiscipline.OrderByDescending(d => d.TotalDocuments).Take(10))
                {
                    AddTableRow(disciplineTable, new[] { $"{item.DisciplineCode} {item.DisciplineName}", item.TotalDocuments.ToString(), item.ApprovedDocuments.ToString() });
                }
                body.AppendChild(disciplineTable);
                body.AppendChild(new Paragraph());
            }

            // Последние документы
            if (model.RecentDocuments.Any())
            {
                AddHeading(body, "Последние документы");
                var recentTable = CreateTable(5);
                AddTableRow(recentTable, new[] { "Название", "Тип", "Автор", "Дисциплина", "Статус" }, true);
                foreach (var doc in model.RecentDocuments.Take(10))
                {
                    AddTableRow(recentTable, new[] { doc.Name, doc.DocumentType, doc.AuthorName, doc.DisciplineName, doc.Status });
                }
                body.AppendChild(recentTable);
            }
        }

        private void BuildWorkloadWord(Body body, WorkloadReportViewModel model)
        {
            AddHeading(body, "Статистика");
            var statsTable = CreateTable(2);
            AddTableRow(statsTable, new[] { "Показатель", "Значение" }, true);
            AddTableRow(statsTable, new[] { "Всего преподавателей", model.TotalTeachers.ToString() });
            AddTableRow(statsTable, new[] { "Всего дисциплин", model.TotalDisciplines.ToString() });
            AddTableRow(statsTable, new[] { "Всего часов", model.TotalHours.ToString() });
            AddTableRow(statsTable, new[] { "Всего групп", model.TotalGroups.ToString() });
            body.AppendChild(statsTable);
            body.AppendChild(new Paragraph());

            if (model.WorkloadByTeacher.Any())
            {
                AddHeading(body, "Нагрузка по преподавателям");
                var teacherTable = CreateTable(3);
                AddTableRow(teacherTable, new[] { "Преподаватель", "Часов", "Дисциплин" }, true);
                foreach (var item in model.WorkloadByTeacher.OrderByDescending(w => w.TotalHours).Take(15))
                {
                    AddTableRow(teacherTable, new[] { item.TeacherName, item.TotalHours.ToString(), item.DisciplinesCount.ToString() });
                }
                body.AppendChild(teacherTable);
                body.AppendChild(new Paragraph());
            }

            if (model.WorkloadDetails.Any())
            {
                AddHeading(body, "Детальная нагрузка");
                var detailTable = CreateTable(7); // было 6, стало 7
                AddTableRow(detailTable, new[] { "Преподаватель", "Дисциплина", "Год", "Сем.", "Часы", "Тип", "Форма контроля" }, true);
                foreach (var item in model.WorkloadDetails.Take(20))
                {
                    AddTableRow(detailTable, new[] { item.TeacherName, $"{item.DisciplineCode} {item.DisciplineName}", item.AcademicYear, item.Semester.ToString(), item.Hours.ToString(), item.LoadType, item.ControlType ?? "" });
                }
                body.AppendChild(detailTable);
            }
        }

        private void BuildTasksWord(Body body, TasksReportViewModel model)
        {
            AddHeading(body, "Статистика");
            var statsTable = CreateTable(2);
            AddTableRow(statsTable, new[] { "Показатель", "Значение" }, true);
            AddTableRow(statsTable, new[] { "Всего задач", model.TotalTasks.ToString() });
            AddTableRow(statsTable, new[] { "Ожидают", model.PendingTasks.ToString() });
            AddTableRow(statsTable, new[] { "В работе", model.InProgressTasks.ToString() });
            AddTableRow(statsTable, new[] { "Выполнено", model.CompletedTasks.ToString() });
            AddTableRow(statsTable, new[] { "Просрочено", model.OverdueTasks.ToString() });
            AddTableRow(statsTable, new[] { "Отменено", model.CancelledTasks.ToString() });
            body.AppendChild(statsTable);
            body.AppendChild(new Paragraph());

            if (model.OverdueTasksList.Any())
            {
                AddHeading(body, "Просроченные задачи");
                var overdueTable = CreateTable(4);
                AddTableRow(overdueTable, new[] { "Задача", "Исполнитель", "Дисциплина", "Срок" }, true);
                foreach (var task in model.OverdueTasksList)
                {
                    AddTableRow(overdueTable, new[] { task.Title, task.ExecutorName, task.DisciplineName, task.DueDate.ToString("dd.MM.yyyy") });
                }
                body.AppendChild(overdueTable);
                body.AppendChild(new Paragraph());
            }

            if (model.TasksByExecutor.Any())
            {
                AddHeading(body, "Эффективность преподавателей");
                var execTable = CreateTable(4);
                AddTableRow(execTable, new[] { "Преподаватель", "Всего задач", "Выполнено", "Просрочено" }, true);
                foreach (var item in model.TasksByExecutor.OrderByDescending(t => t.TotalTasks).Take(15))
                {
                    AddTableRow(execTable, new[] { item.ExecutorName, item.TotalTasks.ToString(), item.CompletedTasks.ToString(), item.OverdueTasks.ToString() });
                }
                body.AppendChild(execTable);
            }
        }

        private void BuildActivityWord(Body body, ActivityReportViewModel model)
        {
            // Период
            var periodPara = new Paragraph();
            var periodRun = new Run();
            periodRun.AppendChild(new Text($"Период: {model.Period}"));
            periodPara.AppendChild(periodRun);
            body.AppendChild(periodPara);
            body.AppendChild(new Paragraph());

            AddHeading(body, "Сводная статистика");
            var statsTable = CreateTable(2);
            AddTableRow(statsTable, new[] { "Показатель", "Значение" }, true);
            AddTableRow(statsTable, new[] { "Всего документов", model.TotalDocuments.ToString() });
            AddTableRow(statsTable, new[] { "Утверждено документов", model.ApprovedDocuments.ToString() });
            AddTableRow(statsTable, new[] { "Всего часов нагрузки", model.TotalWorkloadHours.ToString() });
            AddTableRow(statsTable, new[] { "Всего задач", model.TotalTasks.ToString() });
            AddTableRow(statsTable, new[] { "Выполнено задач", model.CompletedTasks.ToString() });
            body.AppendChild(statsTable);
            body.AppendChild(new Paragraph());

            if (model.TeachersActivity.Any())
            {
                AddHeading(body, "Активность преподавателей");
                var teacherTable = CreateTable(5);
                AddTableRow(teacherTable, new[] { "Преподаватель", "Документов", "Утверждено", "Часов", "Задач (выполнено)" }, true);
                foreach (var item in model.TeachersActivity.OrderByDescending(t => t.DocumentsCreated + t.TasksCompleted).Take(15))
                {
                    AddTableRow(teacherTable, new[] { item.TeacherName, item.DocumentsCreated.ToString(), item.DocumentsApproved.ToString(), item.WorkloadHours.ToString(), item.TasksCompleted.ToString() });
                }
                body.AppendChild(teacherTable);
                body.AppendChild(new Paragraph());
            }

            if (model.MonthlyActivity.Any())
            {
                AddHeading(body, "Динамика по месяцам");
                var monthlyTable = CreateTable(4);
                AddTableRow(monthlyTable, new[] { "Месяц", "Документов", "Задач создано", "Задач выполнено" }, true);
                foreach (var item in model.MonthlyActivity)
                {
                    AddTableRow(monthlyTable, new[] { $"{item.Month} {item.Year}", item.DocumentsCreated.ToString(), item.TasksCreated.ToString(), item.TasksCompleted.ToString() });
                }
                body.AppendChild(monthlyTable);
            }
        }

        // Вспомогательные методы для Word
        private Table CreateTable(int columns)
        {
            var table = new Table();
            var tableProperties = new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 1 },
                    new BottomBorder { Val = BorderValues.Single, Size = 1 },
                    new LeftBorder { Val = BorderValues.Single, Size = 1 },
                    new RightBorder { Val = BorderValues.Single, Size = 1 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 1 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 1 }
                ),
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }
            );
            table.AppendChild(tableProperties);

            // Создаем TableGrid (аналог разметки колонок)
            var tableGrid = new TableGrid();
            for (int i = 0; i < columns; i++)
            {
                tableGrid.AppendChild(new GridColumn { Width = (5000 / columns).ToString() });
            }
            table.AppendChild(tableGrid);

            return table;
        }

        private void AddTableRow(Table table, string[] cells, bool isHeader = false)
        {
            var tr = new TableRow();
            foreach (var text in cells)
            {
                var tc = new TableCell();
                var p = new Paragraph();
                var run = new Run();
                run.AppendChild(new Text(text));
                if (isHeader)
                {
                    run.RunProperties = new RunProperties(new Bold());
                }
                p.AppendChild(run);
                tc.Append(p);
                tr.Append(tc);
            }
            table.AppendChild(tr);
        }

        private void AddHeading(Body body, string text)
        {
            var heading = new Paragraph();
            var run = new Run();
            run.AppendChild(new Text(text));
            run.RunProperties = new RunProperties(new Bold());
            heading.AppendChild(run);
            heading.ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines() { After = "200" });
            body.AppendChild(heading);
        }

        #endregion

        #region Вспомогательные методы

        private async Task PopulateDropdowns(ReportParameterViewModel model)
        {
            // Учебные годы
            model.AcademicYears = new List<SelectListItem>
            {
                new SelectListItem { Value = "2025-2026", Text = "2025-2026" },
                new SelectListItem { Value = "2026-2027", Text = "2026-2027" },
                new SelectListItem { Value = "2027-2028", Text = "2027-2028" }
            };

            // Преподаватели
            model.Teachers = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = u.FullName ?? u.Email
                })
                .ToListAsync();
            model.Teachers.Insert(0, new SelectListItem { Value = "", Text = "-- Все преподаватели --" });

            // Дисциплины
            model.Disciplines = await _context.Disciplines
                .OrderBy(d => d.Code)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = $"{d.Code} - {d.Name}"
                })
                .ToListAsync();
            model.Disciplines.Insert(0, new SelectListItem { Value = "", Text = "-- Все дисциплины --" });

            // Статусы документов
            model.DocumentStatuses = Enum.GetValues(typeof(DocumentStatus))
                .Cast<DocumentStatus>()
                .Select(s => new SelectListItem
                {
                    Value = ((int)s).ToString(),
                    Text = GetDocumentStatusDisplay(s)
                })
                .ToList();
            model.DocumentStatuses.Insert(0, new SelectListItem { Value = "", Text = "-- Все статусы --" });
        }

        private ReportParametersDisplay GetParametersDisplay(ReportParameterViewModel model)
        {
            var display = new ReportParametersDisplay();

            if (model.StartDate.HasValue || model.EndDate.HasValue)
            {
                display.Period = $"с {model.StartDate:dd.MM.yyyy} по {model.EndDate:dd.MM.yyyy}";
            }

            if (!string.IsNullOrEmpty(model.AcademicYear))
            {
                display.AcademicYear = model.AcademicYear;
            }

            if (model.Semester.HasValue)
            {
                display.Semester = model.Semester.Value;
            }

            if (model.TeacherId.HasValue)
            {
                var teacher = _context.Users.Find(model.TeacherId.Value);
                display.Teacher = teacher?.FullName ?? teacher?.Email;
            }

            if (model.DisciplineId.HasValue)
            {
                var discipline = _context.Disciplines.Find(model.DisciplineId.Value);
                display.Discipline = discipline?.Name;
            }

            if (model.DocumentStatus.HasValue)
            {
                display.DocumentStatus = GetDocumentStatusDisplay(model.DocumentStatus.Value);
            }

            return display;
        }

        private string GetDocumentStatusDisplay(DocumentStatus status)
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

        private string GetTaskStatusDisplay(Models.TaskStatus status)
        {
            return status switch
            {
                Models.TaskStatus.Pending => "Ожидает",
                Models.TaskStatus.InProgress => "В работе",
                Models.TaskStatus.Completed => "Выполнена",
                Models.TaskStatus.Overdue => "Просрочена",
                Models.TaskStatus.Cancelled => "Отменена",
                _ => status.ToString()
            };
        }

        private string GetReportTypeName(string reportType)
        {
            return reportType switch
            {
                "Documents" => "документам",
                "Workload" => "нагрузке",
                "Tasks" => "задачам",
                "Activity" => "деятельности",
                _ => reportType
            };
        }

        private string GetMonthName(int month)
        {
            return month switch
            {
                1 => "Январь",
                2 => "Февраль",
                3 => "Март",
                4 => "Апрель",
                5 => "Май",
                6 => "Июнь",
                7 => "Июль",
                8 => "Август",
                9 => "Сентябрь",
                10 => "Октябрь",
                11 => "Ноябрь",
                12 => "Декабрь",
                _ => month.ToString()
            };
        }

        #endregion

        // GET: Reports/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var report = await _context.Reports
                .Include(r => r.CreatedBy)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound();
            }

            return View(report);
        }

        // POST: Reports/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report != null)
            {
                // Удаляем файл, если он есть
                if (!string.IsNullOrEmpty(report.FilePath))
                {
                    var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, report.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }

                _context.Reports.Remove(report);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Отчет успешно удален";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}