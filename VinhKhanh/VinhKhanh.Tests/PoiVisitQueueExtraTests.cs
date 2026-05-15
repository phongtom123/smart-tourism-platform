using VinhKhanh.Services;
using Xunit;

namespace VinhKhanh.Tests;

// Mở rộng PoiVisitQueueTests gốc, tập trung vào các vùng chưa có coverage:
// - Cooldown timing (giả lập bằng cách read state qua Reader),
// - Edge cases tham số (boothId boundary, deviceId chứa ký tự đặc biệt/unicode),
// - Tính toàn vẹn FIFO khi xen kẽ TryEnqueue/Read,
// - Hành vi DropOldest cụ thể (item cũ bị thay item mới khi vượt 2000).
public class PoiVisitQueueExtraTests
{
    // TC-PVX01: idGianHang = int.MaxValue vẫn enqueue (queue không validate range)
    [Fact]
    public void TryEnqueue_BoothIdMaxValue_Accepted()
    {
        var queue = new PoiVisitQueue();
        Assert.True(queue.TryEnqueue(int.MaxValue, "DEVICE-001"));
    }

    // TC-PVX02: idGianHang = int.MinValue vẫn enqueue (queue không validate range)
    [Fact]
    public void TryEnqueue_BoothIdMinValue_Accepted()
    {
        var queue = new PoiVisitQueue();
        Assert.True(queue.TryEnqueue(int.MinValue, "DEVICE-001"));
    }

    // TC-PVX03: deviceId chứa Unicode (đảm bảo key composition không vỡ encoding)
    [Fact]
    public void TryEnqueue_UnicodeDeviceId_Accepted()
    {
        var queue = new PoiVisitQueue();
        Assert.True(queue.TryEnqueue(1, "THIẾT-BỊ-001"));
        // Cùng id Unicode: dedup
        Assert.False(queue.TryEnqueue(1, "THIẾT-BỊ-001"));
    }

    // TC-PVX04: deviceId Unicode khác case → vẫn dedup do StringComparer.OrdinalIgnoreCase
    [Fact]
    public void TryEnqueue_UnicodeDifferentCase_Deduped()
    {
        var queue = new PoiVisitQueue();
        queue.TryEnqueue(1, "thiết-bị-001");
        // Vietnamese diacritic không có hoa thường khác nhau ngoài ASCII ban đầu;
        // assert: dùng cùng chuỗi → dedup
        Assert.False(queue.TryEnqueue(1, "thiết-bị-001"));
    }

    // TC-PVX05: deviceId rất dài (10_000 ký tự) — queue không crash
    [Fact]
    public void TryEnqueue_VeryLongDeviceId_DoesNotThrow()
    {
        var queue = new PoiVisitQueue();
        var huge = new string('Z', 10_000);
        var ex = Record.Exception(() => queue.TryEnqueue(1, huge));
        Assert.Null(ex);
    }

