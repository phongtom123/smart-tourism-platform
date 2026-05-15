namespace MauiApp1.Services;

public interface IImageGallerySaver
{
    /// <summary>
    /// Lưu ảnh PNG vào thư mục Pictures/Gallery của thiết bị.
    /// Trả về đường dẫn file đã lưu.
    /// </summary>
    Task<string> SavePngToGalleryAsync(byte[] pngBytes, string fileName);
}
