using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace Quan4TestScript
{
    class Program
    {
        private static readonly string BaseUrl = Environment.GetEnvironmentVariable("QUAN4_BASE_URL")
            ?? "http://localhost:5114";
        private static readonly string HeardHistoryPath = Path.Combine(AppContext.BaseDirectory, "test2-heard-history.txt");

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== QUAN4 TEST AUTOMATION SYSTEM ===\n");
            Console.WriteLine($"Base URL: {BaseUrl}\n");

            // Menu chọn mode
            Console.WriteLine("Chọn chế độ chạy:");
            Console.WriteLine("  1 - TEST MODE (Queue stress test + Geofence priority)");
            Console.WriteLine("  2 - EMULATION MODE (Interactive GPS simulation)");
            Console.Write("Nhập lựa chọn (1 hoặc 2): ");
            
            string choice = Console.ReadLine()?.Trim() ?? "1";

            if (choice == "2")
            {
                // EMULATION MODE
                await EmulationMode();
            }
            else
            {
                // TEST MODE (original)
                int requestCount = 10;
                if (args.Length > 0 && int.TryParse(args[0], out int count))
                {
                    requestCount = Math.Max(1, count);
                }
                Console.WriteLine($"Số lượng requests test: {requestCount}\n");

                await TestPoiVisitQueue(requestCount);
                Console.WriteLine("\n-------------------------------------------------\n");
                await TestPoiSelectionFromDifferentLocations();
                Console.WriteLine("\n=== HOÀN THÀNH TOÀN BỘ TEST ===");
            }
        }

        // =====================================================================
        // EMULATION MODE: Interactive GPS Simulation
        // =====================================================================
        static async Task EmulationMode()
        {
            Console.Clear();
            Console.WriteLine("=== EMULATION MODE: GPS LOCATION SIMULATOR ===\n");
            
            if (!await IsServiceReachableAsync(BaseUrl))
            {
                Console.WriteLine($"❌ Không kết nối được tới backend tại {BaseUrl}");
                return;
            }

            using var client = new HttpClient();

            // Test Stores
            const int storeAId = 10;
            const string storeAName = "Test Overlap A";
            const double storeALat = 10.7630000;
            const double storeALon = 106.6605000;

            const int storeBId = 11;
            const string storeBName = "Test Overlap B";
            const double storeBLat = 10.7630004;
            const double storeBLon = 106.6605002;

            const int price = 500000;
            const int geofenceRadiusMeters = 5;

            double currentLat = 10.7630002; // Midpoint
            double currentLon = 106.6605001;

            var heardHistory = LoadHeardHistory();

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== EMULATION MODE - INTERACTIVE GPS SIMULATION ===\n");

                // Calculate distances
                var distToA = CalculateDistance(currentLat, currentLon, storeALat, storeALon);
                var distToB = CalculateDistance(currentLat, currentLon, storeBLat, storeBLon);
                bool inGeofenceA = distToA * 1000 <= geofenceRadiusMeters;
                bool inGeofenceB = distToB * 1000 <= geofenceRadiusMeters;

                // Current location
                Console.WriteLine($"📍 Current Location: ({currentLat:F7}, {currentLon:F7})");
                Console.WriteLine($"   → Distance to Store A: {distToA * 1000:F2}m {(inGeofenceA ? "✅ IN GEOFENCE" : "❌")}");
                Console.WriteLine($"   → Distance to Store B: {distToB * 1000:F2}m {(inGeofenceB ? "✅ IN GEOFENCE" : "❌")}");

                // Store Info
                Console.WriteLine($"\n┌─ Store Information:");
                Console.WriteLine($"│  🏪 Store A: \"{storeAName}\" (ID={storeAId})");
                Console.WriteLine($"│     Position: ({storeALat:F7}, {storeALon:F7})");
                Console.WriteLine($"│     Price: {price:N0}đ/month | Heard: {(heardHistory.Contains(storeAId) ? "Đã nghe ✅" : "Chưa ❌")}");
                Console.WriteLine($"│");
                Console.WriteLine($"│  🏪 Store B: \"{storeBName}\" (ID={storeBId})");
                Console.WriteLine($"│     Position: ({storeBLat:F7}, {storeBLon:F7})");
                Console.WriteLine($"│     Price: {price:N0}đ/month | Heard: {(heardHistory.Contains(storeBId) ? "Đã nghe ✅" : "Chưa ❌")}");
                Console.WriteLine($"└─");

                // Priority calculation if in geofence
                if (inGeofenceA || inGeofenceB)
                {
                    var candidates = new List<(int id, string name, double dist)>();
                    if (inGeofenceA) candidates.Add((storeAId, storeAName, distToA));
                    if (inGeofenceB) candidates.Add((storeBId, storeBName, distToB));

                    var prioritized = candidates
                        .OrderBy(x => price)
                        .ThenBy(x => heardHistory.Contains(x.id) ? 1 : 0)
                        .ThenBy(x => x.id)
                        .ToList();

                    if (prioritized.Count > 0)
                    {
                        var winner = prioritized[0];
                        Console.WriteLine($"\n🎯 PRIORITY RESULT:");
                        Console.WriteLine($"   🥇 Will trigger: \"{winner.name}\" (ID={winner.id})");
                        Console.WriteLine($"   ➜ Rule: price → heard history → ID");
                    }
                }
                else
                {
                    Console.WriteLine($"\n⚠️  Not in any geofence. Move to a store location to test priority!");
                }

                // Menu
                Console.WriteLine($"\n╔════════════════════════════════════════╗");
                Console.WriteLine($"║ LOCATION SIMULATOR MENU                ║");
                Console.WriteLine($"╠════════════════════════════════════════╣");
                Console.WriteLine($"║  1 - Move to Store A                   ║");
                Console.WriteLine($"║  2 - Move to Store B                   ║");
                Console.WriteLine($"║  3 - Move to Midpoint                  ║");
                Console.WriteLine($"║  4 - Enter custom coordinates          ║");
                Console.WriteLine($"║  5 - Mark Store A as heard             ║");
                Console.WriteLine($"║  6 - Mark Store B as heard             ║");
                Console.WriteLine($"║  7 - Reset heard history               ║");
                Console.WriteLine($"║  0 - Exit emulation mode               ║");
                Console.WriteLine($"╚════════════════════════════════════════╝");
                Console.Write("Enter choice: ");

                string input = Console.ReadLine()?.Trim() ?? "0";

                switch (input)
                {
                    case "1":
                        currentLat = storeALat;
                        currentLon = storeALon;
                        Console.WriteLine("✓ Moved to Store A");
                        break;

                    case "2":
                        currentLat = storeBLat;
                        currentLon = storeBLon;
                        Console.WriteLine("✓ Moved to Store B");
                        break;

                    case "3":
                        currentLat = 10.7630002;
                        currentLon = 106.6605001;
                        Console.WriteLine("✓ Moved to Midpoint");
                        break;

                    case "4":
                        Console.Write("Enter latitude: ");
                        if (double.TryParse(Console.ReadLine(), out double lat))
                        {
                            Console.Write("Enter longitude: ");
                            if (double.TryParse(Console.ReadLine(), out double lon))
                            {
                                currentLat = lat;
                                currentLon = lon;
                                Console.WriteLine("✓ Position updated");
                            }
                        }
                        break;

                    case "5":
                        heardHistory.Add(storeAId);
                        SaveHeardHistory(heardHistory);
                        Console.WriteLine("✓ Store A marked as heard");
                        break;

                    case "6":
                        heardHistory.Add(storeBId);
                        SaveHeardHistory(heardHistory);
                        Console.WriteLine("✓ Store B marked as heard");
                        break;

                    case "7":
                        heardHistory.Clear();
                        SaveHeardHistory(heardHistory);
                        Console.WriteLine("✓ Heard history reset");
                        break;

                    case "0":
                        Console.WriteLine("Exiting emulation mode...");
                        return;

                    default:
                        Console.WriteLine("❌ Invalid choice");
                        break;
                }

                Console.Write("\nPress Enter to continue...");
                Console.ReadLine();
            }
        }

        // =====================================================================
        // TEST 1: HÀNG ĐỢI POI VISIT + DEDUP
        // =====================================================================
        static async Task TestPoiVisitQueue(int requestCount = 10)
        {
            Console.WriteLine($"[TEST 1] Đang test hàng đợi POI visit (dedup queue) với {requestCount} requests...");

            if (!await IsServiceReachableAsync(BaseUrl))
            {
                Console.WriteLine($"   -> Bỏ qua test 1: không kết nối được tới backend tại {BaseUrl}");
                return;
            }

            using var client = new HttpClient();

            // Bước 1: Lấy danh sách POI
            List<int> poiIds = new();
            try
            {
                var response = await client.GetAsync($"{BaseUrl}/api/poi?lang=vi");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(content);
                    foreach (var poi in jsonDoc.RootElement.EnumerateArray().Take(1))
                    {
                        if (poi.TryGetProperty("id", out var idEl))
                            poiIds.Add(idEl.GetInt32());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   -> Lỗi lấy danh sách POI: {ex.Message}");
                return;
            }

            if (poiIds.Count == 0)
            {
                Console.WriteLine("   -> Bỏ qua test 1: không có POI nào trong hệ thống.");
                return;
            }

            Console.WriteLine($"   -> Tìm được {poiIds.Count} POI, sử dụng POI ID: {poiIds[0]} cho test.");

            var globalStopwatch = Stopwatch.StartNew();
            var firstPoiId = poiIds[0];
            var tasks = new List<Task>();
            var acceptedCount = 0;
            var rejectedCount = 0;
            var errorCount = 0;
            var lock_stats = new object();

            // Bước 2: Gọi N device khác nhau cùng lúc record visit cùng POI
            // Mục đích: kiểm tra queue có hoạt động + response time có bị ảnh hưởng không
            // + tìm giới hạn của queue (khi nào server sẽ reject hoặc trả error)
            for (int i = 1; i <= requestCount; i++)
            {
                int requestId = i;
                var deviceId = $"TEST-DEVICE-{requestId:D4}";
                tasks.Add(Task.Run(async () =>
                {
                    var sw = Stopwatch.StartNew();
                    var visitUrl = $"{BaseUrl}/api/poi/{firstPoiId}/visit";
                    var req = new HttpRequestMessage(HttpMethod.Post, visitUrl);
                    req.Headers.Add("X-Device-Id", deviceId);

                    try
                    {
                        var response = await client.SendAsync(req);
                        sw.Stop();
                        var responseText = await response.Content.ReadAsStringAsync();
                        var queued = responseText.Contains("\"queued\":true");
                        
                        lock (lock_stats)
                        {
                            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                                acceptedCount++;
                            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest || 
                                     response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                                rejectedCount++;
                            else
                                errorCount++;
                        }
                        
                        if (requestId <= 20 || !response.IsSuccessStatusCode)
                            Console.WriteLine($"   -> [Request {requestId:D4}] Status: {(int)response.StatusCode} | Queued: {queued} | Thời gian: {sw.ElapsedMilliseconds} ms");
                    }
                    catch (Exception ex)
                    {
                        lock (lock_stats) { errorCount++; }
                        Console.WriteLine($"   -> [Request {requestId:D4}] Lỗi: {ex.Message}");
                    }
                }));
            }

            await Task.WhenAll(tasks);
            globalStopwatch.Stop();

            Console.WriteLine($"\n[KẾT QUẢ TEST 1]");
            Console.WriteLine($"   Tổng request gửi: {requestCount}");
            Console.WriteLine($"   Accepted (202): {acceptedCount}");
            Console.WriteLine($"   Rejected/Error: {rejectedCount + errorCount}");
            Console.WriteLine($"   Tổng thời gian: {globalStopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"-> 202 Accepted = Server chấp nhận request và xếp vào queue để xử lý async");
            Console.WriteLine($"   Nếu tất cả được 202, queue chưa đạt limit. Nếu có error, bạn đã tìm được giới hạn!");
        }

        // =====================================================================
        // TEST 2: GEOFENCE OVERLAP - PRIORITY TEST
        // =====================================================================
        static async Task TestPoiSelectionFromDifferentLocations()
        {
            Console.WriteLine("[TEST 2] Đang test GEOFENCE OVERLAP - Test Overlap A/B cách nhau 5m...");

            if (!await IsServiceReachableAsync(BaseUrl))
            {
                Console.WriteLine($"   -> Bỏ qua test 2: không kết nối được tới backend.");
                return;
            }

            using var client = new HttpClient();

            const int poi1Id = 10;
            const string poi1Name = "Test Overlap A";
            const double poi1Lat = 10.7630000;
            const double poi1Lon = 106.6605000;

            const int poi2Id = 11;
            const string poi2Name = "Test Overlap B";
            const double poi2Lat = 10.7630004;
            const double poi2Lon = 106.6605002;

            const double testRadiusMeters = 5;

            var overlapDistance = CalculateDistance(poi1Lat, poi1Lon, poi2Lat, poi2Lon);
            var midLat = (poi1Lat + poi2Lat) / 2;
            var midLon = (poi1Lon + poi2Lon) / 2;
            var distToMid1 = CalculateDistance(poi1Lat, poi1Lon, midLat, midLon);
            var distToMid2 = CalculateDistance(poi2Lat, poi2Lon, midLat, midLon);

            Console.WriteLine($"\n   ┌─ [GEOFENCE VISUALIZATION]");
            Console.WriteLine($"   │  GianHang 1: \"{poi1Name}\" (ID={poi1Id})");
            Console.WriteLine($"   │    📍 Tọa độ: ({poi1Lat:F7}, {poi1Lon:F7})");
            Console.WriteLine($"   │    🔵 Radius: {testRadiusMeters}m (vòng kích hoạt audio)");
            Console.WriteLine($"   │");
            Console.WriteLine($"   │  GianHang 2: \"{poi2Name}\" (ID={poi2Id})");
            Console.WriteLine($"   │    📍 Tọa độ: ({poi2Lat:F7}, {poi2Lon:F7})");
            Console.WriteLine($"   │    🔵 Radius: {testRadiusMeters}m (vòng kích hoạt audio)");
            Console.WriteLine($"   │");
            Console.WriteLine($"   │  Khoảng cách: {overlapDistance * 1000:F2}m");
            Console.WriteLine($"   │  → {(overlapDistance * 1000 < testRadiusMeters * 2 ? "✅ GEOFENCE OVERLAP" : "⚠️  Gần overlap")}");
            Console.WriteLine($"   └─ [END VISUALIZATION]");

            Console.WriteLine($"\n   📌 Vị trí giữa: ({midLat:F7}, {midLon:F7})");
            Console.WriteLine($"      → Cách GianHang 1: {distToMid1 * 1000:F2}m");
            Console.WriteLine($"      → Cách GianHang 2: {distToMid2 * 1000:F2}m");

            Console.WriteLine($"\n   🔍 Query backend: GET /api/gianhang?lat={midLat}&lon={midLon}&radiusMeters={testRadiusMeters}");

            List<(int id, string name, double lat, double lon, decimal price, double distance)> nearbyGianHang = new();
            try
            {
                var nearbyUrl = $"{BaseUrl}/api/gianhang?lat={midLat}&lon={midLon}&radiusMeters={testRadiusMeters}&lang=vi";
                var response = await client.GetAsync(nearbyUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(content);

                    var rootElement = jsonDoc.RootElement;
                    if (rootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var gh in rootElement.EnumerateArray())
                        {
                            if (gh.TryGetProperty("idGianHang", out var idEl) &&
                                gh.TryGetProperty("ten", out var tenEl) &&
                                gh.TryGetProperty("lat", out var latEl) &&
                                gh.TryGetProperty("lon", out var lonEl))
                            {
                                var ghId = idEl.GetInt32();
                                var ghName = tenEl.GetString() ?? "N/A";
                                var ghLat = latEl.GetDouble();
                                var ghLon = lonEl.GetDouble();
                                var ghPrice = gh.TryGetProperty("phiHangThang", out var priceEl) && priceEl.ValueKind == JsonValueKind.Number
                                    ? priceEl.GetDecimal()
                                    : 0m;
                                var dist = CalculateDistance(ghLat, ghLon, midLat, midLon);
                                nearbyGianHang.Add((ghId, ghName, ghLat, ghLon, ghPrice, dist));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   -> Lỗi query nearby: {ex.Message}");
            }

            var comparisonCandidates = nearbyGianHang
                .Where(x => x.id == poi1Id || x.id == poi2Id)
                .ToList();

            var heardHistory = LoadHeardHistory();
            var prioritizedStores = comparisonCandidates
                .OrderBy(x => x.price)
                .ThenBy(x => heardHistory.Contains(x.id) ? 1 : 0)
                .ThenBy(x => x.id)
                .ToList();

            Console.WriteLine($"\n   ⚡ [PRIORITY RESULT - HỆ THỐNG ƯU TIÊN CÁI NÀO]:");
            if (comparisonCandidates.Count >= 2)
            {
                var nearest = prioritizedStores[0];
                var second = prioritizedStores[1];

                Console.WriteLine($"      ✅ Cả 2 GianHang đều trong vòng {testRadiusMeters}m!");
                Console.WriteLine($"      \n      🥇 Priority 1 (ƯU TIÊN): \"{nearest.name}\"");
                Console.WriteLine($"         📍 ({nearest.lat:F7}, {nearest.lon:F7})");
                Console.WriteLine($"         💰 Giá tiền: {nearest.price:N0}đ/tháng");
                Console.WriteLine($"         💬 Đã nghe trước: {(heardHistory.Contains(nearest.id) ? "Có" : "Chưa")}");
                Console.WriteLine($"         📏 Distance: {nearest.distance * 1000:F2}m");

                Console.WriteLine($"      \n      🥈 Priority 2 (thứ hai): \"{second.name}\"");
                Console.WriteLine($"         📍 ({second.lat:F7}, {second.lon:F7})");
                Console.WriteLine($"         💰 Giá tiền: {second.price:N0}đ/tháng");
                Console.WriteLine($"         💬 Đã nghe trước: {(heardHistory.Contains(second.id) ? "Có" : "Chưa")}");
                Console.WriteLine($"         📏 Distance: {second.distance * 1000:F2}m");

                Console.WriteLine($"      \n      ➜ Luật áp dụng: giá tiền -> đã nghe trước -> ID nhỏ hơn");
                Console.WriteLine($"      ➜ Khi người dùng đứng giữa, hệ thống ưu tiên: '{nearest.name}'");
            }
            else if (comparisonCandidates.Count == 1)
            {
                Console.WriteLine($"      ⚠️  Chỉ 1 GianHang trong 2 store test: \"{comparisonCandidates[0].name}\" | Giá: {comparisonCandidates[0].price:N0}đ/tháng");
            }
            else
            {
                Console.WriteLine($"      ❌ Không có đủ 2 GianHang test nào trong kết quả!");
            }

            if (prioritizedStores.Count > 0)
            {
                var priorityGianHang = prioritizedStores.First();
                heardHistory.Add(priorityGianHang.id);
                SaveHeardHistory(heardHistory);
                Console.WriteLine($"\n   🧑 Người dùng đứng tại tọa độ giữa, gọi /api/poi/{priorityGianHang.id}/visit...");
                Console.WriteLine($"   ➜ Ưu tiên: GianHang '{priorityGianHang.name}'");
                await RecordVisitAtPoi(client, priorityGianHang.id, "TEST-GEOFENCE-MID");
            }
            else
            {
                Console.WriteLine($"\n   ❌ Không thể test visit vì không có GianHang trong vòng!");
            }

            Console.WriteLine($"\n[KẾT QUẢ TEST 2] GEOFENCE OVERLAP TEST HOÀN TẤT");
            Console.WriteLine($"-> Kì vọng: Khi người dùng đứng giữa 2 POI Test Overlap A/B cách nhau 5m,");
            Console.WriteLine($"   hệ thống ưu tiên POI GẦN NHẤT trước.");
        }

        // =====================================================================
        // HELPER FUNCTIONS
        // =====================================================================

        private static async Task RecordVisitAtPoi(HttpClient client, int poiId, string deviceId)
        {
            try
            {
                var visitUrl = $"{BaseUrl}/api/poi/{poiId}/visit";
                var req = new HttpRequestMessage(HttpMethod.Post, visitUrl);
                req.Headers.Add("X-Device-Id", deviceId);
                var response = await client.SendAsync(req);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"       Status: {(int)response.StatusCode}");
                if (content.Length < 150)
                    Console.WriteLine($"       Response: {content}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"       Lỗi: {ex.Message}");
            }
        }

        private static async Task<bool> IsServiceReachableAsync(string url)
        {
            try
            {
                var uri = new Uri(url);
                var port = uri.Port > 0 ? uri.Port : (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80);

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(uri.Host, port);
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(1500));

                return completedTask == connectTask && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        // Haversine formula để tính khoảng cách giữa 2 điểm GPS
        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Bán kính Trái Đất (km)
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static HashSet<int> LoadHeardHistory()
        {
            try
            {
                if (!File.Exists(HeardHistoryPath))
                    return new HashSet<int>();

                return File.ReadAllLines(HeardHistoryPath)
                    .Select(line => int.TryParse(line.Trim(), out var id) ? id : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToHashSet();
            }
            catch
            {
                return new HashSet<int>();
            }
        }

        private static void SaveHeardHistory(HashSet<int> heardHistory)
        {
            try
            {
                File.WriteAllLines(HeardHistoryPath, heardHistory.OrderBy(id => id).Select(id => id.ToString()));
            }
            catch
            {
                // If persistence fails, the test still runs with in-memory state.
            }
        }
    }
}
