<?php
$auth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($auth['idTaiKhoan']) ? (int) $auth['idTaiKhoan'] : 0;
$selectedPackageId = isset($_GET['selected']) ? (int) $_GET['selected'] : 0;
$statusFilter = isset($_GET['status']) ? strtolower(trim((string) $_GET['status'])) : 'all';
$flashMessage = isset($_GET['message']) ? (string) $_GET['message'] : '';
$flashError = isset($_GET['error']) ? (string) $_GET['error'] : '';
$flashNotice = isset($_GET['notice']) ? (string) $_GET['notice'] : '';

if (!in_array($statusFilter, array('all', 'hoat_dong', 'tam_ngung', 'ngung_ap_dung'), true)) {
    $statusFilter = 'all';
}

function service_packages_path($idGoi = null, $suffix = '')
{
    $path = 'Admin/service-packages';
    if ($idGoi !== null) {
        $path .= '/' . rawurlencode((string) $idGoi);
    }
    if ($suffix !== '') {
        $path .= '/' . ltrim($suffix, '/');
    }
    return $path;
}

function service_page_url($statusFilter, $selectedPackageId = 0, $message = '', $error = '', $notice = '', $action = '')
{
    $params = array('usecase' => 'service');

    if ($statusFilter !== 'all') {
        $params['status'] = $statusFilter;
    }

    if ($selectedPackageId > 0) {
        $params['selected'] = $selectedPackageId;
    }

    if ($message !== '') {
        $params['message'] = $message;
    }

    if ($error !== '') {
        $params['error'] = $error;
    }

    if ($notice !== '') {
        $params['notice'] = $notice;
    }

    if ($action !== '') {
        $params['action'] = $action;
    }

    return admin_url('index1st.php?' . http_build_query($params));
}

