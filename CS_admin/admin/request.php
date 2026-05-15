<?php
$auth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($auth['idTaiKhoan']) ? (int) $auth['idTaiKhoan'] : 0;
$isOwnerRequestViewer = isset($auth['loaiTaiKhoan']) && $auth['loaiTaiKhoan'] === 'chu_quan_ly';
$statusFilter = isset($_GET['status']) ? strtolower(trim((string) $_GET['status'])) : 'all';
$selectedRequestId = isset($_GET['selected']) ? (int) $_GET['selected'] : 0;
$flashMessage = isset($_GET['message']) ? (string) $_GET['message'] : '';
$flashError = isset($_GET['error']) ? (string) $_GET['error'] : '';
$flashNotice = isset($_GET['notice']) ? (string) $_GET['notice'] : '';

if (!in_array($statusFilter, array('all', 'cho_duyet', 'cho_thanh_toan', 'da_duyet', 'tu_choi'), true)) {
    $statusFilter = 'all';
}

function request_path($isOwnerRequestViewer, $idYeuCau = null, $suffix = '')
{
    $path = $isOwnerRequestViewer ? 'Owner/store-requests' : 'Admin/store-requests';
    if ($idYeuCau !== null) {
        $path .= '/' . rawurlencode((string) $idYeuCau);
    }
    if ($suffix !== '') {
        $path .= '/' . ltrim($suffix, '/');
    }
    return $path;
}

function request_page_url($statusFilter, $selectedRequestId = 0, $message = '', $error = '', $notice = '')
{
    $params = array('usecase' => 'request');

    if ($statusFilter !== 'all') {
        $params['status'] = $statusFilter;
    }

    if ($selectedRequestId > 0) {
        $params['selected'] = $selectedRequestId;
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

    return admin_url('index1st.php?' . http_build_query($params));
}

function request_status_meta($status)
{
    $status = strtolower(trim((string) $status));

    switch ($status) {
        case 'cho_thanh_toan':
            return array('label' => 'CHỜ THANH TOÁN', 'class' => 'blue');
        case 'da_duyet':
            return array('label' => 'ĐÃ DUYỆT', 'class' => 'green');
        case 'tu_choi':
            return array('label' => 'Từ chối', 'class' => 'rejected');
        default:
            return array('label' => 'Chờ duyệt', 'class' => 'pending');
    }
}

function request_format_datetime($value)
{
    if (empty($value)) {
        return 'Chưa có';
    }

    $timestamp = strtotime((string) $value);
    if ($timestamp === false) {
        return 'Chưa có';
    }

    return date('d/m/Y H:i', $timestamp);
}

function request_display_text($value, $emptyText = 'Chưa có')
{
    $value = trim((string) $value);
    return $value !== '' ? $value : $emptyText;
}

if (!$isOwnerRequestViewer && $_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['request_action']) && $_POST['request_action'] === 'review') {
    $targetRequestId = isset($_POST['idYeuCau']) ? (int) $_POST['idYeuCau'] : 0;
    $decision = isset($_POST['decision']) ? strtolower(trim((string) $_POST['decision'])) : '';
    $phiHangThang = isset($_POST['phiHangThang']) && $_POST['phiHangThang'] !== '' ? (float) $_POST['phiHangThang'] : null;
    $lat = isset($_POST['lat']) && $_POST['lat'] !== '' ? (float) $_POST['lat'] : null;
    $lon = isset($_POST['lon']) && $_POST['lon'] !== '' ? (float) $_POST['lon'] : null;

    if ($targetRequestId <= 0) {
        header('Location: ' . request_page_url($statusFilter, 0, '', 'Không xác định được yêu cầu cần xử lý.', $flashNotice));
        exit;
    }

    if (!in_array($decision, array('cho_thanh_toan', 'tu_choi'), true)) {
        header('Location: ' . request_page_url($statusFilter, $targetRequestId, '', 'Trạng thái xử lý không hợp lệ.', $flashNotice));
        exit;
    }

    if ($decision === 'cho_thanh_toan' && ($phiHangThang === null || $lat === null || $lon === null)) {
        header('Location: ' . request_page_url($statusFilter, $targetRequestId, '', 'Vui long nhap phi hang thang, vi do va kinh do truoc khi phe duyet.', $flashNotice));
        exit;
    }

    $apiError = '';
    $httpCode = 0;
    $result = $idTaiKhoan > 0
        ? admin_api_call(
            'PATCH',
            request_path($isOwnerRequestViewer, $targetRequestId, 'review'),
            array(
                'trangThaiYeuCau' => $decision,
                'phiHangThang' => $phiHangThang,
                'lat' => $lat,
                'lon' => $lon,
            ),
            $apiError,
            $httpCode,
            array('idTaiKhoan' => $idTaiKhoan)
        )
        : null;

    if ($result === null) {
        $msg = $apiError !== '' ? $apiError : 'Không thể xử lý yêu cầu: backend API chưa sẵn sàng.';
        header('Location: ' . request_page_url($statusFilter, $targetRequestId, '', $msg, $flashNotice));
        exit;
    }

    $successMessage = $decision === 'cho_thanh_toan'
        ? 'Đã phê duyệt yêu cầu và tạo gian hàng mới.'
        : 'Đã từ chối yêu cầu.';

    header('Location: ' . request_page_url($statusFilter, $targetRequestId, $successMessage, '', $flashNotice));
    exit;
}

