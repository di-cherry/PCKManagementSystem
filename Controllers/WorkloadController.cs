using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PCKManagementSystem.Data;
using PCKManagementSystem.Models;
using PCKManagementSystem.Models.ViewModels;
using System.Security.Claims;
using PCKManagementSystem.Hubs;

namespace PCKManagementSystem.Controllers
{
    [Authorize]
    public class WorkloadController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public WorkloadController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // Вспомогательный метод для безопасного получения ID текущего пользователя
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim) : 0;
        }

        // GET: Workload
        public async Task<IActionResult> Index(string sortOrder, string searchString, string academicYear, int? semester, int? teacherId, int? disciplineId)
        {
            var workloads = _context.Workloads
                .Include(w => w.Teacher)
                .Include(w => w.Discipline)
                .ThenInclude(d => d.Specialty)
                .AsQueryable();

            // Фильтры
            if (!string.IsNullOrEmpty(academicYear))
                workloads = workloads.Where(w => w.AcademicYear == academicYear);
            if (semester.HasValue)
                workloads = workloads.Where(w => w.Semester == semester.Value);
            if (teacherId.HasValue)
                workloads = workloads.Where(w => w.TeacherId == teacherId.Value);
            if (disciplineId.HasValue)
                workloads = workloads.Where(w => w.DisciplineId == disciplineId.Value);

            // Поиск
            if (!string.IsNullOrEmpty(searchString))
            {
                workloads = workloads.Where(w =>
                    w.Teacher.FullName.Contains(searchString) ||
                    w.Discipline.Name.Contains(searchString) ||
                    w.Discipline.Code.Contains(searchString));
            }

            // Сортировка
            ViewData["CurrentSort"] = sortOrder;
            ViewData["TeacherSortParm"] = sortOrder == "teacher" ? "teacher_desc" : "teacher";
            ViewData["DisciplineSortParm"] = sortOrder == "discipline" ? "discipline_desc" : "discipline";
            ViewData["YearSortParm"] = sortOrder == "year" ? "year_desc" : "year";
            ViewData["SemesterSortParm"] = sortOrder == "semester" ? "semester_desc" : "semester";
            ViewData["HoursSortParm"] = sortOrder == "hours" ? "hours_desc" : "hours";
            ViewData["TypeSortParm"] = sortOrder == "type" ? "type_desc" : "type";
            ViewData["CurrentSearch"] = searchString;

            switch (sortOrder)
            {
                case "teacher":
                    workloads = workloads.OrderBy(w => w.Teacher.FullName);
                    break;
                case "teacher_desc":
                    workloads = workloads.OrderByDescending(w => w.Teacher.FullName);
                    break;
                case "discipline":
                    workloads = workloads.OrderBy(w => w.Discipline.Name);
                    break;
                case "discipline_desc":
                    workloads = workloads.OrderByDescending(w => w.Discipline.Name);
                    break;
                case "year":
                    workloads = workloads.OrderBy(w => w.AcademicYear);
                    break;
                case "year_desc":
                    workloads = workloads.OrderByDescending(w => w.AcademicYear);
                    break;
                case "semester":
                    workloads = workloads.OrderBy(w => w.Semester);
                    break;
                case "semester_desc":
                    workloads = workloads.OrderByDescending(w => w.Semester);
                    break;
                case "hours":
                    workloads = workloads.OrderBy(w => w.Hours);
                    break;
                case "hours_desc":
                    workloads = workloads.OrderByDescending(w => w.Hours);
                    break;
                case "type":
                    workloads = workloads.OrderBy(w => w.LoadType);
                    break;
                case "type_desc":
                    workloads = workloads.OrderByDescending(w => w.LoadType);
                    break;
                default:
                    workloads = workloads.OrderBy(w => w.AcademicYear).ThenBy(w => w.Semester);
                    break;
            }

            // Преобразование в ViewModel (как было)
            var workloadList = await workloads
                .Select(w => new WorkloadViewModel
                {
                    Id = w.Id,
                    TeacherName = w.Teacher.FullName,
                    DisciplineName = w.Discipline.Name,
                    DisciplineCode = w.Discipline.Code,
                    AcademicYear = w.AcademicYear,
                    Semester = w.Semester,
                    Hours = w.Hours,
                    LoadType = w.LoadType,
                    GroupsCount = w.GroupsCount,
                    Comments = w.Comments ?? "",
                    CreatedAt = w.CreatedAt,
                    ControlType = w.ControlType,
                    TotalHours = w.TotalHours,
                    Course = w.Course,
                    StudyForm = w.StudyForm
                })
                .ToListAsync();

            // Подготовка данных для фильтров
            var filterModel = new WorkloadFilterViewModel
            {
                AcademicYear = academicYear,
                Semester = semester,
                TeacherId = teacherId,
                DisciplineId = disciplineId,
                AcademicYears = GetAcademicYears(),
                Teachers = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.FullName }).ToListAsync(),
                Disciplines = await _context.Disciplines.OrderBy(d => d.Code)
                    .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = $"{d.Code} - {d.Name}" }).ToListAsync()
            };

            ViewBag.FilterModel = filterModel;
            ViewBag.IsChairmanOrAdmin = User.IsInRole("Администратор") || User.IsInRole("Председатель ПЦК");
            ViewBag.CurrentSearch = searchString;

            return View(workloadList);
        }

        private List<SelectListItem> GetLoadTypeOptions()
        {
            var types = new List<string>
            {
                "Лекции", "Практические", "Лабораторные", "Консультации",
                "Экзамен", "Зачет", "Курсовая работа", "Практика"
            };
            return types.Select(t => new SelectListItem { Value = t, Text = t }).ToList();
        }


        // GET: Workload/Create
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Create()
        {
            var model = new WorkloadCreateViewModel
            {
                Teachers = await GetTeachersList(),
                Disciplines = await GetDisciplinesList(),
                LoadTypeOptions = GetLoadTypeOptions(),
                AcademicYears = GetAcademicYears()
            };

            return View(model);
        }

        // POST: Workload/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Create(WorkloadCreateViewModel model)
        {
            // Проверка на дублирование
            var exists = await _context.Workloads.AnyAsync(w =>
                w.TeacherId == model.TeacherId &&
                w.DisciplineId == model.DisciplineId &&
                w.AcademicYear == model.AcademicYear &&
                w.Semester == model.Semester);


            if (ModelState.IsValid)
            {
                string loadTypesString = model.SelectedLoadTypes != null && model.SelectedLoadTypes.Any()
                ? string.Join(", ", model.SelectedLoadTypes)
                : string.Empty;
                var workload = new Workload
                {
                    TeacherId = model.TeacherId,
                    DisciplineId = model.DisciplineId,
                    AcademicYear = model.AcademicYear,
                    Semester = model.Semester,
                    Hours = model.Hours,
                    LoadType = loadTypesString,
                    GroupsCount = model.GroupsCount,
                    Comments = model.Comments,
                    CreatedAt = DateTime.UtcNow,
                    ControlType = model.ControlType,
                    AdditionalHours = model.AdditionalHours,
                    TotalHours = model.Hours * model.GroupsCount + model.AdditionalHours,
                    Course = model.Course,
                    StudyForm = model.StudyForm
                };

                _context.Add(workload);
                await _context.SaveChangesAsync();

                // Получаем название дисциплины
                var discipline = await _context.Disciplines.FindAsync(model.DisciplineId);
                var message = $"Вам назначена нагрузка: {discipline?.Name} ({model.Hours} ч.) на {model.AcademicYear} уч.год, {model.Semester} семестр";
                var url = Url.Action("MyLoad", "Workload"); // можно сразу вести на страницу нагрузки преподавателя

                await _hubContext.Clients.User(model.TeacherId.ToString())
                    .SendAsync("ReceiveNotification", message, url);

                // TODO: Добавить запись в AuditLog
                TempData["Success"] = "Учебная нагрузка успешно добавлена";
                return RedirectToAction(nameof(Index));
            }

            // Если дошли до сюда - что-то пошло не так, перезагружаем списки
            model.Teachers = await GetTeachersList();
            model.Disciplines = await GetDisciplinesList();
            model.LoadTypeOptions = GetLoadTypeOptions();
            model.AcademicYears = GetAcademicYears();

            return View(model);
        }

        // GET: Workload/Edit/5
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var workload = await _context.Workloads.FindAsync(id);
            if (workload == null)
            {
                return NotFound();
            }

            var model = new WorkloadCreateViewModel
            {
                TeacherId = workload.TeacherId,
                DisciplineId = workload.DisciplineId,
                AcademicYear = workload.AcademicYear, 
                AcademicYears = GetAcademicYears(),
                Semester = workload.Semester,
                Hours = workload.Hours,
                //LoadType = workload.LoadType,
                GroupsCount = workload.GroupsCount,
                Comments = workload.Comments,
                Teachers = await GetTeachersList(),
                Disciplines = await GetDisciplinesList(),
                LoadTypeOptions = GetLoadTypeOptions(),
                SelectedLoadTypes = string.IsNullOrEmpty(workload.LoadType) ? new List<string>() : workload.LoadType.Split(", ").ToList(),
                Course = workload.Course,
                StudyForm = workload.StudyForm
            };

            return View(model);
        }

        // POST: Workload/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Edit(int id, WorkloadCreateViewModel model)
        {
            var workload = await _context.Workloads.FindAsync(id);
            if (workload == null)
            {
                return NotFound();
            }

            // Проверка на дублирование (исключая текущую запись)
            var exists = await _context.Workloads.AnyAsync(w =>
                w.Id != id &&
                w.TeacherId == model.TeacherId &&
                w.DisciplineId == model.DisciplineId &&
                w.AcademicYear == model.AcademicYear &&
                w.Semester == model.Semester);

            string loadTypesString = model.SelectedLoadTypes != null && model.SelectedLoadTypes.Any() ? string.Join(", ", model.SelectedLoadTypes) : string.Empty;

            if (ModelState.IsValid)
            {

                workload.TeacherId = model.TeacherId;
                workload.DisciplineId = model.DisciplineId;
                workload.AcademicYear = model.AcademicYear;
                workload.Semester = model.Semester;
                workload.Hours = model.Hours;
                workload.LoadType = loadTypesString;
                workload.GroupsCount = model.GroupsCount;
                workload.Comments = model.Comments;
                workload.UpdatedAt = DateTime.UtcNow;
                workload.ControlType = model.ControlType;
                workload.AdditionalHours = model.AdditionalHours;
                workload.TotalHours = model.Hours * model.GroupsCount + model.AdditionalHours;
                workload.Course = model.Course;
                workload.StudyForm = model.StudyForm;

                _context.Update(workload);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Учебная нагрузка успешно обновлена";
                return RedirectToAction(nameof(Index));
            }

            model.Teachers = await GetTeachersList();
            model.Disciplines = await GetDisciplinesList();
            model.LoadTypeOptions = GetLoadTypeOptions();
            model.AcademicYears = GetAcademicYears();

            return View(model);
        }

        // GET: Workload/Delete/5
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var workload = await _context.Workloads
                .Include(w => w.Teacher)
                .Include(w => w.Discipline)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (workload == null)
            {
                return NotFound();
            }

            return View(workload);
        }

        // POST: Workload/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var workload = await _context.Workloads.FindAsync(id);
            if (workload != null)
            {
                _context.Workloads.Remove(workload);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Нагрузка успешно удалена";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Workload/MyLoad (для преподавателей)
        [Authorize(Roles = "Преподаватель")]
        public async Task<IActionResult> MyLoad()
        {
            var userId = GetCurrentUserId();

            var workloads = await _context.Workloads
                .Include(w => w.Discipline)
                .ThenInclude(d => d.Specialty)
                .Where(w => w.TeacherId == userId)
                .OrderBy(w => w.AcademicYear)
                .ThenBy(w => w.Semester)
                .Select(w => new WorkloadViewModel
                {
                    Id = w.Id,
                    TeacherName = w.Teacher.FullName,
                    DisciplineName = w.Discipline.Name,
                    DisciplineCode = w.Discipline.Code,
                    AcademicYear = w.AcademicYear,
                    Semester = w.Semester,
                    Hours = w.Hours,
                    LoadType = w.LoadType,
                    GroupsCount = w.GroupsCount,
                    Comments = w.Comments ?? "",
                    CreatedAt = w.CreatedAt
                })
                .ToListAsync();

            ViewBag.IsMyLoad = true;
            return View("Index", workloads);
        }

        // Вспомогательные методы для получения списков
        private async Task<List<SelectListItem>> GetTeachersList()
        {
            var teachers = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = u.FullName
                })
                .ToListAsync();

            return teachers;
        }

        private async Task<List<SelectListItem>> GetDisciplinesList()
        {
            var disciplines = await _context.Disciplines
                .OrderBy(d => d.Code)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = $"{d.Code} - {d.Name}"
                })
                .ToListAsync();

            return disciplines;
        }

        private bool WorkloadExists(int id)
        {
            return _context.Workloads.Any(e => e.Id == id);
        }

        private List<SelectListItem> GetAcademicYears(int startOffset = -2, int yearsCount = 5)
        {
            var currentYear = DateTime.Now.Year;
            var startYear = currentYear + startOffset; // начиная с прошлого года
            var years = new List<SelectListItem>();
            for (int i = 0; i < yearsCount; i++)
            {
                var year = startYear + i;
                var value = $"{year}-{year + 1}";
                years.Add(new SelectListItem { Value = value, Text = value });
            }
            return years;
        }
    }
}