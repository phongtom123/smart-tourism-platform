using System.Collections.Concurrent;
using System.Threading.Channels;

namespace VinhKhanh.Services
{
    /// <summary>
    /// Nhận (idGianHang, maThietBi) từ PoiController, dedup tại chỗ trong cửa sổ
    /// EnqueueCooldown để tránh ngập channel khi cùng thiết bị quét lại liên tục.
    /// Dedup theo ngày vẫn do DB đảm nhiệm khi worker flush.
    /// </summary>
    public sealed class PoiVisitQueue
    {
        private static readonly TimeSpan EnqueueCooldown = TimeSpan.FromSeconds(5);

        private readonly Channel<PoiVisitItem> _channel;
        private readonly ConcurrentDictionary<string, long> _lastEnqueuedTicks = new(StringComparer.OrdinalIgnoreCase);

        public PoiVisitQueue()
        {
            _channel = Channel.CreateBounded<PoiVisitItem>(new BoundedChannelOptions(2000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        }

        /// <summary>
        /// Trả về false nếu cặp (idGianHang, maThietBi) vừa enqueue trong EnqueueCooldown.
        /// </summary>
        public bool TryEnqueue(int idGianHang, string maThietBi)
        {
            if (string.IsNullOrWhiteSpace(maThietBi))
                return false;

            var key = idGianHang.ToString() + "|" + maThietBi;
            var nowTicks = DateTime.UtcNow.Ticks;
            var cooldownTicks = EnqueueCooldown.Ticks;

            // Atomic check-and-set: chỉ duy nhất một thread "thắng" trong cửa sổ cooldown.
            // Spin retry khi race với thread khác cùng key.
            while (true)
            {
                if (_lastEnqueuedTicks.TryGetValue(key, out var lastTicks))
                {
                    if (nowTicks - lastTicks < cooldownTicks)
                        return false;

                    if (_lastEnqueuedTicks.TryUpdate(key, nowTicks, lastTicks))
                        break;
                }
                else if (_lastEnqueuedTicks.TryAdd(key, nowTicks))
                {
                    break;
                }
            }

            return _channel.Writer.TryWrite(new PoiVisitItem(idGianHang, maThietBi));
        }

        internal ChannelReader<PoiVisitItem> Reader => _channel.Reader;
    }

    public readonly record struct PoiVisitItem(int IdGianHang, string MaThietBi);

    /// <summary>
    /// Background worker đọc PoiVisitQueue, gom batch, flush DB mỗi 5s hoặc khi đủ 50 items.
    /// </summary>
    public sealed class PoiVisitWorker : BackgroundService
    {
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);
        private const int MaxBatchSize = 50;

        private readonly PoiVisitQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PoiVisitWorker> _logger;

        public PoiVisitWorker(
            PoiVisitQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<PoiVisitWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batch = new HashSet<PoiVisitItem>();

            while (!stoppingToken.IsCancellationRequested)
            {
                using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                flushCts.CancelAfter(FlushInterval);

                try
                {
                    while (batch.Count < MaxBatchSize)
                    {
                        var item = await _queue.Reader.ReadAsync(flushCts.Token);
                        batch.Add(item);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Flush window het hoac shutdown — flush nhung gi dang co
                }

                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch, stoppingToken);
                    batch.Clear();
                }
            }
        }

        private async Task FlushBatchAsync(HashSet<PoiVisitItem> items, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var gianHangService = scope.ServiceProvider.GetRequiredService<GianHangService>();
                var counted = await gianHangService.FlushVisitBatchAsync(items, ct);
                _logger.LogDebug("PoiVisitWorker: flushed {Count} items, {Counted} new visits recorded.", items.Count, counted);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "PoiVisitWorker: batch flush failed for {Count} items.", items.Count);
            }
        }
    }
}
