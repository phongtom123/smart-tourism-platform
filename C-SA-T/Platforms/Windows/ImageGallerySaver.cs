using MauiApp1.Services;

namespace MauiApp1.Platforms.Windows;

public class ImageGallerySaver : IImageGallerySaver
{
    public async Task<string> SavePngToGalleryAsync(byte[] pngBytes, string fileName)
    {
        var picturesDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var dir = Path.Combine(picturesDir, "VinhKhanh");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, pngBytes);
        return path;
    }
}
