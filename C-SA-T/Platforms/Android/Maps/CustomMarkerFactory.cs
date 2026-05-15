using AndroidContext = global::Android.Content.Context;
using AndroidBitmap = global::Android.Graphics.Bitmap;
using AndroidCanvas = global::Android.Graphics.Canvas;
using AndroidPaint = global::Android.Graphics.Paint;
using AndroidPaintFlags = global::Android.Graphics.PaintFlags;
using AndroidColor = global::Android.Graphics.Color;
using AndroidRectF = global::Android.Graphics.RectF;
using AndroidPath = global::Android.Graphics.Path;

namespace MauiApp1.Platforms.Android.Maps;

internal static class CustomMarkerFactory
{
    public static AndroidBitmap Create(AndroidContext context, double rating, AndroidBitmap? imageBitmap = null)
    {
        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;

        int width = (int)(86 * density);
        int height = (int)(112 * density);

        var bitmap = AndroidBitmap.CreateBitmap(width, height, AndroidBitmap.Config.Argb8888!)!;
        var canvas = new AndroidCanvas(bitmap);

        using var anti = new AndroidPaint(AndroidPaintFlags.AntiAlias);

        float centerX = width / 2f;
        float imageRadius = 28f * density;
        float imageCenterY = 36f * density;

        // White border
        anti.Color = AndroidColor.White;
        canvas.DrawCircle(centerX, imageCenterY, imageRadius + (3f * density), anti);

        // Draw image if available, otherwise draw solid circle
        if (imageBitmap != null)
        {
            try
            {
                DrawCircularImage(canvas, imageBitmap, centerX, imageCenterY, imageRadius, density);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CustomMarkerFactory] Error drawing circular image: {ex.Message}");
                // Fallback to solid circle
                anti.Color = AndroidColor.Rgb(43, 43, 43);
                canvas.DrawCircle(centerX, imageCenterY, imageRadius, anti);
            }
        }
        else
        {
            anti.Color = AndroidColor.Rgb(43, 43, 43);
            canvas.DrawCircle(centerX, imageCenterY, imageRadius, anti);
        }

        // Rating badge
        anti.Color = AndroidColor.Rgb(245, 102, 31);
        float ratingTop = imageCenterY + imageRadius - (7f * density);
        float ratingBottom = ratingTop + (20f * density);
        var ratingRect = new AndroidRectF(centerX - (28f * density), ratingTop, centerX + (28f * density), ratingBottom);
        canvas.DrawRoundRect(ratingRect, 10f * density, 10f * density, anti);

        using var textPaint = new AndroidPaint(AndroidPaintFlags.AntiAlias)
        {
            Color = AndroidColor.White,
            TextSize = 12f * density,
            TextAlign = AndroidPaint.Align.Center,
            FakeBoldText = true
        };

        canvas.DrawText($"{rating:0.0} ★", centerX, ratingTop + (14f * density), textPaint);

        // Bottom circles
        anti.Color = AndroidColor.Rgb(255, 213, 192);
        canvas.DrawCircle(centerX, height - (18f * density), 16f * density, anti);

        anti.Color = AndroidColor.Rgb(245, 140, 92);
        canvas.DrawCircle(centerX, height - (18f * density), 10f * density, anti);

        return bitmap;
    }

    private static void DrawCircularImage(AndroidCanvas canvas, AndroidBitmap imageBitmap, float centerX, float centerY, float radius, float density)
    {
        try
        {
            int diameter = (int)(radius * 2);
            
            // Scale the image to fit the circle
            var scaledBitmap = AndroidBitmap.CreateScaledBitmap(imageBitmap, diameter, diameter, true);
            if (scaledBitmap == null)
            {
                System.Diagnostics.Debug.WriteLine("[CustomMarkerFactory] Failed to scale bitmap");
                return;
            }

            // Create a temporary bitmap for the circular result
            var circleBitmap = AndroidBitmap.CreateBitmap(diameter, diameter, AndroidBitmap.Config.Argb8888!);
            if (circleBitmap == null)
            {
                System.Diagnostics.Debug.WriteLine("[CustomMarkerFactory] Failed to create circle bitmap");
                scaledBitmap?.Dispose();
                return;
            }

            var circleCanvas = new AndroidCanvas(circleBitmap);

            // Draw the scaled image
            using var imagePaint = new AndroidPaint(AndroidPaintFlags.AntiAlias | AndroidPaintFlags.FilterBitmap);
            circleCanvas.DrawBitmap(scaledBitmap, 0, 0, imagePaint);

            // Create a circular path and clip to it
            using var path = new AndroidPath();
            path.AddCircle(radius, radius, radius, AndroidPath.Direction.Ccw!);

            // Create a new bitmap with circular clip
            var finalCircleBitmap = AndroidBitmap.CreateBitmap(diameter, diameter, AndroidBitmap.Config.Argb8888!);
            if (finalCircleBitmap == null)
            {
                System.Diagnostics.Debug.WriteLine("[CustomMarkerFactory] Failed to create final circle bitmap");
                circleBitmap?.Dispose();
                scaledBitmap?.Dispose();
                return;
            }

            var finalCanvas = new AndroidCanvas(finalCircleBitmap);
            using var clipPaint = new AndroidPaint(AndroidPaintFlags.AntiAlias);
            finalCanvas.ClipPath(path);
            finalCanvas.DrawBitmap(circleBitmap, 0, 0, clipPaint);

            // Draw the final circular image on the main canvas
            using var mainPaint = new AndroidPaint(AndroidPaintFlags.AntiAlias);
            canvas.DrawBitmap(finalCircleBitmap, centerX - radius, centerY - radius, mainPaint);

            System.Diagnostics.Debug.WriteLine($"[CustomMarkerFactory] Successfully drew circular image at ({centerX}, {centerY}) with radius {radius}");

            // Cleanup
            scaledBitmap?.Dispose();
            circleBitmap?.Dispose();
            finalCircleBitmap?.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomMarkerFactory] Exception in DrawCircularImage: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }
}
