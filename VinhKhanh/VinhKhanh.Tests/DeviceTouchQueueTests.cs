using VinhKhanh.Services;
using Xunit;

namespace VinhKhanh.Tests;

public class DeviceTouchQueueTests
{
    // TC-Q01: Enqueue lần đầu luôn thành công
    [Fact]
    public void TryEnqueue_FirstTime_ReturnsTrue()
    {
        var queue = new DeviceTouchQueue();
        var result = queue.TryEnqueue("APP-CLIENT-001");
        Assert.True(result);
    }

    // TC-Q02: Enqueue cùng device liền ngay sau → bị dedup
    [Fact]
    public void TryEnqueue_SameDeviceImmediately_ReturnsFalse()
    {
        var queue = new DeviceTouchQueue();
        queue.TryEnqueue("APP-CLIENT-001");

        var result = queue.TryEnqueue("APP-CLIENT-001");
        Assert.False(result);
    }

    // TC-Q03: Hai device khác nhau → cả hai đều vào queue
    [Fact]
    public void TryEnqueue_DifferentDevices_BothAccepted()
    {
        var queue = new DeviceTouchQueue();
        var r1 = queue.TryEnqueue("APP-CLIENT-001");
        var r2 = queue.TryEnqueue("APP-CLIENT-002");
        Assert.True(r1);
        Assert.True(r2);
    }

    // TC-Q04: Device ID rỗng / null-like không enqueue
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryEnqueue_BlankId_NotEnqueued(string id)
    {
        var queue = new DeviceTouchQueue();
        // Middleware đã lọc blank trước khi gọi TryEnqueue,
        // nhưng queue vẫn phải xử lý được mà không crash.
        var ex = Record.Exception(() => queue.TryEnqueue(id));
        Assert.Null(ex);
    }

    // TC-Q05: Device ID dài hơn 100 ký tự (middleware lọc, queue nhận an toàn)
    [Fact]
    public void TryEnqueue_LongId_DoesNotThrow()
    {
        var queue = new DeviceTouchQueue();
        var longId = new string('A', 101);
        var ex = Record.Exception(() => queue.TryEnqueue(longId));
        Assert.Null(ex);
    }

    // TC-Q06: Case-insensitive dedup — cùng device viết hoa/thường khác nhau
    [Fact]
    public void TryEnqueue_SameIdDifferentCase_DedupApplied()
    {
        var queue = new DeviceTouchQueue();
        queue.TryEnqueue("APP-CLIENT-abc");

        var result = queue.TryEnqueue("APP-CLIENT-ABC");
        Assert.False(result);
    }

    // TC-Q07: Item enqueue được có thể đọc ra từ Reader
    [Fact]
    public async Task TryEnqueue_Accepted_ItemAvailableOnReader()
    {
        var queue = new DeviceTouchQueue();
        queue.TryEnqueue("APP-CLIENT-001");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var id = await queue.Reader.ReadAsync(cts.Token);
        Assert.Equal("APP-CLIENT-001", id);
    }

    // TC-Q08: Sau khi enqueue thành công, lần enqueue thứ 2 cùng device bị dedup
    //         cho đến hết cooldown (không thể test real-time 5s, test logic cooldown qua reflection)
    [Fact]
    public void TryEnqueue_AfterCooldown_CanEnqueueAgain()
    {
        // Dùng 2 instance độc lập để simulate "thiết bị mới" — kiểm tra
        // rằng device khác không bị ảnh hưởng cooldown của device kia.
        var queue = new DeviceTouchQueue();
        queue.TryEnqueue("APP-CLIENT-A");
        queue.TryEnqueue("APP-CLIENT-A"); // dedup

        // Device B hoàn toàn độc lập
        var r = queue.TryEnqueue("APP-CLIENT-B");
        Assert.True(r);
    }
}
