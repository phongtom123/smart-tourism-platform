<?php
$auth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($auth['idTaiKhoan']) ? (int) $auth['idTaiKhoan'] : 0;
$selectedDeviceId = isset($_GET['selected']) ? (int) $_GET['selected'] : 0;
$statusFilter = isset($_GET['status']) ? strtolower(trim((string) $_GET['status'])) : 'all';
$loaiFilter = isset($_GET['loai']) ? strtolower(trim((string) $_GET['loai'])) : 'all';
$flashMessage = isset($_GET['message']) ? (string) $_GET['message'] : '';
$flashNotice = isset($_GET['notice']) ? (string) $_GET['notice'] : '';

if (!in_array($statusFilter, array('all', 'hoat_dong', 'khoa', 'cho_kich_hoat'), true)) {
    $statusFilter = 'all';
}
if (!in_array($loaiFilter, array('all', 'app_client', 'portal_web', 'hardware'), true)) {
    $loaiFilter = 'all';
}

function device_list_query($idTaiKhoan, $loaiFilter = 'all')
{
    $query = array('idTaiKhoan' => $idTaiKhoan);
    if ($loaiFilter !== 'all' && in_array($loaiFilter, array('app_client', 'portal_web', 'hardware'), true)) {
        $query['loai'] = $loaiFilter;
    }
    return $query;
}

function device_list_url($statusFilter, $selectedDeviceId = 0, $message = '', $notice = '', $loaiFilter = 'all')
{
    $params = array('usecase' => 'device');
    if ($statusFilter !== 'all') $params['status'] = $statusFilter;
    if ($loaiFilter !== 'all') $params['loai'] = $loaiFilter;
    if ($selectedDeviceId > 0) $params['selected'] = $selectedDeviceId;
    if ($message !== '') $params['message'] = $message;
    if ($notice !== '') $params['notice'] = $notice;
    return admin_url('index1st.php?' . http_build_query($params));
}

function device_status_meta($trangThai)
{
    switch (strtolower(trim((string) $trangThai))) {
        case 'hoat_dong':
            return array('label' => 'Hoạt động', 'class' => 'active');
        case 'khoa':
            return array('label' => 'Đã khóa', 'class' => 'locked');
        default:
            return array('label' => 'Chờ kích hoạt', 'class' => 'pending');
    }
}

function device_format_datetime($value)
{
    if (empty($value)) return 'Chưa có';
    $timestamp = strtotime((string) $value);
    return $timestamp === false ? 'Chưa có' : date('d/m/Y H:i', $timestamp);
}

function device_status_options()
{
    return array(
        'hoat_dong' => 'Hoạt động',
        'khoa' => 'Khóa',
        'cho_kich_hoat' => 'Chờ kích hoạt',
    );
}

function device_loai_options()
{
    return array(
        'all' => 'Tất cả loại',
        'app_client' => 'App mobile',
        'portal_web' => 'Portal web',
        'hardware' => 'Thiết bị cứng',
    );
}

function device_loai_label($loai)
{
    $opts = device_loai_options();
    $key = strtolower(trim((string) $loai));
    return isset($opts[$key]) ? $opts[$key] : ($loai !== '' ? $loai : '—');
}

