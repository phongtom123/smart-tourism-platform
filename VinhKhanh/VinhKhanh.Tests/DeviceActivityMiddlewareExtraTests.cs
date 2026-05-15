using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using VinhKhanh.Middleware;
using VinhKhanh.Services;
using Xunit;

namespace VinhKhanh.Tests;

// Mở rộng DeviceActivityMiddlewareTests gốc — bổ sung biên status code,
// header có nhiều giá trị, ký tự whitespace nội bộ, độ dài đúng 100, và
// tương tác Middleware ↔ Queue.
public class DeviceActivityMiddlewareExtraTests
{
    private static (DeviceActivityMiddleware mw, DefaultHttpContext ctx) Build(
        DeviceTouchQueue queue,
        int statusCode,
        StringValues headerValue,
        bool setHeader)
    {
        var ctx = new DefaultHttpContext();
        if (setHeader)
            ctx.Request.Headers["X-Device-Id"] = headerValue;

        var mw = new DeviceActivityMiddleware(
            innerCtx =>
            {
                innerCtx.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            },
            queue);

        return (mw, ctx);
    }

    private static async Task<bool> WasEnqueuedAsync(DeviceTouchQueue queue, int timeoutMs = 100)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await queue.Reader.ReadAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    // TC-MX01: Status 399 (mọi mã <400) → enqueue
    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(399)]
    public async Task Invoke_NonErrorStatus_Enqueues(int status)
    {
        var queue = new DeviceTouchQueue();
        var (mw, ctx) = Build(queue, status, "DEV-OK", setHeader: true);

        await mw.InvokeAsync(ctx);

        Assert.True(await WasEnqueuedAsync(queue));
    }

    // TC-MX02: Status ≥ 400 (rổ lỗi) → KHÔNG enqueue
    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task Invoke_ErrorStatus_DoesNotEnqueue(int status)
    {
        var queue = new DeviceTouchQueue();
        var (mw, ctx) = Build(queue, status, "DEV-ERR", setHeader: true);

        await mw.InvokeAsync(ctx);

        Assert.False(await WasEnqueuedAsync(queue));
    }

    // TC-MX03: Header dài đúng 100 ký tự → enqueue (boundary cho phép)
    [Fact]
    public async Task Invoke_HeaderExactly100Chars_Enqueues()
    {
        var queue = new DeviceTouchQueue();
        var id = new string('A', 100);
        var (mw, ctx) = Build(queue, 200, id, setHeader: true);

        await mw.InvokeAsync(ctx);

        Assert.True(await WasEnqueuedAsync(queue));
    }

    // TC-MX04: Header dài đúng 101 ký tự → KHÔNG enqueue (boundary chặn)
    [Fact]
    public async Task Invoke_HeaderExactly101Chars_DoesNotEnqueue()
    {
        var queue = new DeviceTouchQueue();
        var id = new string('A', 101);
        var (mw, ctx) = Build(queue, 200, id, setHeader: true);

        await mw.InvokeAsync(ctx);

        Assert.False(await WasEnqueuedAsync(queue));
    }

    // TC-MX05: Header có whitespace bao quanh giá trị → middleware Trim trước khi enqueue
    [Fact]
    public async Task Invoke_HeaderWithSurroundingWhitespace_EnqueuesTrimmed()
    {
        var queue = new DeviceTouchQueue();
        var (mw, ctx) = Build(queue, 200, "  DEV-TRIM  ", setHeader: true);

        await mw.InvokeAsync(ctx);

        using var cts = new CancellationTokenSource(200);
        var id = await queue.Reader.ReadAsync(cts.Token);
        Assert.Equal("DEV-TRIM", id);
    }

    // TC-MX06: Header tab/newline-only → coi như blank, KHÔNG enqueue
    [Theory]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("   \t   ")]
    public async Task Invoke_HeaderOnlyWhitespace_DoesNotEnqueue(string raw)
    {
        var queue = new DeviceTouchQueue();
        var (mw, ctx) = Build(queue, 200, raw, setHeader: true);

        await mw.InvokeAsync(ctx);

        Assert.False(await WasEnqueuedAsync(queue));
    }

    // TC-MX07: Header có 2 giá trị (ASP.NET nối comma) — middleware xử lý theo .ToString()
    //          → vẫn enqueue chuỗi composite (queue không validate format)
    [Fact]
    public async Task Invoke_HeaderMultipleValues_EnqueuesAsComposite()
    {
        var queue = new DeviceTouchQueue();
        var (mw, ctx) = Build(queue, 200, new StringValues(new[] { "DEV-X", "DEV-Y" }), setHeader: true);

        await mw.InvokeAsync(ctx);

        using var cts = new CancellationTokenSource(200);
        var id = await queue.Reader.ReadAsync(cts.Token);
        // .ToString() trên StringValues nhiều phần tử nối bằng comma
        Assert.Equal("DEV-X,DEV-Y", id);
    }

    // TC-MX08: Hai request liên tiếp cùng device (cùng inner queue) → request 2 bị dedup ở queue
    //          (Middleware không dedup, queue dedup trong cooldown 5s)
    [Fact]
    public async Task Invoke_SameDeviceTwice_SecondCallDedupedByQueue()
    {
        var queue = new DeviceTouchQueue();
        var (mw1, ctx1) = Build(queue, 200, "DEV-DUP", setHeader: true);
        var (mw2, ctx2) = Build(queue, 200, "DEV-DUP", setHeader: true);

        await mw1.InvokeAsync(ctx1);
        await mw2.InvokeAsync(ctx2);

        using var cts = new CancellationTokenSource(200);
        var first = await queue.Reader.ReadAsync(cts.Token);
        Assert.Equal("DEV-DUP", first);

        // Không còn item thứ 2
        Assert.False(await WasEnqueuedAsync(queue, 100));
    }

    // TC-MX09: Inner pipeline ném exception — middleware không nuốt lỗi
    [Fact]
    public async Task Invoke_InnerPipelineThrows_BubblesUp()
    {
        var queue = new DeviceTouchQueue();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Device-Id"] = "DEV-OK";

        var mw = new DeviceActivityMiddleware(
            _ => throw new InvalidOperationException("inner failure"),
            queue);

        await Assert.ThrowsAsync<InvalidOperationException>(() => mw.InvokeAsync(ctx));
    }

    // TC-MX10: Status code mặc định ASP.NET (200) khi inner không set → enqueue
    [Fact]
    public async Task Invoke_DefaultStatusCode_Enqueues()
    {
        var queue = new DeviceTouchQueue();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Device-Id"] = "DEV-DEFAULT";

        var mw = new DeviceActivityMiddleware(_ => Task.CompletedTask, queue);

        await mw.InvokeAsync(ctx);

        Assert.True(await WasEnqueuedAsync(queue));
    }

    // TC-MX11: Header X-Device-Id (case-insensitive theo HTTP spec) — verify ASP.NET HeaderDictionary
    [Theory]
    [InlineData("X-Device-Id")]
    [InlineData("x-device-id")]
    [InlineData("X-DEVICE-ID")]
    [InlineData("X-Device-ID")]
    public async Task Invoke_HeaderNameCaseInsensitive_Enqueues(string headerName)
    {
        var queue = new DeviceTouchQueue();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[headerName] = "DEV-CASE";

        var mw = new DeviceActivityMiddleware(
            innerCtx => { innerCtx.Response.StatusCode = 200; return Task.CompletedTask; },
            queue);

        await mw.InvokeAsync(ctx);

        Assert.True(await WasEnqueuedAsync(queue));
    }
}
