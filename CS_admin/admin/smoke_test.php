<?php
/**
 * Sandbox smoke test cho admin web ↔ backend API.
 *
 * Mở trên trình duyệt: /CS_admin/admin/smoke_test.php?idTaiKhoan=1
 * Hoặc CLI: php smoke_test.php --idTaiKhoan=1
 */

require_once dirname(__DIR__) . '/connect.php';

$isCli = php_sapi_name() === 'cli';

if ($isCli) {
    $idTaiKhoan = 1;
    foreach ($argv as $arg) {
        if (preg_match('/^--idTaiKhoan=(\d+)/', $arg, $m)) {
            $idTaiKhoan = (int) $m[1];
        }
    }
} else {
    $idTaiKhoan = isset($_GET['idTaiKhoan']) ? (int) $_GET['idTaiKhoan'] : 1;
    header('Content-Type: text/html; charset=UTF-8');
}

$results = array();

function smoke_test_run($name, $method, $path, $payload = null, $query = array(), $expectShape = null)
{
    global $results;

    $error = '';
    $httpCode = 0;
    $started = microtime(true);
    $response = admin_api_call($method, $path, $payload, $error, $httpCode, $query);
    $elapsedMs = (int) round((microtime(true) - $started) * 1000);

    $passed = false;
    $detail = '';

    if ($error !== '') {
        $detail = 'API error: ' . $error;
    } elseif ($expectShape === 'array' && is_array($response) && (count($response) === 0 || isset($response[0]) || array_keys($response) === range(0, count($response) - 1))) {
        $passed = true;
        $detail = 'Got array, count=' . count($response);
    } elseif ($expectShape === 'object' && is_array($response)) {
        $passed = true;
        $detail = 'Got object with keys: ' . implode(',', array_slice(array_keys($response), 0, 8)) . (count($response) > 8 ? '...' : '');
    } elseif ($expectShape === null && $response !== null) {
        $passed = true;
        $detail = 'Got response (' . gettype($response) . ')';
    } else {
        $detail = 'Unexpected response type: ' . gettype($response);
    }

    $results[] = array(
        'name' => $name,
        'method' => $method,
        'path' => $path,
        'http' => $httpCode,
        'ms' => $elapsedMs,
        'passed' => $passed,
        'detail' => $detail,
    );

    return $response;
}

// 1. Backend health: GET /Admin/summary
smoke_test_run('Admin summary (basic + dashboard fields)', 'GET', 'Admin/summary', null,
    array('idTaiKhoan' => $idTaiKhoan), 'object');

// 2. Stores list: GET /Admin/stores
$stores = smoke_test_run('Admin stores list', 'GET', 'Admin/stores', null,
    array('idTaiKhoan' => $idTaiKhoan), 'array');
$firstStoreId = (is_array($stores) && count($stores) > 0) ? (int) ($stores[0]['idGianHang'] ?? 0) : 0;

// 3. Owners: GET /Admin/owners
smoke_test_run('Admin owners list', 'GET', 'Admin/owners', null,
    array('idTaiKhoan' => $idTaiKhoan), 'array');

// 4. Accounts: GET /Admin/accounts
smoke_test_run('Admin accounts list', 'GET', 'Admin/accounts', null,
    array('idTaiKhoan' => $idTaiKhoan), 'array');

// 5. Service packages
smoke_test_run('Service packages', 'GET', 'Admin/service-packages', null,
    array('idTaiKhoan' => $idTaiKhoan), 'array');

// 6. Devices
smoke_test_run('Devices', 'GET', 'Admin/devices', null,
    array('idTaiKhoan' => $idTaiKhoan), 'array');

// 7. Store requests
smoke_test_run('Store requests (admin)', 'GET', 'Admin/store-requests', null,
    array('idTaiKhoan' => $idTaiKhoan), 'array');

// 8. NEW: POI Map
smoke_test_run('POI Map (NEW)', 'GET', 'Admin/poi-map', null,
    array('idTaiKhoan' => $idTaiKhoan, 'ownerOnly' => 'false'), 'array');

// 9. NEW: Daily visits cho store đầu tiên (nếu có)
if ($firstStoreId > 0) {
    smoke_test_run('Store daily visits (NEW)', 'GET',
        'Admin/stores/' . rawurlencode((string) $firstStoreId) . '/daily-visits',
        null, array('idTaiKhoan' => $idTaiKhoan), 'object');
}

// 10. NEW: Invoices list
$invoices = smoke_test_run('Invoices list (NEW)', 'GET', 'Admin/invoices', null,
    array('idTaiKhoan' => $idTaiKhoan, 'ownerOnly' => 'false'), 'array');
$firstInvoiceId = (is_array($invoices) && count($invoices) > 0) ? (int) ($invoices[0]['idHoaDonGianHang'] ?? 0) : 0;

