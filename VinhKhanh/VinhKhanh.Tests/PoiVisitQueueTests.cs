using VinhKhanh.Services;
using Xunit;

namespace VinhKhanh.Tests;

public class PoiVisitQueueTests
{
    // TC-PV01: Enqueue lan dau luon thanh cong
    [Fact]
    public void TryEnqueue_FirstTime_ReturnsTrue()
    {
        var queue = new PoiVisitQueue();
        var result = queue.TryEnqueue(1, "DEVICE-001");
        Assert.True(result);
    }

    // TC-PV02: Cung (booth, device) lien tiep -> dedup
    [Fact]
    public void TryEnqueue_SamePairImmediately_Deduped()
    {
        var queue = new PoiVisitQueue();
        queue.TryEnqueue(1, "DEVICE-001");
        var result = queue.TryEnqueue(1, "DEVICE-001");
        Assert.False(result);
    }

    // TC-PV03: Cung device, gian hang khac -> ca hai vao queue (dedup theo cap)
    [Fact]
    public void TryEnqueue_SameDeviceDifferentBooths_BothAccepted()
    {
        var queue = new PoiVisitQueue();
        var r1 = queue.TryEnqueue(1, "DEVICE-001");
        var r2 = queue.TryEnqueue(2, "DEVICE-001");
        Assert.True(r1);
        Assert.True(r2);
    }

    // TC-PV04: Cung gian hang, device khac nhau -> ca hai vao queue
    [Fact]
    public void TryEnqueue_DifferentDevicesSameBooth_BothAccepted()
    {
        var queue = new PoiVisitQueue();
        var r1 = queue.TryEnqueue(1, "DEVICE-001");
        var r2 = queue.TryEnqueue(1, "DEVICE-002");
        Assert.True(r1);
        Assert.True(r2);
    }

    // TC-PV05: Device ID rong/null -> reject (PoiController da chan, queue van phai an toan)
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryEnqueue_BlankDeviceId_ReturnsFalse(string? deviceId)
    {
        var queue = new PoiVisitQueue();
        var result = queue.TryEnqueue(1, deviceId!);
        Assert.False(result);
    }

    // TC-PV06: Dedup case-insensitive cho deviceId
    [Fact]
    public void TryEnqueue_SameDeviceDifferentCase_Deduped()
    {
        var queue = new PoiVisitQueue();
        queue.TryEnqueue(1, "device-abc");
        var result = queue.TryEnqueue(1, "DEVICE-ABC");
        Assert.False(result);
    }

    // TC-PV07: Item enqueue thanh cong se doc duoc tu Reader
    [Fact]
    public async Task TryEnqueue_Accepted_ItemAvailableOnReader()
    {
        var queue = new PoiVisitQueue();
        queue.TryEnqueue(7, "DEVICE-001");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var item = await queue.Reader.ReadAsync(cts.Token);
        Assert.Equal(7, item.IdGianHang);
        Assert.Equal("DEVICE-001", item.MaThietBi);
    }