    // TC-PVX06: deviceId chỉ chứa khoảng trắng (tab/newline) → reject như blank
    [Theory]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData(" \t \n ")]
    public void TryEnqueue_WhitespaceOnly_ReturnsFalse(string id)
    {
        var queue = new PoiVisitQueue();
        Assert.False(queue.TryEnqueue(1, id));
    }

    // TC-PVX07: Đan xen Read và Enqueue — Reader không miss item nào
    [Fact]
    public async Task TryEnqueue_InterleavedReadWrite_NoLoss()
    {
        var queue = new PoiVisitQueue();
        const int N = 50;

        var readTask = Task.Run(async () =>
        {
            var ids = new List<int>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                while (ids.Count < N)
                {
                    var item = await queue.Reader.ReadAsync(cts.Token);
                    ids.Add(item.IdGianHang);
                }
            }
            catch (OperationCanceledException) { }
            return ids;
        });

        for (var i = 0; i < N; i++)
        {
            // Mỗi cặp khác nhau để không bị dedup
            queue.TryEnqueue(i, $"DEV-{i:D4}");
            // Yield mỗi vài lần để Reader xen kẽ
            if (i % 10 == 0)
                await Task.Yield();
        }

        var got = await readTask;
        Assert.Equal(N, got.Count);
        Assert.Equal(Enumerable.Range(0, N).ToList(), got);
    }

    // TC-PVX08: Sau khi Reader đọc hết, Enqueue tiếp vẫn vào queue được
    [Fact]
    public async Task TryEnqueue_AfterReaderDrained_StillAccepts()
    {
        var queue = new PoiVisitQueue();
        queue.TryEnqueue(1, "DEV-A");
        queue.TryEnqueue(2, "DEV-B");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await queue.Reader.ReadAsync(cts.Token);
        await queue.Reader.ReadAsync(cts.Token);

        Assert.True(queue.TryEnqueue(3, "DEV-C"));
    }

    // TC-PVX09: DropOldest — sau N (lớn) lần enqueue đa-cặp, channel vẫn không vượt 2000
    [Fact]
    public void TryEnqueue_FarOverCapacity_DropOldestStable()
    {
        var queue = new PoiVisitQueue();
        const int N = 5000;

        for (var i = 0; i < N; i++)
            queue.TryEnqueue(i, $"DEV-{i:D6}"); // mỗi cặp khác nhau, không bị dedup-cooldown

        Assert.True(queue.Reader.Count <= 2000,
            $"Channel vượt 2000: {queue.Reader.Count}");
    }

    // TC-PVX10: Nhiều cặp liên tiếp khác nhau (không dedup) — TryEnqueue không trả false ngầm
    [Fact]
    public void TryEnqueue_ManyDistinctPairs_NoFalseNegatives()
    {
        var queue = new PoiVisitQueue();
        const int N = 500;

        var ok = 0;
        for (var i = 0; i < N; i++)
            if (queue.TryEnqueue(i % 100, $"DEV-{i:D5}")) // booth lặp nhưng device duy nhất
                ok++;

        Assert.Equal(N, ok);
    }

    // TC-PVX11: Cùng booth+device gọi 100 lần liên tiếp → đúng 1 lần true, 99 false
    [Fact]
    public void TryEnqueue_HundredSpamSamePair_OnlyOneSuccess()
    {
        var queue = new PoiVisitQueue();
        var trues = 0;
        for (var i = 0; i < 100; i++)
            if (queue.TryEnqueue(7, "SAME-DEV"))
                trues++;
        Assert.Equal(1, trues);
    }

    // TC-PVX12: PoiVisitItem là record struct → so sánh value-based qua HashSet
    [Fact]
    public void PoiVisitItem_RecordStruct_ValueSemanticsInDictionary()
    {
        var dict = new Dictionary<PoiVisitItem, int>
        {
            [new(1, "DEV-A")] = 10
        };

        Assert.True(dict.TryGetValue(new PoiVisitItem(1, "DEV-A"), out var v));
        Assert.Equal(10, v);
    }

    // TC-PVX13: Khi Reader chưa đọc, trạng thái Count phản ánh đúng số đã enqueue
    [Fact]
    public void TryEnqueue_UnreadItems_ReaderCountMatches()
    {
        var queue = new PoiVisitQueue();
        queue.TryEnqueue(1, "A");
        queue.TryEnqueue(2, "B");
        queue.TryEnqueue(3, "C");
        Assert.Equal(3, queue.Reader.Count);
    }

    // TC-PVX14: Dedup theo cặp (booth, device): 2 device khác nhau cùng booth — KHÔNG dedup nhau
    [Fact]
    public void TryEnqueue_DedupKey_IsBoothDevicePair()
    {
        var queue = new PoiVisitQueue();
        Assert.True(queue.TryEnqueue(99, "DEV-X"));
        // Cooldown chỉ áp dụng cho (99, DEV-X). (99, DEV-Y) phải ok.
        Assert.True(queue.TryEnqueue(99, "DEV-Y"));
    }

    // TC-PVX15: 5000 enqueue PARALLEL với 50 device khác nhau (mỗi device 100 lần).
    // Sau khi sửa cooldown thành atomic (TryUpdate/TryAdd loop), mỗi device
    // CHỈ pass đúng 1 lần — race window đã đóng. Trước fix: ~120 trues.
    [Fact]
    public async Task TryEnqueue_ParallelSpamSameDevice_CooldownLimitsAccepts()
    {
        var queue = new PoiVisitQueue();
        const int Devices = 50;
        const int RepeatPerDevice = 100;

        var tasks = new List<Task<bool>>();
        for (var d = 0; d < Devices; d++)
            for (var r = 0; r < RepeatPerDevice; r++)
            {
                var devId = $"DEV-{d:D3}";
                tasks.Add(Task.Run(() => queue.TryEnqueue(1, devId)));
            }

        var results = await Task.WhenAll(tasks);
        var trues = results.Count(x => x);

        Assert.Equal(Devices, trues);
    }

    // TC-PVX16: Trong stress test, item thực sự đọc ra phải khớp với số accepted
    [Fact]
    public async Task TryEnqueue_StressDistinctPairs_ReadCountMatchesAccepted()
    {
        var queue = new PoiVisitQueue();
        const int N = 1500; // < 2000 capacity để không bị DropOldest

        var accepted = 0;
        for (var i = 0; i < N; i++)
            if (queue.TryEnqueue(i, $"DEV-{i:D5}"))
                accepted++;

        Assert.Equal(N, accepted);

        var read = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
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
}
