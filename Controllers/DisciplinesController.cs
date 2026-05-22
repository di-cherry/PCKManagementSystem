using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PCKManagementSystem.Data;
using PCKManagementSystem.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using PCKManagementSystem.Models.ViewModels;

namespace PCKManagementSystem.Controllers
{
    [Authorize] // Только авторизованные пользователи могут доступаться к контроллеру
    public class DisciplinesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DisciplinesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Disciplines
        // GET: Disciplines
        public async Task<IActionResult> Index(string sortOrder, string searchString, int? specialtyId)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["CodeSortParm"] = sortOrder == "code" ? "code_desc" : "code";
            ViewData["NameSortParm"] = sortOrder == "name" ? "name_desc" : "name";
            ViewData["SpecialtySortParm"] = sortOrder == "specialty" ? "specialty_desc" : "specialty";
            ViewData["CurrentFilter"] = searchString;

            var disciplines = _context.Disciplines
                .Include(d => d.Specialty)
                .AsQueryable();

            // Фильтр по специальности
            if (specialtyId.HasValue)
            {
                disciplines = disciplines.Where(d => d.SpecialtyId == specialtyId.Value);
                ViewData["CurrentSpecialtyId"] = specialtyId.Value;
            }

            // Поиск
            if (!string.IsNullOrEmpty(searchString))
            {
                disciplines = disciplines.Where(d => d.Name.Contains(searchString) || d.Code.Contains(searchString));
            }

            // Сортировка
            switch (sortOrder)
            {
                case "code":
                    disciplines = disciplines.OrderBy(d => d.Code);
                    break;
                case "code_desc":
                    disciplines = disciplines.OrderByDescending(d => d.Code);
                    break;
                case "name":
                    disciplines = disciplines.OrderBy(d => d.Name);
                    break;
                case "name_desc":
                    disciplines = disciplines.OrderByDescending(d => d.Name);
                    break;
                case "specialty":
                    disciplines = disciplines.OrderBy(d => d.Specialty.Name);
                    break;
                case "specialty_desc":
                    disciplines = disciplines.OrderByDescending(d => d.Specialty.Name);
                    break;
                default:
                    disciplines = disciplines.OrderBy(d => d.Code);
                    break;
            }

            // Заполнение выпадающего списка специальностей для фильтра
            ViewBag.Specialties = await _context.Specialties
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
                .ToListAsync();

