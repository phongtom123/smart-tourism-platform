using AndroidContext = global::Android.Content.Context;
using AndroidBitmap = global::Android.Graphics.Bitmap;
using AndroidCanvas = global::Android.Graphics.Canvas;
using AndroidPaint = global::Android.Graphics.Paint;
using AndroidPaintFlags = global::Android.Graphics.PaintFlags;
using AndroidColor = global::Android.Graphics.Color;

namespace MauiApp1.Platforms.Android.Maps;

internal static class UserLocationMarkerFactory
{
    public static AndroidBitmap Create(AndroidContext context)
    {
        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        var size = (int)(56 * density);
        var bitmap = AndroidBitmap.CreateBitmap(size, size, AndroidBitmap.Config.Argb8888!)!;
        var canvas = new AndroidCanvas(bitmap);

        using var paint = new AndroidPaint(AndroidPaintFlags.AntiAlias);

        var cx = size / 2f;
        var cy = size / 2f;
        var outerRadius = 21f * density;
        var innerRadius = 17f * density;

        paint.Color = AndroidColor.White;
        canvas.DrawCircle(cx, cy, outerRadius, paint);

        paint.Color = AndroidColor.Rgb(34, 139, 230);
        canvas.DrawCircle(cx, cy, innerRadius, paint);

        paint.Color = AndroidColor.White;
        canvas.DrawCircle(cx, cy - (5.5f * density), 4.5f * density, paint);

        paint.StrokeWidth = 2.8f * density;
        paint.SetStyle(AndroidPaint.Style.Stroke);
        canvas.DrawArc(
            cx - (8f * density),
            cy - (1.5f * density),
            cx + (8f * density),
            cy + (12f * density),
            200,
            140,
            false,
            paint);

        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.Color = AndroidColor.Argb(110, 34, 139, 230);
        canvas.DrawCircle(cx, cy, 26f * density, paint);

        return bitmap;
    }
}
