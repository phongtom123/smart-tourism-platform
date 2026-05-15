<?php
/**
 * Debug helper — xem real-time các visit đã được ghi gần đây.
 * Mở: /CS_admin/admin/debug_visits.php
 */
require_once dirname(__DIR__) . '/connect.php';

header('Content-Type: text/html; charset=UTF-8');

$conn = admin_db_connection();
if (!$conn instanceof mysqli) {
    echo 'DB không kết nối';
    exit;
}

$action = isset($_GET['action']) ? (string) $_GET['action'] : '';
$idStore = isset($_GET['store']) ? (int) $_GET['store'] : 0;
$device = isset($_GET['device']) ? trim((string) $_GET['device']) : '';
$message = '';

if ($action === 'reset_today') {
    $conn->query("DELETE FROM luot_truy_cap_thiet_bi_ngay WHERE ngay = CURDATE()");
    $message = 'Đã reset dedupe hôm nay (' . $conn->affected_rows . ' rows). Giờ mọi thiết bị có thể count lại.';
}

if ($action === 'manual_visit' && $idStore > 0 && $device !== '') {
    // Gọi API thật
    $ch = curl_init('http://localhost:5114/api/Poi/' . $idStore . '/visit');
    curl_setopt($ch, CURLOPT_POST, true);
    curl_setopt($ch, CURLOPT_POSTFIELDS, '');
    curl_setopt($ch, CURLOPT_HTTPHEADER, ['X-Device-Id: ' . $device, 'Content-Length: 0']);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    $body = curl_exec($ch);
    $code = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);
    $message = "Manual visit POI $idStore (device: $device) → HTTP $code → $body";
}

$today = $conn->query("SELECT idGianHang, maThietBi, ngayTao FROM luot_truy_cap_thiet_bi_ngay WHERE ngay = CURDATE() ORDER BY ngayTao DESC LIMIT 50")->fetch_all(MYSQLI_ASSOC);
$counters = $conn->query("SELECT idGianHang, ten, luotTruyCap FROM gianhang ORDER BY idGianHang")->fetch_all(MYSQLI_ASSOC);
$todayPerStore = $conn->query("SELECT idGianHang, soLuot FROM luot_truy_cap_ngay WHERE ngay = CURDATE()")->fetch_all(MYSQLI_ASSOC);
$conn->close();

$todayMap = array();
foreach ($todayPerStore as $row) $todayMap[(int) $row['idGianHang']] = (int) $row['soLuot'];
?>
<!doctype html>
<html lang="vi"><head><meta charset="UTF-8" /><title>Debug visits</title>
<style>
body { font-family: system-ui, sans-serif; margin: 24px; background: #f9fafb; }
h1 { margin: 0 0 12px; }
.tools { display: flex; gap: 8px; margin-bottom: 16px; flex-wrap: wrap; }
.tools a, .tools button { padding: 8px 14px; border-radius: 6px; border: 1px solid #d1d5db; background: white; text-decoration: none; color: #1f2937; font-size: 13px; cursor: pointer; }
.tools a.danger { background: #fee2e2; color: #991b1b; border-color: #fca5a5; }
.tools form { display: inline-flex; gap: 4px; }
.tools input { padding: 6px 10px; border: 1px solid #d1d5db; border-radius: 6px; font-size: 13px; }
.notice { padding: 10px 14px; background: #fef3c7; border: 1px solid #fcd34d; border-radius: 6px; margin-bottom: 16px; }
table { background: white; border-collapse: collapse; box-shadow: 0 1px 3px rgba(0,0,0,.06); border-radius: 8px; overflow: hidden; width: 100%; max-width: 900px; }
th, td { padding: 8px 12px; border-bottom: 1px solid #e5e7eb; font-size: 13px; text-align: left; }
th { background: #f3f4f6; font-weight: 600; }
code { background: #f3f4f6; padding: 1px 5px; border-radius: 3px; font-size: 12px; }
.section { margin-top: 20px; }
.section h2 { font-size: 16px; margin: 0 0 8px; color: #374151; }
.muted { color: #6b7280; font-size: 12px; }
</style></head><body>
<h1>🔍 Debug visits (hôm nay: <?php echo date('Y-m-d'); ?>)</h1>
<p class="muted">Reload trang sau khi nghe audio trên phone để xem visit có được ghi nhận không.</p>

<?php if ($message !== '') { ?>
<div class="notice"><?php echo htmlspecialchars($message); ?></div>
<?php } ?>

<div class="tools">
  <a href="?">🔄 Reload</a>
  <a class="danger" href="?action=reset_today" onclick="return confirm('Reset dedupe hôm nay? Mọi device sẽ count lại được.')">♻️ Reset dedupe hôm nay</a>
  <form method="get">
    <input type="hidden" name="action" value="manual_visit" />
    <input type="number" name="store" placeholder="POI id" min="1" required style="width:80px" />
    <input type="text" name="device" placeholder="X-Device-Id" required style="width:220px" />
    <button>📡 Test gọi API thủ công</button>
  </form>
</div>

<div class="section">
<h2>Counter gian hàng (gianhang.luotTruyCap + luot_truy_cap_ngay hôm nay)</h2>
<table>
<tr><th>POI</th><th>Tên</th><th>Tổng tích lũy</th><th>Hôm nay</th></tr>
<?php foreach ($counters as $row) { ?>
<tr>
<td>#<?php echo (int) $row['idGianHang']; ?></td>
<td><?php echo htmlspecialchars($row['ten']); ?></td>
<td><strong><?php echo (int) $row['luotTruyCap']; ?></strong></td>
<td><?php echo isset($todayMap[(int) $row['idGianHang']]) ? $todayMap[(int) $row['idGianHang']] : 0; ?></td>
</tr>
<?php } ?>
</table>
</div>

<div class="section">
<h2>Dedupe hôm nay (<?php echo count($today); ?> entries) — thiết bị đã ghé</h2>
<?php if (count($today) === 0) { ?>
<p class="muted">Chưa có thiết bị nào ghé hôm nay. Nếu phone đã nghe audio mà không thấy ở đây → API chưa gọi được.</p>
<?php } else { ?>
<table>
<tr><th>Thời gian</th><th>POI</th><th>Device ID</th></tr>
<?php foreach ($today as $row) { ?>
<tr>
<td><?php echo htmlspecialchars($row['ngayTao']); ?></td>
<td>#<?php echo (int) $row['idGianHang']; ?></td>
<td><code><?php echo htmlspecialchars($row['maThietBi']); ?></code></td>
</tr>
<?php } ?>
</table>
<?php } ?>
</div>

</body></html>