            return View(await disciplines.ToListAsync());
        }

        // GET: Disciplines/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var discipline = await _context.Disciplines
                .Include(d => d.Specialty)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (discipline == null)
            {
                return NotFound();
            }

            return View(discipline);
        }

        // GET: Disciplines/Create
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public IActionResult Create()
        {
            ViewBag.Specialties = _context.Specialties
        .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
        .ToList();
            return View();
        }

        // POST: Disciplines/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Create([Bind("Name,Code,SpecialtyId")] Discipline discipline)
        {
            // Проверка состояния модели
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                TempData["Error"] = "Ошибка валидации: " + errors;
                ViewData["SpecialtyId"] = new SelectList(_context.Specialties, "Id", "Name", discipline.SpecialtyId);
                return View(discipline);
            }

            try
            {
                // Добавляем и сохраняем
                _context.Add(discipline);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Дисциплина успешно добавлена";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                // Ошибка базы данных (например, нарушение уникальности, внешнего ключа)
                TempData["Error"] = "Ошибка базы данных: " + ex.Message;
                if (ex.InnerException != null)
                    TempData["Error"] += " | Внутренняя ошибка: " + ex.InnerException.Message;
            }
            catch (Exception ex)
            {
                // Любая другая ошибка
                TempData["Error"] = "Общая ошибка: " + ex.Message;
            }

            ViewData["SpecialtyId"] = new SelectList(_context.Specialties, "Id", "Name", discipline.SpecialtyId);
            return View(discipline);
        }

        // GET: Disciplines/Edit/5
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var discipline = await _context.Disciplines.FindAsync(id);
            if (discipline == null)
            {
                return NotFound();
            }

            ViewBag.Specialties = _context.Specialties
        .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
        .ToList();
            return View(discipline);
        }

        // POST: Disciplines/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Code,SpecialtyId")] Discipline discipline)
        {
            if (id != discipline.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(discipline);
                    await _context.SaveChangesAsync();

                    // TODO: Добавить запись в AuditLog
                    TempData["Success"] = "Дисциплина успешно обновлена";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DisciplineExists(discipline.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["SpecialtyId"] = new SelectList(_context.Specialties, "Id", "Name", discipline.SpecialtyId);
            return View(discipline);
        }

        // GET: Disciplines/Delete/5
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var discipline = await _context.Disciplines
                .Include(d => d.Specialty)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (discipline == null)
            {
                return NotFound();
            }

            // Проверяем, есть ли связанные документы или нагрузка
            bool hasRelatedDocuments = await _context.Documents.AnyAsync(d => d.DisciplineId == id);
            bool hasRelatedWorkloads = await _context.Workloads.AnyAsync(w => w.DisciplineId == id);

            ViewBag.HasRelatedData = hasRelatedDocuments || hasRelatedWorkloads;

            return View(discipline);
        }

        // POST: Disciplines/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var discipline = await _context.Disciplines.FindAsync(id);

            if (discipline == null)
            {
                return NotFound();
            }

            // Проверяем еще раз перед удалением
            bool hasRelatedDocuments = await _context.Documents.AnyAsync(d => d.DisciplineId == id);
            bool hasRelatedWorkloads = await _context.Workloads.AnyAsync(w => w.DisciplineId == id);

            if (hasRelatedDocuments || hasRelatedWorkloads)
            {
                TempData["Error"] = "Нельзя удалить дисциплину, т.к. с ней связаны документы или учебная нагрузка";
                return RedirectToAction(nameof(Index));
            }

            _context.Disciplines.Remove(discipline);
            await _context.SaveChangesAsync();

            // TODO: Добавить запись в AuditLog
            TempData["Success"] = "Дисциплина успешно удалена";

            return RedirectToAction(nameof(Index));
        }

        private bool DisciplineExists(int id)
        {
            return _context.Disciplines.Any(e => e.Id == id);
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel()
        {
            var disciplines = await _context.Disciplines
                .Include(d => d.Specialty)
                .OrderBy(d => d.Code)
                .ToListAsync();

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Дисциплины");

            worksheet.Cells[1, 1].Value = "Код";
            worksheet.Cells[1, 2].Value = "Название";
            worksheet.Cells[1, 3].Value = "Специальность (код)";
            worksheet.Cells[1, 4].Value = "Специальность (название)";
            worksheet.Cells[1, 1, 1, 4].Style.Font.Bold = true;

            int row = 2;
            foreach (var d in disciplines)
            {
                worksheet.Cells[row, 1].Value = d.Code;
                worksheet.Cells[row, 2].Value = d.Name;
                worksheet.Cells[row, 3].Value = d.Specialty?.Code;
                worksheet.Cells[row, 4].Value = d.Specialty?.Name;
                row++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            var stream = new MemoryStream(package.GetAsByteArray());
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Disciplines.xlsx");
        }

        // GET: Disciplines/Import
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public IActionResult Import()
        {
            return View();
        }

        // POST: Disciplines/Import/Preview
        [HttpPost]
        [Authorize(Roles = "Администратор,Председатель ПЦК")]
        public async Task<IActionResult> PreviewImport(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Файл не выбран" });
            }

            if (!file.FileName.EndsWith(".xlsx"))
            {
                return Json(new { success = false, message = "Поддерживаются только файлы Excel (.xlsx)" });
            }

            var previewData = new List<DisciplineImportPreviewViewModel>();
            var allSpecialties = await _context.Specialties.ToListAsync();

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    if (worksheet == null || worksheet.Dimension == null)
                    {
                        return Json(new { success = false, message = "Файл не содержит данных" });
                    }

                    var rowCount = worksheet.Dimension.Rows;
                    for (int row = 2; row <= rowCount; row++) // начинаем со 2-й строки (1-я – заголовки)
                    {
                        var code = worksheet.Cells[row, 1].Text?.Trim();
                        var name = worksheet.Cells[row, 2].Text?.Trim();
                        var specialtyCode = worksheet.Cells[row, 3].Text?.Trim();

                        var previewItem = new DisciplineImportPreviewViewModel
                        {
                            RowNumber = row,
                            Code = code,
                            Name = name,
                            SpecialtyCode = specialtyCode
                        };

                        // Проверка обязательных полей
                        if (string.IsNullOrEmpty(code))
                        {
                            previewItem.IsValid = false;
                            previewItem.ErrorMessage = "Код дисциплины не указан";
                            previewData.Add(previewItem);
                            continue;
                        }

                        if (string.IsNullOrEmpty(name))
                        {
                            previewItem.IsValid = false;
                            previewItem.ErrorMessage = "Название дисциплины не указано";
                            previewData.Add(previewItem);
                            continue;
                        }

                        // Поиск специальности
                        var specialty = allSpecialties.FirstOrDefault(s => s.Code == specialtyCode);
                        var specialtyCodes = allSpecialties.Select(s => s.Code).ToList();
                        if (specialty == null)
                        {
                            previewItem.IsValid = false;
                            var available = string.Join(", ", specialtyCodes.Take(5));
                            if (specialtyCodes.Count > 5) available += $" и ещё {specialtyCodes.Count - 5}";
                            previewItem.ErrorMessage = $"Специальность с кодом '{specialtyCode}' не найдена. Доступные: {available}";
                            previewData.Add(previewItem);
                            continue;
                        }

                        previewItem.SpecialtyName = specialty.Name;
                        previewItem.IsValid = true;

                        // Проверяем, существует ли уже дисциплина с таким кодом
                        var existing = await _context.Disciplines.FirstOrDefaultAsync(d => d.Code == code);
                        if (existing != null)
                        {
                            previewItem.IsNew = false;
                            previewItem.ExistingDisciplineId = existing.Id;
                            previewItem.ErrorMessage = existing.Name == name ? "Будет обновлена (без изменений)" : $"Будет обновлена: {existing.Name} → {name}";
                        }
                        else
                        {
                            previewItem.IsNew = true;
                            previewItem.ErrorMessage = "Будет создана";
                        }

                        previewData.Add(previewItem);
                    }
                }
            }

            return Json(new { success = true, data = previewData });
        }
    }
}