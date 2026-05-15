using VinhKhanh.Services;

namespace VinhKhanh.Middleware
{
    public class DeviceActivityMiddleware
    {
        private const string DeviceHeaderName = "X-Device-Id";

        private readonly RequestDelegate _next;
        private readonly DeviceTouchQueue _queue;

        public DeviceActivityMiddleware(RequestDelegate next, DeviceTouchQueue queue)
        {
            _next = next;
            _queue = queue;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (context.Response.StatusCode >= 400)
                return;

            if (!context.Request.Headers.TryGetValue(DeviceHeaderName, out var headerValue))
                return;

            var maThietBi = headerValue.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(maThietBi) || maThietBi.Length > 100)
                return;

            _queue.TryEnqueue(maThietBi);
        }
    }
}