$requestError = '';
$requestHttpCode = 0;
$allRequests = $idTaiKhoan > 0
    ? admin_api_call('GET', request_path($isOwnerRequestViewer), null, $requestError, $requestHttpCode, array('idTaiKhoan' => $idTaiKhoan))
    : array();
$pageNotice = $flashNotice;
$usingFallback = false;

if (!is_array($allRequests)) {
    $allRequests = array();
    if ($flashError === '' && $requestError !== '') {
        $flashError = $requestError;
    } elseif ($flashError === '' && $idTaiKhoan > 0) {
        $flashError = 'Không thể tải danh sách yêu cầu: backend API chưa sẵn sàng.';
    }
}

$counts = array(
    'all' => count($allRequests),
    'cho_duyet' => 0,
    'cho_thanh_toan' => 0,
    'da_duyet' => 0,
    'tu_choi' => 0,
);

foreach ($allRequests as $requestItem) {
    $status = isset($requestItem['trangThaiYeuCau']) ? (string) $requestItem['trangThaiYeuCau'] : 'cho_duyet';
    if (isset($counts[$status])) {
        $counts[$status]++;
    }
}

$filteredRequests = array_values(array_filter($allRequests, function ($requestItem) use ($statusFilter) {
    if ($statusFilter === 'all') {
        return true;
    }

    return isset($requestItem['trangThaiYeuCau']) && $requestItem['trangThaiYeuCau'] === $statusFilter;
}));

$selectedRequest = null;
foreach ($allRequests as $requestItem) {
    if ((int) ($requestItem['idYeuCau'] ?? 0) === $selectedRequestId) {
        $selectedRequest = $requestItem;
        break;
    }
}