// 11. NEW: Invoice detail
if ($firstInvoiceId > 0) {
    smoke_test_run('Invoice detail (NEW)', 'GET',
        'Admin/invoices/' . rawurlencode((string) $firstInvoiceId),
        null, array('idTaiKhoan' => $idTaiKhoan, 'ownerOnly' => 'false'), 'object');

    smoke_test_run('Invoice status (NEW)', 'GET',
        'Admin/invoices/' . rawurlencode((string) $firstInvoiceId) . '/status',
        null, array(), 'object');
} else {
    $results[] = array(
        'name' => 'Invoice detail (NEW)',
        'method' => 'SKIP',
        'path' => 'Admin/invoices/{id}',
        'http' => 0,
        'ms' => 0,
        'passed' => null,
        'detail' => 'Skip - không có hóa đơn nào trong DB.',
    );
}

// Render result
$total = count($results);
$pass = count(array_filter($results, function ($r) { return $r['passed'] === true; }));
$fail = count(array_filter($results, function ($r) { return $r['passed'] === false; }));
$skip = count(array_filter($results, function ($r) { return $r['passed'] === null; }));

if ($isCli) {
    echo "\n=== Smoke test (idTaiKhoan=$idTaiKhoan) ===\n";
    foreach ($results as $r) {
        $mark = $r['passed'] === true ? '✓' : ($r['passed'] === false ? '✗' : '~');
        printf("%s [%-6s %s] HTTP %d %dms — %s — %s\n",
            $mark, $r['method'], $r['path'], $r['http'], $r['ms'], $r['name'], $r['detail']);
    }
    echo "\nTotal: $total — Pass: $pass — Fail: $fail — Skip: $skip\n";
    exit($fail === 0 ? 0 : 1);
}

?>
<!doctype html>
<html lang="vi">
<head>
<meta charset="UTF-8" />
<title>Smoke Test — Admin ↔ Backend</title>
<style>
body { font-family: system-ui, sans-serif; margin: 32px; color: #1f2937; background: #f9fafb; }
h1 { margin: 0 0 6px; font-size: 22px; }
.muted { color: #6b7280; margin: 0 0 20px; font-size: 14px; }
.summary { padding: 12px 18px; border-radius: 8px; margin-bottom: 20px; display: flex; gap: 24px; font-weight: 600; }
.summary .pass { color: #059669; }
.summary .fail { color: #dc2626; }
.summary .skip { color: #6b7280; }
table { border-collapse: collapse; width: 100%; background: white; box-shadow: 0 1px 3px rgba(0,0,0,.06); border-radius: 8px; overflow: hidden; }
th, td { padding: 10px 14px; text-align: left; font-size: 13px; border-bottom: 1px solid #e5e7eb; vertical-align: top; }
th { background: #f3f4f6; font-weight: 600; color: #374151; }
tr:last-child td { border-bottom: none; }
.badge { display: inline-block; padding: 2px 8px; border-radius: 4px; font-weight: 600; font-size: 12px; }
.badge.pass { background: #d1fae5; color: #065f46; }
.badge.fail { background: #fee2e2; color: #991b1b; }
.badge.skip { background: #e5e7eb; color: #4b5563; }
code { font-family: 'Cascadia Code', Consolas, monospace; background: #f3f4f6; padding: 2px 6px; border-radius: 3px; font-size: 12px; }
.detail { color: #6b7280; font-size: 12px; }
</style>
</head>
<body>
<h1>Smoke Test — Admin ↔ Backend (idTaiKhoan=<?php echo (int) $idTaiKhoan; ?>)</h1>
<p class="muted">Gọi tất cả endpoint .NET backend mà các trang admin đã port. Mở Swagger: <a href="http://localhost:5114/swagger" target="_blank">http://localhost:5114/swagger</a></p>

<div class="summary">
  <span>Total: <?php echo $total; ?></span>
  <span class="pass">Pass: <?php echo $pass; ?></span>
  <span class="fail">Fail: <?php echo $fail; ?></span>
  <span class="skip">Skip: <?php echo $skip; ?></span>
</div>

<table>
<thead>
<tr><th></th><th>Endpoint</th><th>HTTP</th><th>ms</th><th>Mô tả</th><th>Chi tiết</th></tr>
</thead>
<tbody>
<?php foreach ($results as $r) { ?>
<?php
$badgeClass = $r['passed'] === true ? 'pass' : ($r['passed'] === false ? 'fail' : 'skip');
$badgeText = $r['passed'] === true ? '✓ PASS' : ($r['passed'] === false ? '✗ FAIL' : '~ SKIP');
?>
<tr>
  <td><span class="badge <?php echo $badgeClass; ?>"><?php echo $badgeText; ?></span></td>
  <td><code><?php echo htmlspecialchars($r['method'] . ' ' . $r['path']); ?></code></td>
  <td><?php echo (int) $r['http']; ?></td>
  <td><?php echo (int) $r['ms']; ?></td>
  <td><?php echo htmlspecialchars($r['name']); ?></td>
  <td class="detail"><?php echo htmlspecialchars($r['detail']); ?></td>
</tr>
<?php } ?>
</tbody>
</table>

<p class="muted" style="margin-top:24px;">
  Refresh trang để chạy lại. Đổi tài khoản: <code>?idTaiKhoan=&lt;id&gt;</code>.
  Chạy CLI: <code>php smoke_test.php --idTaiKhoan=1</code>
</p>
</body>
</html>
