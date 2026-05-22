using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PCKManagementSystem.Data;
using PCKManagementSystem.Models;
using PCKManagementSystem.Models.ViewModels;

namespace PCKManagementSystem.Controllers
{
    [Authorize]
    public class TeachersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public TeachersController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Teachers
        public async Task<IActionResult> Index(string searchString, string position, string degree, string qualification)
        {
            var roleNames = new[] { "Преподаватель", "Председатель ПЦК" };
            var usersInRoles = await _userManager.GetUsersInRoleAsync("Преподаватель");
            var chairmen = await _userManager.GetUsersInRoleAsync("Председатель ПЦК");
            var allTeachers = usersInRoles.Union(chairmen).Distinct().AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                allTeachers = allTeachers.Where(u => u.FullName.Contains(searchString) ||
                                                    u.Email.Contains(searchString) ||
                                                    (u.Position != null && u.Position.Contains(searchString)));
            }
            if (!string.IsNullOrEmpty(position))
                allTeachers = allTeachers.Where(u => u.Position == position);
            if (!string.IsNullOrEmpty(degree))
                allTeachers = allTeachers.Where(u => u.Degree == degree);
            if (!string.IsNullOrEmpty(qualification))
                allTeachers = allTeachers.Where(u => u.Qualification == qualification);

            var model = allTeachers.Select(u => new TeacherViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Position = u.Position,
                Degree = u.Degree,
                AcademicTitle = u.AcademicTitle,
                EducationLevel = u.EducationLevel,
                Qualification = u.Qualification,
                AdvancedTraining = u.AdvancedTraining,
                ProfessionalRetraining = u.ProfessionalRetraining,
                ExperienceYears = u.ExperienceYears,
                IsActive = u.IsActive
            }).ToList();

            // Для выпадающих списков фильтров
            ViewBag.Positions = await _context.Users.Where(u => !string.IsNullOrEmpty(u.Position)).Select(u => u.Position).Distinct().ToListAsync();
            ViewBag.Degrees = await _context.Users.Where(u => !string.IsNullOrEmpty(u.Degree)).Select(u => u.Degree).Distinct().ToListAsync();
            ViewBag.Qualifications = await _context.Users.Where(u => !string.IsNullOrEmpty(u.Qualification)).Select(u => u.Qualification).Distinct().ToListAsync();

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentPosition"] = position;
            ViewData["CurrentDegree"] = degree;
            ViewData["CurrentQualification"] = qualification;

            return View(model);
        }

        // GET: Teachers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Проверяем, что пользователь действительно преподаватель или председатель
            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("Преподаватель") && !roles.Contains("Председатель ПЦК"))
                return Forbid();

            var model = new TeacherViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Position = user.Position,
                Degree = user.Degree,
                AcademicTitle = user.AcademicTitle,
                EducationLevel = user.EducationLevel,
                Qualification = user.Qualification,
                AdvancedTraining = user.AdvancedTraining,
                ProfessionalRetraining = user.ProfessionalRetraining,
                ExperienceYears = user.ExperienceYears,
                IsActive = user.IsActive
            };

            // Дополнительно можно загрузить нагрузку преподавателя
            var workloads = await _context.Workloads
                .Include(w => w.Discipline)
                .Where(w => w.TeacherId == user.Id)
                .ToListAsync();
            ViewBag.Workloads = workloads;

            return View(model);
        }

        // GET: Teachers/Edit/5
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("Преподаватель") && !roles.Contains("Председатель ПЦК"))
                return Forbid();

            var model = new TeacherEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Position = user.Position,
                Degree = user.Degree,
                AcademicTitle = user.AcademicTitle,
                EducationLevel = user.EducationLevel,
                Qualification = user.Qualification,
                AdvancedTraining = user.AdvancedTraining,
                ProfessionalRetraining = user.ProfessionalRetraining,
                ExperienceYears = user.ExperienceYears,
                IsActive = user.IsActive
            };

            return View(model);
        }

        // POST: Teachers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Edit(int id, TeacherEditViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null) return NotFound();

                user.FullName = model.FullName;
                user.Email = model.Email;
                user.UserName = model.Email; // если UserName = Email
                user.Position = model.Position;
                user.Degree = model.Degree;
                user.AcademicTitle = model.AcademicTitle;
                user.EducationLevel = model.EducationLevel;
                user.Qualification = model.Qualification;
                user.AdvancedTraining = model.AdvancedTraining;
                user.ProfessionalRetraining = model.ProfessionalRetraining;
                user.ExperienceYears = model.ExperienceYears;
                user.IsActive = model.IsActive;

                _context.Update(user);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Данные преподавателя обновлены";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string searchString, string position, string degree, string qualification)
        {
            // Логика фильтрации (такая же как в Index)
            var roleNames = new[] { "Преподаватель", "Председатель ПЦК" };
            var usersInRoles = await _userManager.GetUsersInRoleAsync("Преподаватель");
            var chairmen = await _userManager.GetUsersInRoleAsync("Председатель ПЦК");
            var allTeachers = usersInRoles.Union(chairmen).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                allTeachers = allTeachers.Where(u => u.FullName.Contains(searchString));
            if (!string.IsNullOrEmpty(position))
                allTeachers = allTeachers.Where(u => u.Position == position);
            if (!string.IsNullOrEmpty(degree))
                allTeachers = allTeachers.Where(u => u.Degree == degree);
            if (!string.IsNullOrEmpty(qualification))
                allTeachers = allTeachers.Where(u => u.Qualification == qualification);

            var teachers = allTeachers.ToList();

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Преподаватели");

            ws.Cells[1, 1].Value = "ФИО";
            ws.Cells[1, 2].Value = "Email";
            ws.Cells[1, 3].Value = "Должность";
            ws.Cells[1, 4].Value = "Учёная степень";
            ws.Cells[1, 5].Value = "Учёное звание";
            ws.Cells[1, 6].Value = "Уровень образования";
            ws.Cells[1, 7].Value = "Квалификация";
            ws.Cells[1, 8].Value = "Повышение квалификации";
            ws.Cells[1, 9].Value = "Проф. переподготовка";
            ws.Cells[1, 10].Value = "Опыт (лет)";
            ws.Cells[1, 11].Value = "Активен";
            ws.Cells[1, 1, 1, 11].Style.Font.Bold = true;

            int row = 2;
            foreach (var t in teachers)
            {
                ws.Cells[row, 1].Value = t.FullName;
                ws.Cells[row, 2].Value = t.Email;
                ws.Cells[row, 3].Value = t.Position;
                ws.Cells[row, 4].Value = t.Degree;
                ws.Cells[row, 5].Value = t.AcademicTitle;
                ws.Cells[row, 6].Value = t.EducationLevel;
                ws.Cells[row, 7].Value = t.Qualification;
                ws.Cells[row, 8].Value = t.AdvancedTraining;
                ws.Cells[row, 9].Value = t.ProfessionalRetraining;
                ws.Cells[row, 10].Value = t.ExperienceYears;
                ws.Cells[row, 11].Value = t.IsActive ? "Да" : "Нет";
                row++;
            }

            ws.Cells[ws.Dimension.Address].AutoFitColumns();
            var stream = new MemoryStream(package.GetAsByteArray());
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Teachers.xlsx");
        }
    }
}