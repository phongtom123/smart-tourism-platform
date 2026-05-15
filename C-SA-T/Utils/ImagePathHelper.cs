using System;
using System.IO;
using System.Threading.Tasks;

namespace MauiApp1.Utils
{
    public class ImagePathHelper
    {
        public ImagePathHelper()
        {
        }

        public async Task<string?> GetImagePathAsync(string dbImagePath)
        {
            if (string.IsNullOrWhiteSpace(dbImagePath))
                return null;

            if (dbImagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                dbImagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return dbImagePath;
            }

            if (Path.IsPathRooted(dbImagePath) && File.Exists(dbImagePath))
            {
                return dbImagePath;
            }

            var appDataPath = FileSystem.AppDataDirectory;
            var resourcePath = Path.Combine(appDataPath, "Resources", "Images", dbImagePath);
            if (File.Exists(resourcePath))
            {
                return resourcePath;
            }

            var cachePath = Path.Combine(FileSystem.CacheDirectory, "Images", dbImagePath);
            if (File.Exists(cachePath))
            {
                return cachePath;
            }

            System.Diagnostics.Debug.WriteLine($"[ImagePathHelper] Image not found: {dbImagePath}");
            await Task.CompletedTask;
            return null;
        }

        public async Task CopyImagesToResourcesAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ImagePathHelper] Images are packaged with the app from Resources\\Images folder");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImagePathHelper] Error: {ex.Message}");
            }
        }
    }
}
