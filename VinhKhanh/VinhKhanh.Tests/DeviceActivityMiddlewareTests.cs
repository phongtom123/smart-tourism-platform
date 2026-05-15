using Microsoft.AspNetCore.Http;
using VinhKhanh.Middleware;
using VinhKhanh.Services;
using Xunit;

namespace VinhKhanh.Tests;

public class DeviceActivityMiddlewareTests
{
    private static DefaultHttpContext MakeContext(int statusCode, string? deviceId = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.StatusCode = statusCode;
        if (deviceId != null)
            ctx.Request.Headers["X-Device-Id"] = deviceId;
        return ctx;
    }

    private static DeviceActivityMiddleware MakeMiddleware(DeviceTouchQueue queue, int statusCode, string? deviceId)
    {
        return new DeviceActivityMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            },
            queue);
    }

    // TC-M01: Request thành công có header → enqueue
    [Fact]
    public async Task Invoke_SuccessWithHeader_Enqueues()
    {
        var queue = new DeviceTouchQueue();
        var mw = MakeMiddleware(queue, 200, "APP-CLIENT-001");
        var ctx = MakeContext(200, "APP-CLIENT-001");

        await mw.InvokeAsync(ctx);

        using var cts = new CancellationTokenSource(200);
        var id = await queue.Reader.ReadAsync(cts.Token);
        Assert.Equal("APP-CLIENT-001", id);
    }

    // TC-M02: Request lỗi 400 → không enqueue dù có header
    [Fact]
    public async Task Invoke_Status400_DoesNotEnqueue()
    {
        var queue = new DeviceTouchQueue();
        var mw = MakeMiddleware(queue, 400, "APP-CLIENT-001");
        var ctx = MakeContext(400, "APP-CLIENT-001");

        await mw.InvokeAsync(ctx);

        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => queue.Reader.ReadAsync(cts.Token).AsTask());
    }

    // TC-M03: Request thành công nhưng không có header → không enqueue
    [Fact]
    public async Task Invoke_NoHeader_DoesNotEnqueue()
    {
        var queue = new DeviceTouchQueue();
        var mw = MakeMiddleware(queue, 200, null);
        var ctx = MakeContext(200, null);

        await mw.InvokeAsync(ctx);

        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => queue.Reader.ReadAsync(cts.Token).AsTask());
    }

    // TC-M04: Header có giá trị rỗng → không enqueue
    [Fact]
    public async Task Invoke_EmptyHeader_DoesNotEnqueue()
    {
        var queue = new DeviceTouchQueue();
        var mw = MakeMiddleware(queue, 200, "   ");
        var ctx = MakeContext(200, "   ");

        await mw.InvokeAsync(ctx);

        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => queue.Reader.ReadAsync(cts.Token).AsTask());
    }

    // TC-M05: Header dài hơn 100 ký tự → không enqueue
    [Fact]
    public async Task Invoke_HeaderTooLong_DoesNotEnqueue()
    {
        var queue = new DeviceTouchQueue();
        var longId = new string('X', 101);
        var mw = MakeMiddleware(queue, 200, longId);
        var ctx = MakeContext(200, longId);

        await mw.InvokeAsync(ctx);

        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => queue.Reader.ReadAsync(cts.Token).AsTask());
    }

    // TC-M06: Request 500 → không enqueue
    [Fact]
    public async Task Invoke_Status500_DoesNotEnqueue()
    {
        var queue = new DeviceTouchQueue();
        var mw = MakeMiddleware(queue, 500, "APP-CLIENT-001");
        var ctx = MakeContext(500, "APP-CLIENT-001");

        await mw.InvokeAsync(ctx);

        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => queue.Reader.ReadAsync(cts.Token).AsTask());
    }

    // TC-M07: Hai request từ 2 device khác nhau → cả hai vào queue
    [Fact]
    public async Task Invoke_TwoDifferentDevices_BothEnqueued()
    {
        var queue = new DeviceTouchQueue();
        var mw1 = MakeMiddleware(queue, 200, "APP-CLIENT-001");
        var mw2 = MakeMiddleware(queue, 200, "APP-CLIENT-002");

        await mw1.InvokeAsync(MakeContext(200, "APP-CLIENT-001"));
        await mw2.InvokeAsync(MakeContext(200, "APP-CLIENT-002"));

        using var cts = new CancellationTokenSource(300);
        var id1 = await queue.Reader.ReadAsync(cts.Token);
        var id2 = await queue.Reader.ReadAsync(cts.Token);

        Assert.Contains("APP-CLIENT-001", new[] { id1, id2 });
        Assert.Contains("APP-CLIENT-002", new[] { id1, id2 });
    }
}