function service_redirect($url)
{
    if (!headers_sent()) {
        header('Location: ' . $url);
        exit;
    }

    $encodedUrl = json_encode($url, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    $escapedUrl = htmlspecialchars($url, ENT_QUOTES, 'UTF-8');
    echo '<script>window.location.replace(' . $encodedUrl . ');</script>';
    echo '<noscript><meta http-equiv="refresh" content="0;url=' . $escapedUrl . '"><a href="' . $escapedUrl . '">Tiếp tục</a></noscript>';
    exit;
}

function service_status_meta($status)
{
    $status = strtolower(trim((string) $status));

    switch ($status) {
        case 'tam_ngung':
            return array('label' => 'Tạm ngưng', 'class' => 'paused');
        case 'ngung_ap_dung':
            return array('label' => 'Ngừng áp dụng', 'class' => 'stopped');
        default:
            return array('label' => 'Hoạt động', 'class' => 'active');
    }
}

function service_status_options()
{
    return array(
        'hoat_dong' => 'Hoạt động',
        'tam_ngung' => 'Tạm ngưng',
        'ngung_ap_dung' => 'Ngừng áp dụng',
    );
}

function service_format_currency($value)
{
    return number_format((float) $value, 0, ',', '.') . ' đ';
}

function service_format_datetime($value)
{
    $timestamp = strtotime((string) $value);
    if ($timestamp === false) {
        return 'Chưa có';
    }

    return date('d/m/Y H:i', $timestamp);
}

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $action = isset($_POST['service_action']) ? (string) $_POST['service_action'] : '';
    $postStatusFilter = isset($_POST['status_filter']) ? strtolower(trim((string) $_POST['status_filter'])) : $statusFilter;
    if (!in_array($postStatusFilter, array('all', 'hoat_dong', 'tam_ngung', 'ngung_ap_dung'), true)) {
        $postStatusFilter = 'all';
    }

    if ($action === 'save') {
        $idGoi = isset($_POST['idGoi']) ? (int) $_POST['idGoi'] : 0;
        $payload = array(
            'ten' => trim((string) ($_POST['ten'] ?? '')),
            'moTa' => trim((string) ($_POST['moTa'] ?? '')),
            'gia' => (float) ($_POST['gia'] ?? 0),
            'thoiHanNgay' => (int) ($_POST['thoiHanNgay'] ?? 0),
            'trangThai' => trim((string) ($_POST['trangThai'] ?? 'hoat_dong')),
        );

        if ($payload['ten'] === '' || $payload['gia'] < 0 || $payload['thoiHanNgay'] <= 0 || !array_key_exists($payload['trangThai'], service_status_options())) {
            service_redirect(service_page_url($postStatusFilter, $idGoi, '', 'Thông tin gói dịch vụ chưa hợp lệ.'));
        }

        $apiError = '';
        $apiHttpCode = 0;
        $result = admin_api_call(
            $idGoi > 0 ? 'PUT' : 'POST',
            service_packages_path($idGoi > 0 ? $idGoi : null),
            array(
                'ten' => $payload['ten'],
                'moTa' => $payload['moTa'],
                'gia' => $payload['gia'],
                'thoiHanNgay' => $payload['thoiHanNgay'],
                'trangThai' => $payload['trangThai'],
            ),
            $apiError,
            $apiHttpCode,
            array('idTaiKhoan' => $idTaiKhoan)
        );

        if ($result === null) {
            $msg = $apiError !== '' ? $apiError : 'Không thể lưu gói dịch vụ: backend API chưa sẵn sàng.';
            service_redirect(service_page_url($postStatusFilter, $idGoi, '', $msg));
        }

        $savedId = isset($result['idGoi']) ? (int) $result['idGoi'] : $idGoi;
        service_redirect(service_page_url($postStatusFilter, $savedId, $idGoi > 0 ? 'Cập nhật gói dịch vụ thành công.' : 'Tạo gói dịch vụ thành công.'));
    }

    if ($action === 'change_status') {
        $idGoi = isset($_POST['idGoi']) ? (int) $_POST['idGoi'] : 0;
        $newStatus = trim((string) ($_POST['new_status'] ?? ''));

        if ($idGoi <= 0 || !array_key_exists($newStatus, service_status_options())) {
            service_redirect(service_page_url($postStatusFilter, $idGoi, '', 'Không thể đổi trạng thái gói dịch vụ.'));
        }

        $apiError = '';
        $apiHttpCode = 0;
        $result = admin_api_call(
            'PATCH',
            service_packages_path($idGoi, 'status'),
            array('trangThai' => $newStatus),
            $apiError,
            $apiHttpCode,
            array('idTaiKhoan' => $idTaiKhoan)
        );

        if ($result === null) {
            $msg = $apiError !== '' ? $apiError : 'Không thể đổi trạng thái: backend API chưa sẵn sàng.';
            service_redirect(service_page_url($postStatusFilter, $idGoi, '', $msg));
        }

        service_redirect(service_page_url($postStatusFilter, $idGoi, 'Đổi trạng thái gói dịch vụ thành công.'));
    }
}

$serviceError = $flashError;
$serviceNotice = $flashNotice;
$serviceMessage = $flashMessage;
$apiHttpCode = 0;
$packages = $idTaiKhoan > 0
    ? admin_api_call('GET', service_packages_path(), null, $serviceError, $apiHttpCode, array('idTaiKhoan' => $idTaiKhoan))
    : null;

if (!is_array($packages)) {
    $packages = array();
    if ($serviceError === '') {
        $serviceError = 'Không thể tải danh sách gói dịch vụ: backend API chưa sẵn sàng.';
    }
}

$filteredPackages = array();
$counts = array(
    'all' => 0,
    'hoat_dong' => 0,
    'tam_ngung' => 0,
    'ngung_ap_dung' => 0,
);

foreach ($packages as $package) {
    if (!is_array($package)) {
        continue;
    }

    $status = strtolower(trim((string) ($package['trangThai'] ?? 'hoat_dong')));
    $counts['all']++;
    if (isset($counts[$status])) {
        $counts[$status]++;
    }

    if ($statusFilter === 'all' || $statusFilter === $status) {
        $filteredPackages[] = $package;
    }
}

