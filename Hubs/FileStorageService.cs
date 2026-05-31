using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using PCKManagementSystem.Hubs;

namespace PCKManagementSystem.Hubs
{
    public class FileStorageService : IFileStorageService
    {
        private readonly string _basePath;
        private readonly IWebHostEnvironment _env;
        private readonly IHostEnvironment _hostEnv;

        public FileStorageService(IWebHostEnvironment env, IHostEnvironment hostEnv)
        {
            _env = env;
            _hostEnv = hostEnv;
            // Определяем базовый путь: если среда разработки, используем wwwroot, иначе /data
            if (_hostEnv.IsDevelopment())
                _basePath = Path.Combine(_env.WebRootPath, "uploads");
            else
                _basePath = "/data/uploads";  // постоянный том в Amvera
        }

        public async Task<string> SaveFileAsync(IFormFile file, string subFolder)
        {
            var folder = Path.Combine(_basePath, subFolder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(folder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            // Возвращаем относительный путь для хранения в БД
            return $"/uploads/{subFolder}/{fileName}";
        }

        public async Task<byte[]> GetFileAsync(string filePath)
        {
            // filePath может быть относительным или абсолютным. Приводим к абсолютному.
            string fullPath;
            if (filePath.StartsWith("/uploads/"))
            {
                fullPath = Path.Combine(_basePath, filePath.Substring(9));
            }
            else
            {
                fullPath = Path.Combine(_basePath, filePath);
            }
            if (!File.Exists(fullPath))
                return null;
            return await File.ReadAllBytesAsync(fullPath);
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            string fullPath;
            if (filePath.StartsWith("/uploads/"))
                fullPath = Path.Combine(_basePath, filePath.Substring(9));
            else
                fullPath = Path.Combine(_basePath, filePath);
            if (!File.Exists(fullPath))
                return false;
            File.Delete(fullPath);
            return true;
        }

        public string GetRelativePath(string fullPath)
        {
            // Если путь уже относительный, возвращаем как есть
            if (fullPath.StartsWith("/uploads/"))
                return fullPath;
            // Иначе генерируем относительный (но этот метод обычно не нужен)
            return fullPath;
        }
    }
}