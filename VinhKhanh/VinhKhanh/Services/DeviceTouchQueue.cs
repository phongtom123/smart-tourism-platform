using System.Collections.Concurrent;
using System.Threading.Channels;

namespace VinhKhanh.Services
{
    /// <summary>
    /// Nhận device ID từ middleware, dedup tại chỗ, đưa vào Channel cho worker xử lý batch.
    /// </summary>
    public sealed class DeviceTouchQueue
    {
        // Không enqueue lại cùng device trong khoảng này — giảm số lượng item trong channel.
        private static readonly TimeSpan EnqueueCooldown = TimeSpan.FromSeconds(5);

        private readonly Channel<string> _channel;
        private readonly ConcurrentDictionary<string, long> _lastEnqueuedTicks = new(StringComparer.OrdinalIgnoreCase);

        public DeviceTouchQueue()
        {
            _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(2000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        }

        /// <summary>
        /// Enqueue device ID. Trả về false nếu bị dedup (vừa enqueue trong EnqueueCooldown).
        /// </summary>
        public bool TryEnqueue(string maThietBi)
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            var cooldownTicks = EnqueueCooldown.Ticks;

            // Atomic check-and-set: chỉ một thread "thắng" trong cửa sổ cooldown.
            // Spin retry khi race với thread khác cùng key.
            while (true)
            {
                if (_lastEnqueuedTicks.TryGetValue(maThietBi, out var lastTicks))
                {
                    if (nowTicks - lastTicks < cooldownTicks)
                        return false;

                    if (_lastEnqueuedTicks.TryUpdate(maThietBi, nowTicks, lastTicks))
                        break;
                }
                else if (_lastEnqueuedTicks.TryAdd(maThietBi, nowTicks))
                {
                    break;
                }
            }

            return _channel.Writer.TryWrite(maThietBi);
        }

        internal ChannelReader<string> Reader => _channel.Reader;
    }

    /// <summary>
    /// Background worker đọc Channel, gom batch, flush DB mỗi 5s hoặc khi đủ 50 thiết bị.
    /// </summary>
    public sealed class DeviceTouchWorker : BackgroundService
    {
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);
        private const int MaxBatchSize = 50;

        private readonly DeviceTouchQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DeviceTouchWorker> _logger;

        public DeviceTouchWorker(
            DeviceTouchQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<DeviceTouchWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Gom item cho đến khi hết FlushInterval hoặc đủ MaxBatchSize
                using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                flushCts.CancelAfter(FlushInterval);

                try
                {
                    while (batch.Count < MaxBatchSize)
                    {
                        var id = await _queue.Reader.ReadAsync(flushCts.Token);
                        batch.Add(id);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Timeout flush hoặc app đang shutdown — flush những gì đang có
                }

                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch, stoppingToken);
                    batch.Clear();
                }
            }
        }

        private async Task FlushBatchAsync(HashSet<string> ids, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var deviceService = scope.ServiceProvider.GetRequiredService<DeviceService>();
                var affected = await deviceService.TouchBatchAsync(ids, ct);
                _logger.LogDebug("DeviceTouchWorker: flushed {Count} devices, {Affected} rows updated.", ids.Count, affected);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "DeviceTouchWorker: batch flush failed for {Count} devices.", ids.Count);
            }
        }
    }
}
