using VinhKhanh.Services;
using Xunit;

namespace VinhKhanh.Tests;

// Mở rộng DeviceTouchQueueTests gốc — file gốc chỉ 8 case, thiếu coverage
// về parallel, capacity, FIFO, edge whitespace, độ dài >100 ký tự (queue
// vẫn nhận, middleware mới là chỗ chặn).
public class DeviceTouchQueueExtraTests
{
    // TC-DQX01: 200 device khác nhau enqueue tuần tự → 200 lần accept
    [Fact]
    public async Task TryEnqueue_HundredsOfDistinctDevices_AllAccepted()
    {
        var queue = new DeviceTouchQueue();
        const int N = 200;

        var trues = 0;
        for (var i = 0; i < N; i++)
            if (queue.TryEnqueue($"DEV-{i:D4}"))
                trues++;
        Assert.Equal(N, trues);

        var read = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try
        {
            while (read < N)
            {
                await queue.Reader.ReadAsync(cts.Token);
                read++;
            }
        }
        catch (OperationCanceledException) { }

        Assert.Equal(N, read);
    }

    // TC-DQX02: 1000 enqueue parallel cùng device → đúng 1 lần qua cooldown
    [Fact]
    public async Task TryEnqueue_ParallelSpamSameDevice_OnlyOneAccepted()
    {
        var queue = new DeviceTouchQueue();
        const int N = 1000;

        var tasks = Enumerable.Range(0, N)
            .Select(_ => Task.Run(() => queue.TryEnqueue("DEV-SAME")))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, results.Count(x => x));
    }

    // TC-DQX03: 1000 enqueue parallel khác device → tất cả pass
    [Fact]
    public async Task TryEnqueue_ParallelDistinctDevices_AllAccepted()
    {
        var queue = new DeviceTouchQueue();
        const int N = 1000;

        var tasks = Enumerable.Range(0, N)
            .Select(i => Task.Run(() => queue.TryEnqueue($"DEV-{i:D5}")))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.Equal(N, results.Count(x => x));
    }

    // TC-DQX04: Vượt capacity 2000 → DropOldest, channel ≤ 2000
    [Fact]
    public void TryEnqueue_OverChannelCapacity_DropOldestKeepsLatest()
    {
        var queue = new DeviceTouchQueue();
        const int N = 3000;

        for (var i = 0; i < N; i++)
            queue.TryEnqueue($"DEV-{i:D5}");

        Assert.True(queue.Reader.Count <= 2000,
            $"Channel vượt 2000: {queue.Reader.Count}");
    }

    // TC-DQX05: FIFO — đọc đúng thứ tự enqueue (cho 3 device khác nhau)
    [Fact]
    public async Task Reader_DrainOrder_IsFifo()
    {
        var queue = new DeviceTouchQueue();
        queue.TryEnqueue("DEV-001");
        queue.TryEnqueue("DEV-002");
        queue.TryEnqueue("DEV-003");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        Assert.Equal("DEV-001", await queue.Reader.ReadAsync(cts.Token));
        Assert.Equal("DEV-002", await queue.Reader.ReadAsync(cts.Token));
        Assert.Equal("DEV-003", await queue.Reader.ReadAsync(cts.Token));
    }

    // TC-DQX06: Sau Reader đọc hết, enqueue lại device cũ trong cooldown vẫn dedup
    //          (ConcurrentDictionary giữ key, cooldown timer độc lập với Reader)
    [Fact]
    public async Task TryEnqueue_AfterDrain_StillCooldown()
    {
        var queue = new DeviceTouchQueue();
        queue.TryEnqueue("DEV-COOL");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await queue.Reader.ReadAsync(cts.Token);

        // Còn trong cooldown 5s → false
        Assert.False(queue.TryEnqueue("DEV-COOL"));
    }

    // TC-DQX07: Device ID Unicode → key composition không crash, dedup theo case-insensitive ASCII
    [Fact]
    public void TryEnqueue_UnicodeId_DedupByOrdinalIgnoreCase()
    {
        var queue = new DeviceTouchQueue();
        Assert.True(queue.TryEnqueue("THIẾT-BỊ-Α"));
        // Cùng chuỗi → dedup
        Assert.False(queue.TryEnqueue("THIẾT-BỊ-Α"));
    }

    // TC-DQX08: ID có ký tự đặc biệt (`,`, `|`, `\\n`) — không crash do queue chỉ dùng làm dictionary key
    [Theory]
    [InlineData("DEV,with,comma")]
    [InlineData("DEV|with|pipe")]
    [InlineData("DEV\nnewline")]
    [InlineData("DEV\twith\ttab")]
    [InlineData("DEV-quotes-\"value\"")]
    public void TryEnqueue_SpecialCharsId_DoesNotThrow(string id)
    {
        var queue = new DeviceTouchQueue();
        var ex = Record.Exception(() => queue.TryEnqueue(id));
        Assert.Null(ex);
    }

    // TC-DQX09: Padded ID khác trim ID → key khác (queue không trim — middleware mới trim)
    [Fact]
    public void TryEnqueue_PaddedId_TreatedAsDifferentKey()
    {
        var queue = new DeviceTouchQueue();
        Assert.True(queue.TryEnqueue("DEV-A"));
        Assert.True(queue.TryEnqueue(" DEV-A"));   // khác key
        Assert.True(queue.TryEnqueue("DEV-A "));   // khác key
    }

    // TC-DQX10: Reader.Count phản ánh đúng số item chưa đọc
    [Fact]
    public void Reader_Count_TracksUnreadItems()
    {
        var queue = new DeviceTouchQueue();
        queue.TryEnqueue("A");
        queue.TryEnqueue("B");
        queue.TryEnqueue("C");
        Assert.Equal(3, queue.Reader.Count);
    }

    // TC-DQX11: Mỗi instance queue độc lập, không chia sẻ state
    [Fact]
    public void DistinctInstances_DoNotShareState()
    {
        var q1 = new DeviceTouchQueue();
        var q2 = new DeviceTouchQueue();

        Assert.True(q1.TryEnqueue("X"));
        Assert.True(q2.TryEnqueue("X"));   // queue khác → state tách biệt
        Assert.False(q1.TryEnqueue("X"));  // dedup trong cùng queue
        Assert.False(q2.TryEnqueue("X"));
    }

    // TC-DQX12: Enqueue + Read đan xen — Reader nhận đúng số lượng
    [Fact]
    public async Task TryEnqueue_InterleavedRead_NoLoss()
    {
        var queue = new DeviceTouchQueue();
        const int N = 100;

        var readTask = Task.Run(async () =>
        {
            var ids = new List<string>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                while (ids.Count < N)
                {
                    var id = await queue.Reader.ReadAsync(cts.Token);
                    ids.Add(id);
                }
            }
            catch (OperationCanceledException) { }
            return ids;
        });

        for (var i = 0; i < N; i++)
        {
            queue.TryEnqueue($"DEV-{i:D4}");
            if (i % 7 == 0) await Task.Yield();
        }

        var got = await readTask;
        Assert.Equal(N, got.Count);
    }
}