$deviceError = '';
$deviceNotice = $flashNotice;
$deviceMessage = $flashMessage;

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['device_action']) && $_POST['device_action'] === 'update_status') {
    $targetMaThietBi = trim((string) ($_POST['maThietBi'] ?? ''));
    $targetTrangThai = strtolower(trim((string) ($_POST['trangThai'] ?? '')));
    $targetIdThietBi = isset($_POST['idThietBi']) ? (int) $_POST['idThietBi'] : 0;

    if ($targetMaThietBi === '') {
        $deviceError = 'Không xác định được thiết bị cần cập nhật.';
    } elseif (!array_key_exists($targetTrangThai, device_status_options())) {
        $deviceError = 'Trạng thái không hợp lệ.';
    } else {
        $apiHttpCode = 0;
        $apiError = '';
        $statusPath = 'Admin/devices/' . rawurlencode($targetMaThietBi) . '/status';
        $apiResult = $idTaiKhoan > 0
            ? admin_api_call('PATCH', $statusPath, array('trangThai' => $targetTrangThai), $apiError, $apiHttpCode, array('idTaiKhoan' => $idTaiKhoan))
            : null;

        if (!is_array($apiResult)) {
            $deviceError = $apiError !== '' ? $apiError : 'Không thể cập nhật trạng thái: backend API chưa sẵn sàng.';
        } else {
            $successMsg = isset($apiResult['message']) && $apiResult['message'] !== '' ? (string) $apiResult['message'] : 'Cập nhật trạng thái thiết bị thành công.';
            header('Location: ' . device_list_url($statusFilter, $targetIdThietBi, $successMsg, '', $loaiFilter));
            exit;
        }
    }
}

$deviceHttpCode = 0;
$devices = $idTaiKhoan > 0
    ? admin_api_call('GET', 'Admin/devices', null, $deviceError, $deviceHttpCode, device_list_query($idTaiKhoan, $loaiFilter))
    : array();

if (!is_array($devices)) {
    $devices = array();
    if ($deviceError === '') {
        $deviceError = 'Không thể tải danh sách thiết bị: backend API chưa sẵn sàng.';
    }
}

$totalDevices = count($devices);
$activeCount = 0;
$lockedCount = 0;
$pendingCount = 0;

foreach ($devices as $device) {
    if (!is_array($device)) continue;
    $meta = device_status_meta($device['trangThai'] ?? '');
    if ($meta['class'] === 'active') $activeCount++;
    elseif ($meta['class'] === 'locked') $lockedCount++;
    else $pendingCount++;
}

$filteredDevices = array();
foreach ($devices as $device) {
    if (!is_array($device)) continue;
    $trangThai = strtolower(trim((string) ($device['trangThai'] ?? '')));
    if ($statusFilter === 'all' || $trangThai === $statusFilter) {
        $filteredDevices[] = $device;
    }
}

