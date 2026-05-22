using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PCKManagementSystem.Data;
using PCKManagementSystem.Models;
using PCKManagementSystem.Models.ViewModels;
using System.Security.Claims;

namespace PCKManagementSystem.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProfileController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return !string.IsNullOrEmpty(userIdClaim) ? int.Parse(userIdClaim) : 0;
        }

        // GET: Profile
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);

            var viewModel = new ProfileViewModel
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                FullName = user.FullName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                Roles = roles.ToList(),

                // Статистика пользователя
                DocumentsCreated = await _context.Documents.CountAsync(d => d.AuthorId == userId),
                DocumentsApproved = await _context.Documents.CountAsync(d => d.ApprovedById == userId),
                TasksAssigned = await _context.Tasks.CountAsync(t => t.AssignedById == userId),
                TasksCompleted = await _context.Tasks.CountAsync(t => t.AssignedToId == userId && t.Status == Models.TaskStatus.Completed),
                TasksOverdue = await _context.Tasks.CountAsync(t => t.AssignedToId == userId && t.Status == Models.TaskStatus.Overdue),
                WorkloadHours = await _context.Workloads.Where(w => w.TeacherId == userId).SumAsync(w => w.Hours),

                // Последние действия
                RecentActivities = await _context.AuditLogs
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.ActionDate)
                    .Take(5)
                    .Select(a => new UserActivityLogViewModel
                    {
                        ActionType = a.ActionType,
                        Description = a.ActionDescription,
                        Date = a.ActionDate,
                        EntityType = a.EntityType
                    })
                    .ToListAsync()
            };

            return View(viewModel);
        }

        // GET: Profile/Edit
        public async Task<IActionResult> Edit()
        {
            var userId = GetCurrentUserId();
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                return NotFound();
            }

            var viewModel = new ProfileEditViewModel
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName
            };

            return View(viewModel);
        }

        // POST: Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = GetCurrentUserId();
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                return NotFound();
            }

            // Проверяем, не занят ли email
            if (user.Email != model.Email)
            {
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Этот email уже используется");
                    return View(model);
                }
                user.Email = model.Email;
                user.UserName = model.Email;
            }

            user.FullName = model.FullName;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Профиль успешно обновлен";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // POST: Profile/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Проверьте правильность введенных данных" });
            }

            var userId = GetCurrentUserId();
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                return Json(new { success = false, message = "Пользователь не найден" });
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (result.Succeeded)
            {
                return Json(new { success = true, message = "Пароль успешно изменен" });
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, message = errors });
        }

        // GET: Profile/Activity
        public async Task<IActionResult> Activity(int page = 1, int pageSize = 20)
        {
            var userId = GetCurrentUserId();

            var query = _context.AuditLogs
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.ActionDate);

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new UserActivityLogViewModel
                {
                    ActionType = a.ActionType,
                    Description = a.ActionDescription,
                    Date = a.ActionDate,
                    EntityType = a.EntityType,
                    IpAddress = a.IpAddress,
                    AdditionalInfo = a.AdditionalInfo
                })
                .ToListAsync();

            var viewModel = new UserActivityListViewModel
            {
                Activities = items,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
            };

            return View(viewModel);
        }
    }
}