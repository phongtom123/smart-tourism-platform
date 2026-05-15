using Android.Content;
using Android.OS;
using Android.Provider;
using MauiApp1.Services;

namespace MauiApp1.Platforms.Android;

public class ImageGallerySaver : IImageGallerySaver
{
    public async Task<string> SavePngToGalleryAsync(byte[] pngBytes, string fileName)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            return await SaveViaMediaStoreAsync(pngBytes, fileName);

        return await SaveToExternalStorageAsync(pngBytes, fileName);
    }

    // API 29+ — không cần WRITE_EXTERNAL_STORAGE
    private static Task<string> SaveViaMediaStoreAsync(byte[] pngBytes, string fileName)
    {
        var context = global::Android.App.Application.Context;
        var resolver = context.ContentResolver!;

        var values = new ContentValues();
        values.Put("_display_name", fileName);
        values.Put("mime_type", "image/png");
        values.Put("relative_path", "Pictures/VinhKhanh");
        values.Put("is_pending", 1);

        var uri = resolver.Insert(MediaStore.Images.Media.ExternalContentUri!, values)
            ?? throw new IOException("MediaStore: không thể tạo entry ảnh.");

        using (var stream = resolver.OpenOutputStream(uri)!)
            stream.Write(pngBytes, 0, pngBytes.Length);

        values.Clear();
        values.Put("is_pending", 0);
        resolver.Update(uri, values, null, null);

        return Task.FromResult(uri.ToString()!);
    }

    // API < 29 — lưu vào public Pictures
    private static async Task<string> SaveToExternalStorageAsync(byte[] pngBytes, string fileName)
    {
        var picturesDir = global::Android.OS.Environment.GetExternalStoragePublicDirectory(
            global::Android.OS.Environment.DirectoryPictures)!.AbsolutePath;
        var dir = Path.Combine(picturesDir, "VinhKhanh");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, pngBytes);

        // Thông báo Media Scanner để ảnh xuất hiện ngay trong Gallery
        var context = global::Android.App.Application.Context;
        var mediaScanIntent = new Intent(Intent.ActionMediaScannerScanFile);
        mediaScanIntent.SetData(global::Android.Net.Uri.FromFile(new Java.IO.File(path)));
        context.SendBroadcast(mediaScanIntent);

        return path;
    }
}