$filteredCount = count($filteredDevices);
$selectedDevice = null;
foreach ($filteredDevices as $device) {
    if (isset($device['idThietBi']) && (int) $device['idThietBi'] === $selectedDeviceId) {
        $selectedDevice = $device;
        break;
    }
}
if ($selectedDevice === null && $filteredCount > 0) {
    $selectedDevice = $filteredDevices[0];
}
?>
<main class="main-content">
  <section class="device-page">
    <div class="device-layout">
      <div class="device-left">
        <div class="page-head">
          <div>
            <h2>Quản lý thiết bị</h2>
            <p>Danh sách thiết bị đã đăng ký trong hệ thống</p>
          </div>
          <div class="device-live-badge" id="liveBadge">
            <span class="live-dot"></span>
            <span class="live-label">Đang cập nhật</span>
            <span class="live-timer" id="liveTimer">3s</span>
          </div>
          <div class="page-tabs">
            <?php
            $tabs = array(
                array('key' => 'all', 'label' => 'Tất cả', 'count' => $totalDevices),
                array('key' => 'hoat_dong', 'label' => 'Hoạt động', 'count' => $activeCount),
                array('key' => 'cho_kich_hoat', 'label' => 'Chờ kích hoạt', 'count' => $pendingCount),
                array('key' => 'khoa', 'label' => 'Đã khóa', 'count' => $lockedCount),
            );
            foreach ($tabs as $tab) {
                $tabUrl = device_list_url($tab['key'], $selectedDeviceId, '', '', $loaiFilter);
            ?>
            <a class="head-tab <?php echo $statusFilter === $tab['key'] ? 'active' : ''; ?>" href="<?php echo htmlspecialchars($tabUrl, ENT_QUOTES, 'UTF-8'); ?>">
              <?php echo htmlspecialchars($tab['label'], ENT_QUOTES, 'UTF-8'); ?> (<?php echo (int) $tab['count']; ?>)
            </a>
            <?php } ?>
          </div>
          <div class="page-loai-filter" style="margin-top:8px;">
            <form method="get" style="display:flex;gap:8px;align-items:center;">
              <input type="hidden" name="usecase" value="device" />
              <?php if ($statusFilter !== 'all') { ?>
              <input type="hidden" name="status" value="<?php echo htmlspecialchars($statusFilter, ENT_QUOTES, 'UTF-8'); ?>" />
              <?php } ?>
              <?php if ($selectedDeviceId > 0) { ?>
              <input type="hidden" name="selected" value="<?php echo (int) $selectedDeviceId; ?>" />
              <?php } ?>
              <label style="font-size:13px;">Loại thiết bị:</label>
              <select name="loai" onchange="this.form.submit()">
                <?php foreach (device_loai_options() as $key => $label) { ?>
                <option value="<?php echo htmlspecialchars($key, ENT_QUOTES, 'UTF-8'); ?>" <?php echo $loaiFilter === $key ? 'selected' : ''; ?>>
                  <?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?>
                </option>
                <?php } ?>
              </select>
            </form>
          </div>
        </div>

        <?php if ($deviceError !== '') { ?>
        <div class="store-edit-alert error"><?php echo htmlspecialchars($deviceError, ENT_QUOTES, 'UTF-8'); ?></div>
        <?php } ?>
        <?php if ($deviceNotice !== '') { ?>
        <div class="store-edit-alert warning"><?php echo htmlspecialchars($deviceNotice, ENT_QUOTES, 'UTF-8'); ?></div>
        <?php } ?>
        <?php if ($deviceMessage !== '') { ?>
        <div class="store-edit-alert success"><?php echo htmlspecialchars($deviceMessage, ENT_QUOTES, 'UTF-8'); ?></div>
        <?php } ?>

        <div class="panel device-table-panel">
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>MÃ THIẾT BỊ</th>
                  <th>LOẠI</th>
                  <th>MODEL</th>
                  <th>CHỦ SỞ HỮU</th>
                  <th>TRỰC TUYẾN</th>
                  <th>LẦN CUỐI HOẠT ĐỘNG</th>
                  <th>TRẠNG THÁI</th>
                </tr>
              </thead>
              <tbody>
                <?php if ($filteredCount === 0) { ?>
                <tr>
                  <td colspan="7" class="device-empty">Không có thiết bị phù hợp để hiển thị.</td>
                </tr>
                <?php } ?>
                <?php foreach ($filteredDevices as $device) { ?>
                <?php
                $statusMeta = device_status_meta($device['trangThai'] ?? '');
                $isSelected = $selectedDevice !== null
                    && isset($selectedDevice['idThietBi'], $device['idThietBi'])
                    && (int) $selectedDevice['idThietBi'] === (int) $device['idThietBi'];
                $selectUrl = device_list_url($statusFilter, (int) $device['idThietBi'], '', '', $loaiFilter);
                ?>
                <?php
                $lanCuoi = $device['lanCuoiHoatDong'] ?? '';
                $isOnline = false;
                if (!empty($lanCuoi)) {
                    $ts = strtotime((string) $lanCuoi);
                    $isOnline = $ts !== false && (time() - $ts) <= 45;
                }
                $lanCuoiIso = !empty($lanCuoi) ? date('c', strtotime((string) $lanCuoi)) : '';
                ?>
                <tr class="<?php echo $isSelected ? 'account-row-active' : ''; ?>" data-device-id="<?php echo (int) ($device['idThietBi'] ?? 0); ?>">
                  <td>
                    <a class="device-link" href="<?php echo htmlspecialchars($selectUrl, ENT_QUOTES, 'UTF-8'); ?>">
                      <span class="device-code"><?php echo htmlspecialchars((string) ($device['maThietBi'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></span>
                    </a>
                  </td>
                  <td>
                    <span class="loai-badge"><?php echo htmlspecialchars(device_loai_label($device['loaiThietBi'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></span>
                  </td>
                  <td>
                    <?php if (!empty($device['model']) || !empty($device['platform'])) { ?>
                    <div class="device-model">
                      <?php if (!empty($device['model'])) { ?>
                      <span><?php echo htmlspecialchars((string) $device['model'], ENT_QUOTES, 'UTF-8'); ?></span>
                      <?php } ?>
                      <?php if (!empty($device['platform'])) { ?>
                      <small style="display:block;color:#8892a6;">
                        <?php echo htmlspecialchars((string) $device['platform'], ENT_QUOTES, 'UTF-8'); ?><?php echo !empty($device['appVersion']) ? ' v' . htmlspecialchars((string) $device['appVersion'], ENT_QUOTES, 'UTF-8') : ''; ?>
                      </small>
                      <?php } ?>
                    </div>
                    <?php } else { ?>
                    <span class="device-no-owner">—</span>
                    <?php } ?>
                  </td>
                  <td>
                    <?php if (!empty($device['tenChuSoHuu'])) { ?>
                    <div class="device-owner">
                      <span class="owner-name"><?php echo htmlspecialchars((string) $device['tenChuSoHuu'], ENT_QUOTES, 'UTF-8'); ?></span>
                      <?php if (!empty($device['emailChuSoHuu'])) { ?>
                      <span class="owner-email"><?php echo htmlspecialchars((string) $device['emailChuSoHuu'], ENT_QUOTES, 'UTF-8'); ?></span>
                      <?php } ?>
                    </div>
                    <?php } else { ?>
                    <span class="device-no-owner">Chưa liên kết</span>
                    <?php } ?>
                  </td>
                  <td class="online-cell">
                    <span class="online-badge <?php echo $isOnline ? 'is-online' : 'is-offline'; ?>">
                      <span class="online-dot"></span>
                      <span><?php echo $isOnline ? 'Online' : 'Offline'; ?></span>
                    </span>
                  </td>
                  <td class="last-seen-cell" data-ts="<?php echo htmlspecialchars($lanCuoiIso, ENT_QUOTES, 'UTF-8'); ?>">
                    <?php echo device_format_datetime($lanCuoi); ?>
                  </td>
                  <td>
                    <span class="status-text <?php echo htmlspecialchars($statusMeta['class'], ENT_QUOTES, 'UTF-8'); ?>">
                      <span class="mini-dot"></span>
                      <?php echo htmlspecialchars($statusMeta['label'], ENT_QUOTES, 'UTF-8'); ?>
                    </span>
                  </td>
                </tr>
                <?php } ?>
              </tbody>
            </table>
          </div>
        </div>

        <div class="table-footer">
          <p>Hiển thị <?php echo $filteredCount; ?> thiết bị trong hệ thống</p>
        </div>
      </div>

      <div class="device-right">
        <div class="panel permission-panel">
          <h3>Thông tin thiết bị</h3>
          <p class="permission-desc">Bấm vào từng dòng để xem chi tiết và cập nhật trạng thái.</p>

          <?php if ($selectedDevice !== null) { ?>
          <?php $selectedStatus = device_status_meta($selectedDevice['trangThai'] ?? ''); ?>

          <div class="section-label">THIẾT BỊ ĐANG CHỌN</div>
          <div class="selected-user">
            <div class="selected-avatar device-avatar">
              <i class="fa-solid fa-mobile-screen-button"></i>
            </div>
            <div>
              <h4 class="device-selected-code"><?php echo htmlspecialchars((string) ($selectedDevice['maThietBi'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></h4>
              <p>ID: <?php echo (int) ($selectedDevice['idThietBi'] ?? 0); ?></p>
            </div>
          </div>

          <div class="readonly-stack">
            <div class="readonly-item">
              <span>Trạng thái</span>
              <strong>
                <span class="status-text <?php echo htmlspecialchars($selectedStatus['class'], ENT_QUOTES, 'UTF-8'); ?>">
                  <span class="mini-dot"></span>
                  <?php echo htmlspecialchars($selectedStatus['label'], ENT_QUOTES, 'UTF-8'); ?>
                </span>
              </strong>
            </div>
            <div class="readonly-item">
              <span>Đã kích hoạt</span>
              <strong><?php echo !empty($selectedDevice['daKichHoat']) ? 'Có' : 'Chưa'; ?></strong>
            </div>
            <div class="readonly-item">
              <span>Thời gian kích hoạt</span>
              <strong><?php echo device_format_datetime($selectedDevice['thoiGianKichHoat'] ?? ''); ?></strong>
            </div>
            <div class="readonly-item">
              <span>Lần cuối hoạt động</span>
              <strong><?php echo device_format_datetime($selectedDevice['lanCuoiHoatDong'] ?? ''); ?></strong>
            </div>
            <div class="readonly-item">
              <span>Loại</span>
              <strong><?php echo htmlspecialchars(device_loai_label($selectedDevice['loaiThietBi'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <?php if (!empty($selectedDevice['platform'])) { ?>
            <div class="readonly-item">
              <span>Nền tảng</span>
              <strong><?php echo htmlspecialchars((string) $selectedDevice['platform'], ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <?php } ?>
            <?php if (!empty($selectedDevice['model'])) { ?>
            <div class="readonly-item">
              <span>Model</span>
              <strong><?php echo htmlspecialchars((string) $selectedDevice['model'], ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <?php } ?>
            <?php if (!empty($selectedDevice['manufacturer'])) { ?>
            <div class="readonly-item">
              <span>Nhà sản xuất</span>
              <strong><?php echo htmlspecialchars((string) $selectedDevice['manufacturer'], ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <?php } ?>
            <?php if (!empty($selectedDevice['appVersion'])) { ?>
            <div class="readonly-item">
              <span>Phiên bản app</span>
              <strong><?php echo htmlspecialchars((string) $selectedDevice['appVersion'], ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <?php } ?>
            <?php if (!empty($selectedDevice['tenChuSoHuu'])) { ?>
            <div class="readonly-item">
              <span>Chủ sở hữu</span>
              <strong><?php echo htmlspecialchars((string) $selectedDevice['tenChuSoHuu'], ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <?php } ?>
            <?php if (!empty($selectedDevice['emailChuSoHuu'])) { ?>
            <div class="readonly-item">
              <span>Email</span>
              <strong><?php echo htmlspecialchars((string) $selectedDevice['emailChuSoHuu'], ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <?php } ?>
          </div>

          <div class="section-label role-label">CẬP NHẬT TRẠNG THÁI</div>
          <form method="post" class="account-form">
            <input type="hidden" name="device_action" value="update_status" />
            <input type="hidden" name="maThietBi" value="<?php echo htmlspecialchars((string) ($selectedDevice['maThietBi'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
            <input type="hidden" name="idThietBi" value="<?php echo (int) ($selectedDevice['idThietBi'] ?? 0); ?>" />
            <label>
              <span>Trạng thái thiết bị</span>
              <select name="trangThai">
                <?php foreach (device_status_options() as $val => $label) { ?>
                <option value="<?php echo htmlspecialchars($val, ENT_QUOTES, 'UTF-8'); ?>" <?php echo (($selectedDevice['trangThai'] ?? '') === $val) ? 'selected' : ''; ?>>
                  <?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?>
                </option>
                <?php } ?>
              </select>
            </label>
            <button class="update-btn" type="submit">Lưu trạng thái</button>
          </form>

          <?php } else { ?>
          <div class="account-empty-side">Không có thiết bị nào trong bộ lọc hiện tại.</div>
          <?php } ?>
        </div>

        <div class="role-chart-card">
          <h4>Thống kê thiết bị</h4>
          <div class="role-chart-item">
            <div class="chart-row"><span>Hoạt động</span><strong><?php echo $activeCount; ?></strong></div>
            <div class="progress-line cyan-line">
              <span style="width: <?php echo $totalDevices > 0 ? round(($activeCount / $totalDevices) * 100, 2) : 0; ?>%;"></span>
            </div>
          </div>
          <div class="role-chart-item">
            <div class="chart-row"><span>Chờ kích hoạt</span><strong><?php echo $pendingCount; ?></strong></div>
            <div class="progress-line purple-line">
              <span style="width: <?php echo $totalDevices > 0 ? round(($pendingCount / $totalDevices) * 100, 2) : 0; ?>%;"></span>
            </div>
          </div>
          <div class="role-chart-item">
            <div class="chart-row"><span>Đã khóa</span><strong><?php echo $lockedCount; ?></strong></div>
            <div class="progress-line">
              <span style="width: <?php echo $totalDevices > 0 ? round(($lockedCount / $totalDevices) * 100, 2) : 0; ?>%; background: var(--danger);"></span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</main>
<script>
(function () {
  var REFRESH_SEC = 3;
  var ONLINE_SEC  = 45; // 45s — heartbeat 15s, cho miss 2 lần + latency

  var timer = REFRESH_SEC;
  var timerEl = document.getElementById('liveTimer');
  var badgeEl = document.getElementById('liveBadge');

  function relativeTime(isoStr) {
    if (!isoStr) return 'Chưa có';
    var d = new Date(isoStr);
    if (isNaN(d)) return 'Chưa có';
    var diff = Math.floor((Date.now() - d.getTime()) / 1000);
    if (diff < 10)  return 'Vừa xong';
    if (diff < 60)  return diff + ' giây trước';
    if (diff < 3600) return Math.floor(diff / 60) + ' phút trước';
    if (diff < 86400) return Math.floor(diff / 3600) + ' giờ trước';
    return Math.floor(diff / 86400) + ' ngày trước';
  }

  function isOnline(isoStr) {
    if (!isoStr) return false;
    var d = new Date(isoStr);
    if (isNaN(d)) return false;
    return (Date.now() - d.getTime()) / 1000 <= ONLINE_SEC;
  }

  // Cập nhật cột "Lần cuối hoạt động" và badge online mỗi giây
  function tickRelativeTimes() {
    document.querySelectorAll('td.last-seen-cell[data-ts]').forEach(function (td) {
      var ts = td.getAttribute('data-ts');
      td.textContent = relativeTime(ts);

      var row = td.closest('tr');
      if (!row) return;
      var onlineCell = row.querySelector('td.online-cell');
      if (!onlineCell) return;
      var online = isOnline(ts);
      var badge = onlineCell.querySelector('.online-badge');
      if (!badge) return;
      badge.className = 'online-badge ' + (online ? 'is-online' : 'is-offline');
      badge.querySelector('span:last-child').textContent = online ? 'Online' : 'Offline';
    });
  }

  // Poll API và cập nhật data-ts
  function fetchDevices() {
    badgeEl && badgeEl.classList.add('refreshing');
    fetch('api/devices-proxy.php', { credentials: 'same-origin' })
      .then(function (r) { return r.json(); })
      .then(function (data) {
        if (!Array.isArray(data)) return;
        data.forEach(function (dev) {
          var id  = dev.idThietBi || dev.IdThietBi;
          var lan = dev.lanCuoiHoatDong || dev.LanCuoiHoatDong || '';
          var iso = lan ? new Date(lan).toISOString() : '';
          var row = document.querySelector('tr[data-device-id="' + id + '"]');
          if (row) {
            var cell = row.querySelector('td.last-seen-cell');
            if (cell && iso) cell.setAttribute('data-ts', iso);
          }
        });
        tickRelativeTimes();
      })
      .catch(function () {})
      .finally(function () {
        badgeEl && badgeEl.classList.remove('refreshing');
      });
  }

  // Đếm ngược & refresh
  setInterval(function () {
    timer--;
    if (timerEl) timerEl.textContent = timer + 's';
    if (timer <= 0) {
      timer = REFRESH_SEC;
      fetchDevices();
    }
  }, 1000);

  // Cập nhật relative time mỗi 5 giây
  setInterval(tickRelativeTimes, 3000);

  // Chạy ngay lần đầu
  tickRelativeTimes();
}());
</script>