if ($selectedRequest === null && count($filteredRequests) > 0) {
    $selectedRequest = $filteredRequests[0];
    $selectedRequestId = (int) ($selectedRequest['idYeuCau'] ?? 0);
}
?>
<main class="main-content">
  <section class="request-page">
    <div class="page-head">
      <div>
        <h2>Yêu cầu</h2>
        <p><?php echo $isOwnerRequestViewer ? 'Theo dõi các yêu cầu mở gian hàng bạn đã gửi và xem trạng thái xử lý từ admin.' : 'Quản lý các yêu cầu mở gian hàng mới từ Chủ quản lý. Admin sẽ nhập phí hàng tháng và tọa độ khi xử lý.'; ?></p>
      </div>
    </div>

    <?php if ($flashMessage !== '') { ?>
      <div class="request-alert success"><?php echo htmlspecialchars($flashMessage, ENT_QUOTES, 'UTF-8'); ?></div>
    <?php } ?>

    <?php if ($flashError !== '') { ?>
      <div class="request-alert error"><?php echo htmlspecialchars($flashError, ENT_QUOTES, 'UTF-8'); ?></div>
    <?php } ?>

    <?php if ($pageNotice !== '') { ?>
      <div class="request-alert warning"><?php echo htmlspecialchars($pageNotice, ENT_QUOTES, 'UTF-8'); ?></div>
    <?php } ?>

    <div class="request-layout">
      <div>
        <div class="request-tabs">
          <a class="request-tab<?php echo $statusFilter === 'all' ? ' active' : ''; ?>" href="<?php echo htmlspecialchars(request_page_url('all', $selectedRequestId), ENT_QUOTES, 'UTF-8'); ?>">Tất cả (<?php echo (int) $counts['all']; ?>)</a>
          <a class="request-tab<?php echo $statusFilter === 'cho_duyet' ? ' active' : ''; ?>" href="<?php echo htmlspecialchars(request_page_url('cho_duyet', $selectedRequestId), ENT_QUOTES, 'UTF-8'); ?>">Chờ duyệt (<?php echo (int) $counts['cho_duyet']; ?>)</a>
          <a class="request-tab<?php echo $statusFilter === 'cho_thanh_toan' ? ' active' : ''; ?>" href="<?php echo htmlspecialchars(request_page_url('cho_thanh_toan', $selectedRequestId), ENT_QUOTES, 'UTF-8'); ?>">Chờ TT (<?php echo (int) $counts['cho_thanh_toan']; ?>)</a>
          <a class="request-tab<?php echo $statusFilter === 'da_duyet' ? ' active' : ''; ?>" href="<?php echo htmlspecialchars(request_page_url('da_duyet', $selectedRequestId), ENT_QUOTES, 'UTF-8'); ?>">Đã duyệt (<?php echo (int) $counts['da_duyet']; ?>)</a>
          <a class="request-tab<?php echo $statusFilter === 'tu_choi' ? ' active' : ''; ?>" href="<?php echo htmlspecialchars(request_page_url('tu_choi', $selectedRequestId), ENT_QUOTES, 'UTF-8'); ?>">Từ chối (<?php echo (int) $counts['tu_choi']; ?>)</a>
        </div>

        <div class="request-table-panel">
          <table>
            <thead>
              <tr>
                <th>YÊU CẦU</th>
                <th>CHỦ QUẢN LÝ</th>
                <th>NGÀY GỬI</th>
                <th>TRẠNG THÁI</th>
                <th>KẾT QUẢ</th>
              </tr>
            </thead>
            <tbody>
              <?php if (count($filteredRequests) === 0) { ?>
                <tr>
                  <td colspan="5" class="empty-cell">Chưa có yêu cầu nào ở bộ lọc hiện tại.</td>
                </tr>
              <?php } ?>

              <?php foreach ($filteredRequests as $requestItem) { ?>
                <?php
                $itemId = (int) ($requestItem['idYeuCau'] ?? 0);
                $statusMeta = request_status_meta($requestItem['trangThaiYeuCau'] ?? 'cho_duyet');
                $ownerName = request_display_text($requestItem['hoTenChuQuanLy'] ?? ($requestItem['usernameChuQuanLy'] ?? ''), 'Chủ quản lý');
                $resultText = 'Đang chờ admin xử lý';
                if (($requestItem['trangThaiYeuCau'] ?? '') === 'da_duyet' || ($requestItem['trangThaiYeuCau'] ?? '') === 'cho_thanh_toan') {
                    $resultText = !empty($requestItem['idGianHang']) ? 'Đã tạo gian hàng #' . (int) $requestItem['idGianHang'] : 'Đã duyệt';
                } elseif (($requestItem['trangThaiYeuCau'] ?? '') === 'tu_choi') {
                    $resultText = 'Không tạo gian hàng';
                }
                ?>
                <tr class="<?php echo $selectedRequestId === $itemId ? 'row-active' : ''; ?>">
                  <td>
                    <a class="request-link" href="<?php echo htmlspecialchars(request_page_url($statusFilter, $itemId), ENT_QUOTES, 'UTF-8'); ?>">
                      <strong><?php echo htmlspecialchars((string) ($requestItem['tenGianHang'] ?? 'Yêu cầu mở gian hàng'), ENT_QUOTES, 'UTF-8'); ?></strong>
                      <span>Thêm gian hàng</span>
                    </a>
                  </td>
                  <td>
                    <div class="table-owner">
                      <strong><?php echo htmlspecialchars($ownerName, ENT_QUOTES, 'UTF-8'); ?></strong>
                      <span><?php echo htmlspecialchars(request_display_text($requestItem['emailChuQuanLy'] ?? '', 'Chưa có email'), ENT_QUOTES, 'UTF-8'); ?></span>
                    </div>
                  </td>
                  <td><?php echo htmlspecialchars(request_format_datetime($requestItem['ngayTao'] ?? null), ENT_QUOTES, 'UTF-8'); ?></td>
                  <td><span class="status-badge <?php echo htmlspecialchars($statusMeta['class'], ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($statusMeta['label'], ENT_QUOTES, 'UTF-8'); ?></span></td>
                  <td><?php echo htmlspecialchars($resultText, ENT_QUOTES, 'UTF-8'); ?></td>
                </tr>
              <?php } ?>
            </tbody>
          </table>
        </div>

        <p class="request-summary">Hiển thị <?php echo count($filteredRequests); ?> trên tổng <?php echo count($allRequests); ?> yêu cầu<?php echo $usingFallback ? ' đang đọc từ DB fallback' : ''; ?>.</p>
      </div>

      <aside class="request-panel">
        <?php if ($selectedRequest === null) { ?>
          <div class="request-empty-panel">
            <h3>Chưa có dữ liệu</h3>
            <p><?php echo $isOwnerRequestViewer ? 'Chọn một yêu cầu từ danh sách bên trái để xem chi tiết bạn đã gửi.' : 'Chọn một yêu cầu từ danh sách bên trái để xem chi tiết và xử lý.'; ?></p>
          </div>
        <?php } else { ?>
          <?php
          $selectedStatus = request_status_meta($selectedRequest['trangThaiYeuCau'] ?? 'cho_duyet');
          $selectedOwnerName = request_display_text($selectedRequest['hoTenChuQuanLy'] ?? ($selectedRequest['usernameChuQuanLy'] ?? ''), 'Chủ quản lý');
          $selectedFee = isset($selectedRequest['phiHangThang']) && $selectedRequest['phiHangThang'] !== null
              ? number_format((float) $selectedRequest['phiHangThang'], 0, ',', '.')
              : '';
          ?>
          <div class="request-panel-card">
            <div class="request-panel-head">
              <div>
                <h3>Chi tiết yêu cầu</h3>
                <p><?php echo $isOwnerRequestViewer ? 'Xem lại thông tin yêu cầu bạn đã gửi và theo dõi kết quả xử lý.' : 'Kiểm tra thông tin trước khi phê duyệt hoặc từ chối.'; ?></p>
              </div>
              <span class="status-badge <?php echo htmlspecialchars($selectedStatus['class'], ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($selectedStatus['label'], ENT_QUOTES, 'UTF-8'); ?></span>
            </div>

            <div class="request-highlight">
              <strong><?php echo htmlspecialchars((string) ($selectedRequest['tenGianHang'] ?? 'Yêu cầu mở gian hàng'), ENT_QUOTES, 'UTF-8'); ?></strong>
              <span>Yêu cầu thêm gian hàng</span>
            </div>

            <div class="request-detail-grid">
              <div>
                <span>Chủ quản lý</span>
                <strong><?php echo htmlspecialchars($selectedOwnerName, ENT_QUOTES, 'UTF-8'); ?></strong>
                <small><?php echo htmlspecialchars(request_display_text($selectedRequest['emailChuQuanLy'] ?? '', 'Chưa có email'), ENT_QUOTES, 'UTF-8'); ?></small>
              </div>
              <div>
                <span>Ngày gửi</span>
                <strong><?php echo htmlspecialchars(request_format_datetime($selectedRequest['ngayTao'] ?? null), ENT_QUOTES, 'UTF-8'); ?></strong>
                <small>Tài khoản @<?php echo htmlspecialchars(request_display_text($selectedRequest['usernameChuQuanLy'] ?? '', 'chu_quan_ly'), ENT_QUOTES, 'UTF-8'); ?></small>
              </div>
              <div>
                <span>Địa chỉ đề nghị</span>
                <strong><?php echo htmlspecialchars(request_display_text($selectedRequest['diaChi'] ?? '', 'Chưa có địa chỉ'), ENT_QUOTES, 'UTF-8'); ?></strong>
                <small>ID yêu cầu #<?php echo (int) ($selectedRequest['idYeuCau'] ?? 0); ?></small>
              </div>
              <div>
                <span>Thông tin tạo gian hàng</span>
                <strong>
                  <?php if (!empty($selectedRequest['idGianHang'])) { ?>
                    Gian hàng #<?php echo (int) $selectedRequest['idGianHang']; ?>
                  <?php } else { ?>
                    Chưa tạo gian hàng
                  <?php } ?>
                </strong>
                <small>
                  <?php if ($selectedFee !== '') { ?>
                    Phí hàng tháng: <?php echo $selectedFee; ?> đ
                  <?php } else { ?>
                    Admin sẽ nhập phí và tọa độ khi duyệt
                  <?php } ?>
                </small>
              </div>
            </div>

            <div class="request-description">
              <span>Ghi chú gửi</span>
              <p><?php echo nl2br(htmlspecialchars(request_display_text($selectedRequest['moTa'] ?? '', 'Chủ quản lý chưa gửi ghi chú.'), ENT_QUOTES, 'UTF-8')); ?></p>
            </div>

            <?php if (!$isOwnerRequestViewer && ($selectedRequest['trangThaiYeuCau'] ?? 'cho_duyet') === 'cho_duyet') { ?>
              <form class="request-form" method="post" action="<?php echo htmlspecialchars(request_page_url($statusFilter, $selectedRequestId), ENT_QUOTES, 'UTF-8'); ?>">
                <input type="hidden" name="request_action" value="review" />
                <input type="hidden" name="idYeuCau" value="<?php echo (int) ($selectedRequest['idYeuCau'] ?? 0); ?>" />

                <label>
                  <span>Phí hàng tháng</span>
                  <input type="number" min="0" step="1000" name="phiHangThang" value="" placeholder="Nhập phí hàng tháng do admin duyệt" />
                </label>

                <div class="request-detail-grid">
                  <label>
                    <span>Vĩ độ (Lat)</span>
                    <input type="number" step="0.000001" name="lat" value="" placeholder="10.762622" />
                  </label>

                  <label>
                    <span>Kinh độ (Lon)</span>
                    <input type="number" step="0.000001" name="lon" value="" placeholder="106.660172" />
                  </label>
                </div>

                <div class="request-action-row">
                  <button class="primary-btn approve" type="submit" name="decision" value="cho_thanh_toan">Phê duyệt yêu cầu</button>
                  <button class="secondary-btn reject" type="submit" name="decision" value="tu_choi">Từ chối</button>
                </div>
              </form>
            <?php } elseif ($isOwnerRequestViewer && ($selectedRequest['trangThaiYeuCau'] ?? 'cho_duyet') === 'cho_duyet') { ?>
              <div class="request-resolution">
                <span>Trạng thái hiện tại</span>
                <p>Yêu cầu của bạn đang chờ admin xem xét. Sau khi duyệt, gian hàng mới sẽ được tạo từ thông tin này.</p>
              </div>
            <?php } else { ?>
              <div class="request-resolution">
                <span>Kết quả xử lý</span>
                <p>
                  <?php if (($selectedRequest['trangThaiYeuCau'] ?? '') === 'da_duyet' || ($selectedRequest['trangThaiYeuCau'] ?? '') === 'cho_thanh_toan') { ?>
                    Yêu cầu đã được phê duyệt và tạo gian hàng.
                  <?php } else { ?>
                    Yêu cầu đã bị từ chối.
                  <?php } ?>
                </p>
                <div class="resolution-meta">
                  <div>
                    <span>Thời gian xử lý</span>
                    <strong><?php echo htmlspecialchars(request_format_datetime($selectedRequest['thoiGianXuLy'] ?? null), ENT_QUOTES, 'UTF-8'); ?></strong>
                  </div>
                  <div>
                    <span>Tọa độ đã lưu</span>
                    <strong>
                      <?php if (isset($selectedRequest['lat'], $selectedRequest['lon']) && $selectedRequest['lat'] !== null && $selectedRequest['lon'] !== null) { ?>
                        <?php echo htmlspecialchars((string) $selectedRequest['lat'], ENT_QUOTES, 'UTF-8'); ?> / <?php echo htmlspecialchars((string) $selectedRequest['lon'], ENT_QUOTES, 'UTF-8'); ?>
                      <?php } else { ?>
                        Chưa có
                      <?php } ?>
                    </strong>
                  </div>
                </div>

                <?php if (!empty($selectedRequest['idGianHang'])) { ?>
                  <a class="inline-link" href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=branchdetail2&idGianHang=' . (int) $selectedRequest['idGianHang']), ENT_QUOTES, 'UTF-8'); ?>">Xem gian hàng đã tạo #<?php echo (int) $selectedRequest['idGianHang']; ?></a>
                  <?php if ($isOwnerRequestViewer && ($selectedRequest['trangThaiYeuCau'] ?? '') === 'cho_thanh_toan') { ?>
                    <div style="margin-top: 16px;">
                      <a class="primary-btn approve" style="display: inline-flex; align-items: center; gap: 8px; text-decoration: none;" href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=invoice&status=chua_thanh_toan'), ENT_QUOTES, 'UTF-8'); ?>">
                        <i class="fa-solid fa-file-invoice-dollar"></i> Thanh toán hoá đơn ngay
                      </a>
                    </div>
                  <?php } ?>
                <?php } ?>
              </div>
            <?php } ?>
          </div>
        <?php } ?>
      </aside>
    </div>
  </section>
</main>