$selectedPackage = null;
foreach ($filteredPackages as $package) {
    if (isset($package['idGoi']) && (int) $package['idGoi'] === $selectedPackageId) {
        $selectedPackage = $package;
        break;
    }
}

if ($selectedPackage === null && count($filteredPackages) > 0) {
    $selectedPackage = $filteredPackages[0];
}

$isCreateMode = isset($_GET['action']) && $_GET['action'] === 'new';
if ($isCreateMode) {
    $selectedPackage = array(
        'idGoi' => 0,
        'ten' => '',
        'moTa' => '',
        'gia' => 0,
        'thoiHanNgay' => 30,
        'trangThai' => 'hoat_dong',
        'ngayTao' => '',
    );
}

$existingPackageNames = array();
foreach ($packages as $package) {
    if (!is_array($package)) {
        continue;
    }

    $existingPackageNames[] = array(
        'idGoi' => isset($package['idGoi']) ? (int) $package['idGoi'] : 0,
        'ten' => isset($package['ten']) ? (string) $package['ten'] : '',
    );
}
?>
<main class="main-content">
  <section class="service-page">
    <?php if ($serviceError !== '' || $serviceNotice !== '' || $serviceMessage !== '') { ?>
    <div class="service-toast-stack" aria-live="polite">
      <?php if ($serviceError !== '') { ?>
      <div class="service-toast error" role="alert">
        <div class="service-toast-copy">
          <strong>Lỗi thao tác</strong>
          <p><?php echo htmlspecialchars($serviceError, ENT_QUOTES, 'UTF-8'); ?></p>
        </div>
        <button class="service-toast-close" type="button" aria-label="Đóng thông báo">&times;</button>
      </div>
      <?php } ?>
      <?php if ($serviceNotice !== '') { ?>
      <div class="service-toast warning" role="status">
        <div class="service-toast-copy">
          <strong>Lưu ý</strong>
          <p><?php echo htmlspecialchars($serviceNotice, ENT_QUOTES, 'UTF-8'); ?></p>
        </div>
        <button class="service-toast-close" type="button" aria-label="Đóng thông báo">&times;</button>
      </div>
      <?php } ?>
      <?php if ($serviceMessage !== '') { ?>
      <div class="service-toast success" role="status">
        <div class="service-toast-copy">
          <strong>Thành công</strong>
          <p><?php echo htmlspecialchars($serviceMessage, ENT_QUOTES, 'UTF-8'); ?></p>
        </div>
        <button class="service-toast-close" type="button" aria-label="Đóng thông báo">&times;</button>
      </div>
      <?php } ?>
    </div>
    <?php } ?>

    <div class="service-layout">
      <div class="service-left">
        <div class="page-head">
          <div>
            <h2>Dịch vụ</h2>
            <p>Quản lý các gói dịch vụ trong hệ thống, bao gồm giá, thời hạn sử dụng và trạng thái áp dụng.</p>
          </div>
          <a class="primary-btn" href="<?php echo htmlspecialchars(service_page_url($statusFilter, 0, '', '', '', 'new'), ENT_QUOTES, 'UTF-8'); ?>">
            <i class="fa-solid fa-plus"></i>
            <span>Thêm gói dịch vụ</span>
          </a>
        </div>

        <div class="service-tabs">
          <?php
          $tabs = array(
              'all' => 'Tất cả',
              'hoat_dong' => 'Hoạt động',
              'tam_ngung' => 'Tạm ngưng',
              'ngung_ap_dung' => 'Ngừng áp dụng',
          );
          foreach ($tabs as $tabKey => $tabLabel) {
          ?>
          <a class="service-tab <?php echo $statusFilter === $tabKey ? 'active' : ''; ?>" href="<?php echo htmlspecialchars(service_page_url($tabKey, $selectedPackageId), ENT_QUOTES, 'UTF-8'); ?>">
            <?php echo htmlspecialchars($tabLabel, ENT_QUOTES, 'UTF-8'); ?> (<?php echo (int) $counts[$tabKey]; ?>)
          </a>
          <?php } ?>
        </div>

        <div class="service-table-panel">
          <table>
            <thead>
              <tr>
                <th>GÓI DỊCH VỤ</th>
                <th>GIÁ</th>
                <th>THỜI HẠN</th>
                <th>TRẠNG THÁI</th>
                <th>NGÀY TẠO</th>
              </tr>
            </thead>
            <tbody>
              <?php if (count($filteredPackages) === 0) { ?>
              <tr>
                <td colspan="5" class="empty-cell">Không có gói dịch vụ phù hợp với bộ lọc hiện tại.</td>
              </tr>
              <?php } ?>
              <?php foreach ($filteredPackages as $package) { ?>
              <?php
              $statusMeta = service_status_meta($package['trangThai'] ?? 'hoat_dong');
              $isSelected = $selectedPackage !== null
                  && isset($selectedPackage['idGoi'], $package['idGoi'])
                  && (int) $selectedPackage['idGoi'] === (int) $package['idGoi'];
              ?>
              <tr class="<?php echo $isSelected ? 'row-active' : ''; ?>">
                <td>
                  <a class="package-link" href="<?php echo htmlspecialchars(service_page_url($statusFilter, (int) $package['idGoi']), ENT_QUOTES, 'UTF-8'); ?>">
                    <strong><?php echo htmlspecialchars((string) $package['ten'], ENT_QUOTES, 'UTF-8'); ?></strong>
                    <span><?php echo htmlspecialchars((string) ($package['moTa'] ?? 'Chưa có mô tả'), ENT_QUOTES, 'UTF-8'); ?></span>
                  </a>
                </td>
                <td><?php echo htmlspecialchars(service_format_currency($package['gia'] ?? 0), ENT_QUOTES, 'UTF-8'); ?></td>
                <td><?php echo (int) ($package['thoiHanNgay'] ?? 0); ?> ngày</td>
                <td><span class="status-badge <?php echo htmlspecialchars($statusMeta['class'], ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($statusMeta['label'], ENT_QUOTES, 'UTF-8'); ?></span></td>
                <td><?php echo htmlspecialchars(service_format_datetime($package['ngayTao'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></td>
              </tr>
              <?php } ?>
            </tbody>
          </table>
        </div>
      </div>

      <div class="service-right">
        <div class="service-panel">
          <h3><?php echo !empty($selectedPackage['idGoi']) ? 'Cập nhật gói dịch vụ' : 'Tạo gói dịch vụ'; ?></h3>
          <p>Chọn một gói trong danh sách để chỉnh sửa, hoặc bấm thêm mới để tạo gói dịch vụ khác.</p>

          <?php if ($selectedPackage !== null) { ?>
          <form method="post" class="service-form">
            <input type="hidden" name="service_action" value="save" />
            <input type="hidden" name="idGoi" value="<?php echo (int) ($selectedPackage['idGoi'] ?? 0); ?>" />
            <input type="hidden" name="status_filter" value="<?php echo htmlspecialchars($statusFilter, ENT_QUOTES, 'UTF-8'); ?>" />

            <label>
              <span>Tên gói</span>
              <input type="text" name="ten" value="<?php echo htmlspecialchars((string) ($selectedPackage['ten'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" required />
            </label>

            <label>
              <span>Mô tả</span>
              <textarea name="moTa" rows="4"><?php echo htmlspecialchars((string) ($selectedPackage['moTa'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></textarea>
            </label>

            <div class="field-grid">
              <label>
                <span>Giá</span>
                <input type="number" min="0" step="1000" name="gia" value="<?php echo htmlspecialchars((string) ($selectedPackage['gia'] ?? 0), ENT_QUOTES, 'UTF-8'); ?>" required />
              </label>
              <label>
                <span>Thời hạn ngày</span>
                <input type="number" min="1" step="1" name="thoiHanNgay" value="<?php echo htmlspecialchars((string) ($selectedPackage['thoiHanNgay'] ?? 30), ENT_QUOTES, 'UTF-8'); ?>" required />
              </label>
            </div>

            <label>
              <span>Trạng thái</span>
              <select name="trangThai">
                <?php foreach (service_status_options() as $statusValue => $statusLabel) { ?>
                <option value="<?php echo htmlspecialchars($statusValue, ENT_QUOTES, 'UTF-8'); ?>" <?php echo (($selectedPackage['trangThai'] ?? 'hoat_dong') === $statusValue) ? 'selected' : ''; ?>>
                  <?php echo htmlspecialchars($statusLabel, ENT_QUOTES, 'UTF-8'); ?>
                </option>
                <?php } ?>
              </select>
            </label>

            <button class="primary-btn full-width" type="submit">
              <i class="fa-solid fa-floppy-disk"></i>
              <span><?php echo !empty($selectedPackage['idGoi']) ? 'Lưu thay đổi' : 'Tạo gói dịch vụ'; ?></span>
            </button>
          </form>

          <?php if (!empty($selectedPackage['idGoi'])) { ?>
          <div class="service-meta">
            <div>
              <span>Mã gói</span>
              <strong>#<?php echo (int) $selectedPackage['idGoi']; ?></strong>
            </div>
            <div>
              <span>Ngày tạo</span>
              <strong><?php echo htmlspecialchars(service_format_datetime($selectedPackage['ngayTao'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
          </div>
          <?php } ?>
          <?php } else { ?>
          <div class="empty-cell">Chưa có gói dịch vụ nào để hiển thị.</div>
          <?php } ?>
        </div>
      </div>
    </div>
  </section>
</main>
<script>
document.querySelectorAll('.service-toast').forEach(function (toast) {
  var closeButton = toast.querySelector('.service-toast-close');
  var dismiss = function () {
    toast.classList.add('is-hiding');
    window.setTimeout(function () {
      toast.remove();
    }, 220);
  };

  if (closeButton) {
    closeButton.addEventListener('click', dismiss);
  }

  window.setTimeout(dismiss, 4800);
});

var serviceForm = document.querySelector('.service-form');
if (serviceForm) {
  var packageNameInput = serviceForm.querySelector('input[name="ten"]');
  var packageIdInput = serviceForm.querySelector('input[name="idGoi"]');
  var existingPackages = <?php echo json_encode($existingPackageNames, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;

  var normalizePackageName = function (value) {
    return String(value || '').trim().replace(/\s+/g, ' ').toLowerCase();
  };

  var hasDuplicatePackageName = function () {
    if (!packageNameInput) {
      return false;
    }

    var currentId = packageIdInput ? Number(packageIdInput.value || 0) : 0;
    var targetName = normalizePackageName(packageNameInput.value);
    if (!targetName) {
      return false;
    }

    return existingPackages.some(function (pkg) {
      return normalizePackageName(pkg.ten) === targetName && Number(pkg.idGoi || 0) !== currentId;
    });
  };

  var syncDuplicateValidity = function () {
    if (!packageNameInput) {
      return false;
    }

    if (hasDuplicatePackageName()) {
      packageNameInput.setCustomValidity('Tên dịch vụ không được trùng');
      return true;
    }

    packageNameInput.setCustomValidity('');
    return false;
  };

  if (packageNameInput) {
    packageNameInput.addEventListener('input', syncDuplicateValidity);
    packageNameInput.addEventListener('change', syncDuplicateValidity);
  }

  serviceForm.addEventListener('submit', function (event) {
    if (syncDuplicateValidity()) {
      event.preventDefault();
      packageNameInput.reportValidity();
    }
  });
}
</script>
