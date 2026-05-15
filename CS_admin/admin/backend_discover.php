<?php
/**
 * Hiển thị các URL backend đang khả dụng (LAN IP + ngrok), kèm QR.
 * Admin mở trang này → user dùng phone scan QR / hoặc copy URL paste
 * vào Settings của app để app luôn dùng URL đúng kể cả khi LAN đổi.
 *
 * Mở: /CS_admin/admin/backend_discover.php
 */
require_once dirname(__DIR__) . '/connect.php';

header('Content-Type: text/html; charset=UTF-8');

function detect_lan_ips()
{
    $ips = array();

    // Windows: ipconfig
    if (stripos(PHP_OS, 'WIN') === 0) {
        $output = @shell_exec('ipconfig');
        if (is_string($output)) {
            if (preg_match_all('/IPv4[^:]*:\s*([0-9.]+)/i', $output, $matches)) {
                foreach ($matches[1] as $ip) {
                    if (preg_match('/^192\.168\.|^10\.|^172\.(1[6-9]|2\d|3[01])\./', $ip)) {
                        $ips[] = $ip;
                    }
                }
            }
        }
    } else {
        // Linux/Mac
        $output = @shell_exec("ip -4 addr 2>/dev/null || ifconfig 2>/dev/null");
        if (is_string($output)) {
            if (preg_match_all('/inet\s+(?:addr:)?([0-9.]+)/i', $output, $matches)) {
                foreach ($matches[1] as $ip) {
                    if (preg_match('/^192\.168\.|^10\.|^172\.(1[6-9]|2\d|3[01])\./', $ip)) {
                        $ips[] = $ip;
                    }
                }
            }
        }
    }

    return array_values(array_unique($ips));
}

function detect_ngrok_url()
{
    $appSettingsPath = dirname(dirname(__DIR__)) . '/VinhKhanh/VinhKhanh/appsettings.json';
    if (is_file($appSettingsPath)) {
        $raw = file_get_contents($appSettingsPath);
        $decoded = is_string($raw) ? json_decode($raw, true) : null;
        if (is_array($decoded)) {
            // Lấy từ CASSO_WEBHOOK_URL (đang dùng cùng host ngrok)
            if (!empty($decoded['CASSO_WEBHOOK_URL'])) {
                $u = parse_url((string) $decoded['CASSO_WEBHOOK_URL']);
                if ($u && !empty($u['host'])) {
                    return $u['scheme'] . '://' . $u['host'] . '/';
                }
            }
        }
    }
    return '';
}

function probe_url($url, $timeoutMs = 1500)
{
    $ch = curl_init(rtrim($url, '/') . '/api/Admin/summary?idTaiKhoan=0');
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_NOBODY, true);
    curl_setopt($ch, CURLOPT_CONNECTTIMEOUT_MS, $timeoutMs);
    curl_setopt($ch, CURLOPT_TIMEOUT_MS, $timeoutMs);
    curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
    curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, false);
    curl_setopt($ch, CURLOPT_HTTPHEADER, array('ngrok-skip-browser-warning: true'));
    @curl_exec($ch);
    $code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
    $err = curl_error($ch);
    curl_close($ch);
    return array('http' => $code, 'error' => $err);
}

$lanIps = detect_lan_ips();
$ngrokUrl = detect_ngrok_url();

$candidates = array();
foreach ($lanIps as $ip) {
    $candidates[] = array(
        'name' => 'LAN HTTP ' . $ip,
        'url' => "http://$ip:5114/",
        'note' => 'Chỉ work khi phone & máy này cùng Wi-Fi',
    );
}
$candidates[] = array(
    'name' => 'Localhost HTTP',
    'url' => 'http://localhost:5114/',
    'note' => 'Chỉ work với adb reverse hoặc emulator',
);
if ($ngrokUrl !== '') {
    $candidates[] = array(
        'name' => 'Ngrok (KHUYẾN NGHỊ)',
        'url' => $ngrokUrl,
        'note' => 'Hoạt động qua internet, ổn định khi LAN đổi',
    );
}