    // TC-PV08: Burst N device khac nhau cung gian hang -> tat ca enqueue duoc
    [Fact]
    public async Task TryEnqueue_BurstDifferentDevicesSameBooth_AllAccepted()
    {
        var queue = new PoiVisitQueue();
        const int N = 200;

        var accepted = 0;
        for (var i = 0; i < N; i++)
        {
            if (queue.TryEnqueue(1, $"DEVICE-{i:D4}"))
                accepted++;
        }

        Assert.Equal(N, accepted);

        // Doc them de chac chan tat ca da nam trong channel
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

    // TC-PV09: Cung device quet 2 gian hang xen ke -> khong dedup nham giua cap
    [Fact]
    public void TryEnqueue_AlternatingPairs_DedupPerPair()
    {
        var queue = new PoiVisitQueue();
        Assert.True(queue.TryEnqueue(1, "DEVICE-001"));
        Assert.True(queue.TryEnqueue(2, "DEVICE-001"));
        Assert.False(queue.TryEnqueue(1, "DEVICE-001")); // dedup cap (1, DEVICE-001)
        Assert.False(queue.TryEnqueue(2, "DEVICE-001")); // dedup cap (2, DEVICE-001)
    }

    // TC-PV10: BoothId = 0 (edge): queue khong validate ID, van enqueue duoc
    [Fact]
    public void TryEnqueue_BoothIdZero_Accepted()
    {
        var queue = new PoiVisitQueue();
        Assert.True(queue.TryEnqueue(0, "DEVICE-001"));
    }

    // TC-PV11: BoothId am (du lieu hong tu client): queue van nhan, validation o tang tren
    [Fact]
    public void TryEnqueue_BoothIdNegative_Accepted()
    {
        var queue = new PoiVisitQueue();
        Assert.True(queue.TryEnqueue(-5, "DEVICE-001"));
    }

    // TC-PV12: 1 device quet 5 gian hang khac nhau -> ca 5 deu duoc enqueue
    [Fact]
    public void TryEnqueue_OneDeviceManyBooths_AllAccepted()
    {
        var queue = new PoiVisitQueue();
        for (var boothId = 1; boothId <= 5; boothId++)
        {
            Assert.True(queue.TryEnqueue(boothId, "DEVICE-001"),
                $"Booth {boothId} phai duoc accept");
        }
    }

    // TC-PV13: Spam cung 1 cap 5 lan lien tiep -> chi lan dau true, 4 lan sau false
    [Fact]
    public void TryEnqueue_SpamSamePair_OnlyFirstAccepted()
    {
        var queue = new PoiVisitQueue();
        Assert.True(queue.TryEnqueue(1, "DEVICE-001"));
        for (var i = 0; i < 4; i++)
            Assert.False(queue.TryEnqueue(1, "DEVICE-001"));
    }

    // TC-PV14: Whitespace deviceId co content khac nhau tao key khac (queue khong trim)
    //          Behavior nay duoc dam bao dung o tang sau (NormalizeDeviceId trong GianHangService).
    [Fact]
    public void TryEnqueue_PaddedDeviceId_TreatedAsDifferentKey()
    {
        var queue = new PoiVisitQueue();
        Assert.True(queue.TryEnqueue(1, "DEVICE-001"));
        Assert.True(queue.TryEnqueue(1, " DEVICE-001 "));
    }

    // TC-PV15: Reader tra FIFO theo thu tu enqueue
    [Fact]
    public async Task TryEnqueue_MultipleItems_ReadOrderIsFifo()
    {
        var queue = new PoiVisitQueue();
        queue.TryEnqueue(1, "DEVICE-A");
        queue.TryEnqueue(2, "DEVICE-B");
        queue.TryEnqueue(3, "DEVICE-C");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var i1 = await queue.Reader.ReadAsync(cts.Token);
        var i2 = await queue.Reader.ReadAsync(cts.Token);
        var i3 = await queue.Reader.ReadAsync(cts.Token);

        Assert.Equal(1, i1.IdGianHang);
        Assert.Equal(2, i2.IdGianHang);
        Assert.Equal(3, i3.IdGianHang);
    }

    // TC-PV16: PoiVisitItem cung gia tri => equal (record struct)
    [Fact]
    public void PoiVisitItem_SameValues_AreEqual()
    {
        var a = new PoiVisitItem(1, "DEVICE-001");
        var b = new PoiVisitItem(1, "DEVICE-001");
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    // TC-PV17: PoiVisitItem khac booth -> not equal (worker batch HashSet phai phan biet)
    [Fact]
    public void PoiVisitItem_DifferentBooth_NotEqual()
    {
        var a = new PoiVisitItem(1, "DEVICE-001");
        var b = new PoiVisitItem(2, "DEVICE-001");
        Assert.NotEqual(a, b);
    }

    // TC-PV18: PoiVisitItem khac device -> not equal
    [Fact]
    public void PoiVisitItem_DifferentDevice_NotEqual()
    {
        var a = new PoiVisitItem(1, "DEVICE-001");
        var b = new PoiVisitItem(1, "DEVICE-002");
        Assert.NotEqual(a, b);
    }

    // TC-PV19: HashSet<PoiVisitItem> tu dedup duplicate trong worker batch
    //          (worker chay batch.Add(item) — du item lap, hashset chi giu 1)
    [Fact]
    public void PoiVisitItem_HashSet_DedupsDuplicates()
    {
        var batch = new HashSet<PoiVisitItem>
        {
            new(1, "DEVICE-001"),
            new(1, "DEVICE-001"),
            new(1, "DEVICE-002"),
            new(2, "DEVICE-001"),
        };
        Assert.Equal(3, batch.Count);
    }

    // TC-PV20: Burst nhieu booth + nhieu device -> tat ca cap unique deu enqueue
    [Fact]
    public async Task TryEnqueue_MultiBoothMultiDevice_AllUniquePairsAccepted()
    {
        var queue = new PoiVisitQueue();
        const int Booths = 5;
        const int Devices = 20;

        var accepted = 0;
        for (var b = 1; b <= Booths; b++)
            for (var d = 1; d <= Devices; d++)
                if (queue.TryEnqueue(b, $"DEVICE-{d:D3}"))
                    accepted++;

        Assert.Equal(Booths * Devices, accepted);

        // Doc het channel de chac chan tat ca da nam trong
        var read = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try
        {
            while (read < Booths * Devices)
            {
                await queue.Reader.ReadAsync(cts.Token);
                read++;
            }
        }
        catch (OperationCanceledException) { }

        Assert.Equal(Booths * Devices, read);
    }

    // TC-PV21: 1000 device khac nhau cung gian hang, enqueue tuan tu -> ca 1000 deu accept
    [Fact]
    public async Task TryEnqueue_ThousandDevicesSameBooth_AllAccepted()
    {
        var queue = new PoiVisitQueue();
        const int N = 1000;

        var accepted = 0;
        for (var i = 0; i < N; i++)
            if (queue.TryEnqueue(1, $"DEV-{i:D5}"))
                accepted++;

        Assert.Equal(N, accepted);

        var read = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
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

    // TC-PV22: 1000 enqueue PARALLEL (Task.WhenAll) -> queue thread-safe, khong mat item
    [Fact]
    public async Task TryEnqueue_ThousandParallel_ThreadSafe()
    {
        var queue = new PoiVisitQueue();
        const int N = 1000;

        var tasks = Enumerable.Range(0, N)
            .Select(i => Task.Run(() => queue.TryEnqueue(1, $"DEV-{i:D5}")))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.Equal(N, results.Count(r => r));

        var read = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
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

    // TC-PV23: Vuot capacity 2000 -> DropOldest hoat dong, channel chi giu duoc 2000 item moi nhat
    [Fact]
    public void TryEnqueue_OverChannelCapacity_DropOldestKeepsLatest()
    {
        var queue = new PoiVisitQueue();
        const int N = 3000;

        for (var i = 0; i < N; i++)
            queue.TryEnqueue(1, $"DEV-{i:D5}");

        // Channel bounded 2000 voi DropOldest -> Reader.Count <= 2000
        Assert.True(queue.Reader.Count <= 2000,
            $"Channel khong duoc vuot 2000 item, thuc te = {queue.Reader.Count}");
        Assert.True(queue.Reader.Count >= 1900,
            $"DropOldest phai giu lai gan day channel, thuc te = {queue.Reader.Count}");
    }
}
