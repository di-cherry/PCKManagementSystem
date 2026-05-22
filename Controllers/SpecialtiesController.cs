using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PCKManagementSystem.Data;
using PCKManagementSystem.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PCKManagementSystem.Controllers
{
    [Authorize(Roles = "Администратор,Председатель ПЦК, Преподаватель")]
    public class SpecialtiesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SpecialtiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Specialties
        public async Task<IActionResult> Index(string sortOrder, string searchString)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["CodeSortParm"] = sortOrder == "code" ? "code_desc" : "code";
            ViewData["NameSortParm"] = sortOrder == "name" ? "name_desc" : "name";
            ViewData["DisciplinesSortParm"] = sortOrder == "disciplines" ? "disciplines_desc" : "disciplines";
            ViewData["CurrentFilter"] = searchString;

            var specialties = _context.Specialties
                .Include(s => s.Disciplines)
                .AsQueryable();

            // Поиск
            if (!string.IsNullOrEmpty(searchString))
            {
                specialties = specialties.Where(s => s.Code.Contains(searchString) || s.Name.Contains(searchString));
            }

            // Сортировка
            switch (sortOrder)
            {
                case "code":
                    specialties = specialties.OrderBy(s => s.Code);
                    break;
                case "code_desc":
                    specialties = specialties.OrderByDescending(s => s.Code);
                    break;
                case "name":
                    specialties = specialties.OrderBy(s => s.Name);
                    break;
                case "name_desc":
                    specialties = specialties.OrderByDescending(s => s.Name);
                    break;
                case "disciplines":
                    specialties = specialties.OrderBy(s => s.Disciplines.Count());
                    break;
                case "disciplines_desc":
                    specialties = specialties.OrderByDescending(s => s.Disciplines.Count());
                    break;
                default:
                    specialties = specialties.OrderBy(s => s.Code);
                    break;
            }

            return View(await specialties.ToListAsync());
        }

        // GET: Specialties/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var specialty = await _context.Specialties
                .Include(s => s.Disciplines)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (specialty == null) return NotFound();

            return View(specialty);
        }

        // GET: Specialties/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Specialties/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Code,Name")] Specialty specialty)
        {
            if (ModelState.IsValid)
            {
                // Проверка уникальности кода
                if (await _context.Specialties.AnyAsync(s => s.Code == specialty.Code))
                {
                    ModelState.AddModelError("Code", "Специальность с таким кодом уже существует");
                    return View(specialty);
                }

                _context.Add(specialty);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Специальность успешно добавлена";
                return RedirectToAction(nameof(Index));
            }
            return View(specialty);
        }

        // GET: Specialties/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var specialty = await _context.Specialties.FindAsync(id);
            if (specialty == null) return NotFound();

            return View(specialty);
        }

        // POST: Specialties/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Code,Name")] Specialty specialty)
        {
            if (id != specialty.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Проверка уникальности кода (исключая текущую)
                    if (await _context.Specialties.AnyAsync(s => s.Code == specialty.Code && s.Id != id))
                    {
                        ModelState.AddModelError("Code", "Специальность с таким кодом уже существует");
                        return View(specialty);
                    }

                    _context.Update(specialty);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Специальность успешно обновлена";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SpecialtyExists(specialty.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(specialty);
        }

        // GET: Specialties/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var specialty = await _context.Specialties
                .Include(s => s.Disciplines)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (specialty == null) return NotFound();

            // Проверяем, есть ли связанные дисциплины
            ViewBag.HasRelatedDisciplines = specialty.Disciplines.Any();

            return View(specialty);
        }

        // POST: Specialties/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var specialty = await _context.Specialties
                .Include(s => s.Disciplines)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (specialty == null) return NotFound();

            if (specialty.Disciplines.Any())
            {
                TempData["Error"] = "Нельзя удалить специальность, так как к ней привязаны дисциплины";
                return RedirectToAction(nameof(Index));
            }

            _context.Specialties.Remove(specialty);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Специальность успешно удалена";
            return RedirectToAction(nameof(Index));
        }

        private bool SpecialtyExists(int id)
        {
            return _context.Specialties.Any(e => e.Id == id);
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel(bool? onlyWithDisciplines)
        {
            var query = _context.Specialties.Include(s => s.Disciplines).AsQueryable();

            if (onlyWithDisciplines == true)
            {
                query = query.Where(s => s.Disciplines.Any());
            }

            var specialties = await query.ToListAsync();

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Специальности");

            worksheet.Cells[1, 1].Value = "Код";
            worksheet.Cells[1, 2].Value = "Название";
            worksheet.Cells[1, 3].Value = "Количество дисциплин";
            worksheet.Cells[1, 1, 1, 3].Style.Font.Bold = true;

            int row = 2;
            foreach (var spec in specialties)
            {
                worksheet.Cells[row, 1].Value = spec.Code;
                worksheet.Cells[row, 2].Value = spec.Name;
                worksheet.Cells[row, 3].Value = spec.Disciplines?.Count ?? 0;
                row++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            var stream = new MemoryStream(package.GetAsByteArray());
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Specialties_{(onlyWithDisciplines == true ? "withDisciplines" : "all")}.xlsx");
        }
    }
}