$probedAt = date('H:i:s');
foreach ($candidates as &$c) {
    $c['probe'] = probe_url($c['url']);
}
unset($c);

?>
<!doctype html>
<html lang="vi"><head><meta charset="UTF-8" /><title>Backend URL discovery</title>
<style>
body { font-family: system-ui, sans-serif; margin: 24px; background: #f9fafb; }
h1 { margin: 0 0 6px; }
.muted { color: #6b7280; font-size: 13px; margin-bottom: 16px; }
table { background: white; border-collapse: collapse; box-shadow: 0 1px 3px rgba(0,0,0,.06); border-radius: 8px; overflow: hidden; width: 100%; max-width: 980px; }
th, td { padding: 10px 14px; border-bottom: 1px solid #e5e7eb; font-size: 13px; vertical-align: top; }
th { background: #f3f4f6; font-weight: 600; text-align: left; }
.url-cell { font-family: 'Cascadia Code', Consolas, monospace; word-break: break-all; }
.copy-btn { padding: 4px 8px; border: 1px solid #d1d5db; border-radius: 4px; background: white; cursor: pointer; font-size: 11px; margin-left: 6px; }
.badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
.badge.ok { background: #d1fae5; color: #065f46; }
.badge.warn { background: #fee2e2; color: #991b1b; }
.qr { margin-top: 16px; padding: 16px; background: white; border: 1px solid #e5e7eb; border-radius: 8px; max-width: 360px; text-align: center; }
.qr img { max-width: 280px; }
.qr p { margin: 8px 0; font-size: 12px; color: #4b5563; }
</style></head><body>
<h1>🌐 Backend URL discovery</h1>
<p class="muted">Probe các URL backend khả dụng (probe lúc <?php echo $probedAt; ?>).
Chọn URL có HTTP 200 / HTTP 401 (≠ 0) → copy & paste vào setting của app, hoặc scan QR.</p>

<table>
<tr><th>Loại</th><th>URL</th><th>HTTP</th><th>Ghi chú</th></tr>
<?php foreach ($candidates as $c) {
    $reachable = $c['probe']['http'] > 0 && $c['probe']['http'] < 600;
?>
<tr>
  <td><strong><?php echo htmlspecialchars($c['name']); ?></strong></td>
  <td class="url-cell">
    <span><?php echo htmlspecialchars($c['url']); ?></span>
    <button class="copy-btn" onclick="navigator.clipboard.writeText('<?php echo addslashes($c['url']); ?>')">Copy</button>
  </td>
  <td>
    <?php if ($reachable) { ?>
      <span class="badge ok"><?php echo (int) $c['probe']['http']; ?></span>
    <?php } else { ?>
      <span class="badge warn">offline / timeout</span>
    <?php } ?>
  </td>
  <td><?php echo htmlspecialchars($c['note']); ?>
    <?php if (!empty($c['probe']['error']) && !$reachable) { ?>
      <br><small style="color:#9ca3af"><?php echo htmlspecialchars($c['probe']['error']); ?></small>
    <?php } ?>
  </td>
</tr>
<?php } ?>
</table>

<?php
$best = null;
foreach ($candidates as $c) {
    $http = (int) $c['probe']['http'];
    if ($http > 0 && $http < 600) { $best = $c; break; }
}
if ($best) {
    $qrSrc = 'https://api.qrserver.com/v1/create-qr-code/?size=280x280&data=' . urlencode($best['url']);
?>
<div class="qr">
  <strong>QR cho URL khuyến nghị</strong>
  <p><?php echo htmlspecialchars($best['name']); ?></p>
  <img src="<?php echo htmlspecialchars($qrSrc); ?>" alt="QR" />
  <p>Phone scan QR (cần app có support nhập URL trong Settings) hoặc copy URL paste vào setting app.</p>
</div>
<?php } ?>

<p class="muted" style="margin-top:24px;">
Reload trang để probe lại. Hỗ trợ đặt env <code>MAUI_BACKEND_URL=&lt;url&gt;</code> trong app
(hoặc lưu vào Preferences key <code>backend_base_url</code>) để override toàn bộ.
</p>
</body></html>
