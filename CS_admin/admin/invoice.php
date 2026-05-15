<?php
$auth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($auth['idTaiKhoan']) ? (int) $auth['idTaiKhoan'] : 0;
$isOwnerInvoiceViewer = isset($auth['loaiTaiKhoan']) && $auth['loaiTaiKhoan'] === 'chu_quan_ly';
$statusFilter = isset($_GET['status']) ? strtolower(trim((string) $_GET['status'])) : 'all';
$selectedInvoiceId = isset($_GET['selected']) ? (int) $_GET['selected'] : 0;
$searchTerm = isset($_GET['q']) ? trim((string) $_GET['q']) : '';
$invoiceError = '';

if (!in_array($statusFilter, array('all', 'chua_thanh_toan', 'da_thanh_toan', 'qua_han', 'da_huy'), true)) {
    $statusFilter = 'all';
}

function invoice_page_url($statusFilter, $selectedInvoiceId = 0, $searchTerm = '')
{
    $params = array('usecase' => 'invoice');

    if ($statusFilter !== 'all') {
        $params['status'] = $statusFilter;
    }

    if ($selectedInvoiceId > 0) {
        $params['selected'] = $selectedInvoiceId;
    }

    $searchTerm = trim((string) $searchTerm);
    if ($searchTerm !== '') {
        $params['q'] = $searchTerm;
    }

    return admin_url('index1st.php?' . http_build_query($params));
}

function invoice_payment_url($invoiceId)
{
    return admin_url('index1st.php?' . http_build_query(array(
        'usecase' => 'invoice-payment',
        'idHoaDonGianHang' => (int) $invoiceId,
    )));
}

function invoice_status_meta($status)
{
    $status = strtolower(trim((string) $status));

    switch ($status) {
        case 'da_thanh_toan':
            return array('label' => 'Đã thanh toán', 'class' => 'approved');
        case 'qua_han':
            return array('label' => 'Quá hạn', 'class' => 'rejected');
        case 'da_huy':
            return array('label' => 'Đã hủy', 'class' => 'rejected');
        default:
            return array('label' => 'Chưa thanh toán', 'class' => 'pending');
    }
}

function invoice_format_datetime($value)
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

function invoice_format_date($value)
{
    if (empty($value)) {
        return 'Chưa có';
    }

    $timestamp = strtotime((string) $value);
    if ($timestamp === false) {
        return 'Chưa có';
    }

    return date('d/m/Y', $timestamp);
}

function invoice_money($value)
{
    return number_format((float) $value, 0, ',', '.') . 'đ';
}

function invoice_display_text($value, $emptyText = 'Chưa có')
{
    $value = trim((string) $value);
    return $value !== '' ? $value : $emptyText;
}

function invoice_search_normalize($value)
{
    $text = trim((string) $value);
    if (function_exists('mb_strtolower')) {
        $text = mb_strtolower($text, 'UTF-8');
    } else {
        $text = strtolower($text);
    }

    $ascii = function_exists('iconv') ? @iconv('UTF-8', 'ASCII//TRANSLIT//IGNORE', $text) : false;
    if ($ascii !== false && $ascii !== '') {
        $text = strtolower($ascii);
    }

    return $text;
}

function invoice_matches_search($invoiceItem, $query)
{
    $query = trim((string) $query);
    if ($query === '') {
        return true;
    }

    $needle = invoice_search_normalize($query);
    $invoiceId = isset($invoiceItem['idHoaDonGianHang']) ? (int) $invoiceItem['idHoaDonGianHang'] : 0;
    $haystack = array(
        'HDGH' . str_pad((string) $invoiceId, 4, '0', STR_PAD_LEFT),
        (string) $invoiceId,
        $invoiceItem['tenGianHang'] ?? '',
        $invoiceItem['hoTenChuQuanLy'] ?? '',
        $invoiceItem['usernameChuQuanLy'] ?? '',
        $invoiceItem['emailChuQuanLy'] ?? '',
        $invoiceItem['ghiChu'] ?? '',
    );

    foreach ($haystack as $value) {
        if (strpos(invoice_search_normalize($value), $needle) !== false) {
            return true;
        }
    }

    return false;
}

$apiError = '';
$apiHttpCode = 0;
$query = array('idTaiKhoan' => $idTaiKhoan, 'ownerOnly' => $isOwnerInvoiceViewer ? 'true' : 'false');
$allInvoices = $idTaiKhoan > 0
    ? admin_api_call('GET', 'Admin/invoices', null, $apiError, $apiHttpCode, $query)
    : null;

if (!is_array($allInvoices)) {
    $allInvoices = array();
    $invoiceError = $apiError !== '' ? $apiError : 'Không thể tải danh sách hóa đơn: backend API chưa sẵn sàng.';
}

$counts = array(
    'all' => count($allInvoices),
    'chua_thanh_toan' => 0,
    'da_thanh_toan' => 0,
    'qua_han' => 0,
    'da_huy' => 0,
);

foreach ($allInvoices as $invoiceItem) {
    $status = isset($invoiceItem['trangThai']) ? (string) $invoiceItem['trangThai'] : 'chua_thanh_toan';
    if (isset($counts[$status])) {
        $counts[$status]++;
    }
}

$filteredInvoices = array_values(array_filter($allInvoices, function ($invoiceItem) use ($statusFilter, $searchTerm) {
    if ($statusFilter !== 'all' && (!isset($invoiceItem['trangThai']) || $invoiceItem['trangThai'] !== $statusFilter)) {
        return false;
    }

    return invoice_matches_search($invoiceItem, $searchTerm);
}));

$selectedInvoice = null;
foreach ($filteredInvoices as $invoiceItem) {
    if ((int) ($invoiceItem['idHoaDonGianHang'] ?? 0) === $selectedInvoiceId) {
        $selectedInvoice = $invoiceItem;
        break;
    }
}

if ($selectedInvoice === null && count($filteredInvoices) > 0) {
    $selectedInvoice = $filteredInvoices[0];
    $selectedInvoiceId = (int) ($selectedInvoice['idHoaDonGianHang'] ?? 0);
}
?>
<main class="main-content">
  <section class="request-page">
    <div class="page-head">
      <div>
        <h2>Hóa đơn</h2>
        <p><?php echo $isOwnerInvoiceViewer ? 'Theo dõi các hóa đơn phí duy trì gian hàng và trạng thái thanh toán của bạn.' : 'Quản lý hóa đơn phí duy trì của các gian hàng trong hệ thống.'; ?></p>
      </div>
    </div>

    <?php if ($invoiceError !== '') { ?>
      <div class="request-alert error"><?php echo htmlspecialchars($invoiceError, ENT_QUOTES, 'UTF-8'); ?></div>
    <?php } ?>

    <div class="request-layout">
      <div>
        <form class="request-search-form" method="get" action="<?php echo htmlspecialchars(admin_url('index1st.php'), ENT_QUOTES, 'UTF-8'); ?>">
          <input type="hidden" name="usecase" value="invoice" />
          <?php if ($statusFilter !== 'all') { ?>
          <input type="hidden" name="status" value="<?php echo htmlspecialchars($statusFilter, ENT_QUOTES, 'UTF-8'); ?>" />
          <?php } ?>
          <i class="fa-solid fa-magnifying-glass"></i>
          <input type="search" name="q" value="<?php echo htmlspecialchars($searchTerm, ENT_QUOTES, 'UTF-8'); ?>" placeholder="Tìm theo mã hóa đơn, gian hàng, chủ quản lý..." />
          <button type="submit" aria-label="Tìm kiếm"><i class="fa-solid fa-arrow-right"></i></button>
          <?php if ($searchTerm !== '') { ?>
          <a href="<?php echo htmlspecialchars(invoice_page_url($statusFilter, 0), ENT_QUOTES, 'UTF-8'); ?>" aria-label="Xóa tìm kiếm"><i class="fa-solid fa-xmark"></i></a>
          <?php } ?>
        </form>

        <div class="request-tabs">
          <a class="request-tab<?php echo $statusFilter === 'all' ? ' active' : ''; ?>" href="<?php echo htmlspecialchars(invoice_page_url('all', $selectedInvoiceId, $searchTerm), ENT_QUOTES, 'UTF-8'); ?>">Tất cả (<?php echo (int) $counts['all']; ?>)</a>
          <a class="request-tab<?php echo $statusFilter === 'chua_thanh_toan' ? ' active' : ''; ?>" href="<?php echo htmlspecialchars(invoice_page_url('chua_thanh_toan', $selectedInvoiceId, $searchTerm), ENT_QUOTES, 'UTF-8'); ?>">Chưa thanh toán (<?php echo (int) $counts['chua_thanh_toan']; ?>)</a>
          <a class="request-tab<?php echo $statusFilter === 'da_thanh_toan' ? ' active' : ''; ?>" href="<?php echo htmlspecialchars(invoice_page_url('da_thanh_toan', $selectedInvoiceId, $searchTerm), ENT_QUOTES, 'UTF-8'); ?>">Đã thanh toán (<?php echo (int) $counts['da_thanh_toan']; ?>)</a>
          <a class="request-tab<?php echo $statusFilter === 'qua_han' ? ' active' : ''; ?>" href="<?php echo htmlspecialchars(invoice_page_url('qua_han', $selectedInvoiceId, $searchTerm), ENT_QUOTES, 'UTF-8'); ?>">Quá hạn (<?php echo (int) $counts['qua_han']; ?>)</a>
          <a class="request-tab<?php echo $statusFilter === 'da_huy' ? ' active' : ''; ?>" href="<?php echo htmlspecialchars(invoice_page_url('da_huy', $selectedInvoiceId, $searchTerm), ENT_QUOTES, 'UTF-8'); ?>">Đã hủy (<?php echo (int) $counts['da_huy']; ?>)</a>
        </div>

        <div class="request-table-panel">
          <table>
            <thead>
              <tr>
                <th>HÓA ĐƠN</th>
                <th>GIAN HÀNG</th>
                <th>NGÀY TẠO</th>
                <th>HẠN THANH TOÁN</th>
                <th>TRẠNG THÁI</th>
              </tr>
            </thead>
            <tbody>
              <?php if (count($filteredInvoices) === 0) { ?>
                <tr>
                  <td colspan="5" class="empty-cell">Chưa có hóa đơn nào ở bộ lọc hiện tại.</td>
                </tr>
              <?php } ?>

              <?php foreach ($filteredInvoices as $invoiceItem) { ?>
                <?php
                $itemId = (int) ($invoiceItem['idHoaDonGianHang'] ?? 0);
                $statusMeta = invoice_status_meta($invoiceItem['trangThai'] ?? 'chua_thanh_toan');
                ?>
                <tr class="<?php echo $selectedInvoiceId === $itemId ? 'row-active' : ''; ?>">
                  <td>
                    <a class="request-link" href="<?php echo htmlspecialchars(invoice_page_url($statusFilter, $itemId, $searchTerm), ENT_QUOTES, 'UTF-8'); ?>">
                      <strong>#HDGH-<?php echo str_pad((string) $itemId, 4, '0', STR_PAD_LEFT); ?></strong>
                      <span><?php echo htmlspecialchars(invoice_money($invoiceItem['tongTien'] ?? 0), ENT_QUOTES, 'UTF-8'); ?></span>
                    </a>
                  </td>
                  <td>
                    <div class="table-owner">
                      <strong><?php echo htmlspecialchars(invoice_display_text($invoiceItem['tenGianHang'] ?? '', 'Gian hàng'), ENT_QUOTES, 'UTF-8'); ?></strong>
                      <span>ID gian hàng #<?php echo (int) ($invoiceItem['idGianHang'] ?? 0); ?></span>
                    </div>
                  </td>
                  <td><?php echo htmlspecialchars(invoice_format_datetime($invoiceItem['ngayTao'] ?? null), ENT_QUOTES, 'UTF-8'); ?></td>
                  <td><?php echo htmlspecialchars(invoice_format_date($invoiceItem['ngayHetHan'] ?? null), ENT_QUOTES, 'UTF-8'); ?></td>
                  <td><span class="status-badge <?php echo htmlspecialchars($statusMeta['class'], ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($statusMeta['label'], ENT_QUOTES, 'UTF-8'); ?></span></td>
                </tr>
              <?php } ?>
            </tbody>
          </table>
        </div>

        <p class="request-summary">Hiển thị <?php echo count($filteredInvoices); ?> trên tổng <?php echo count($allInvoices); ?> hóa đơn.</p>
      </div>

      <aside class="request-panel">
        <?php if ($selectedInvoice === null) { ?>
          <div class="request-empty-panel">
            <h3>Chưa có dữ liệu</h3>
            <p>Chọn một hóa đơn từ danh sách bên trái để xem chi tiết.</p>
          </div>
        <?php } else { ?>
          <?php
          $selectedStatus = invoice_status_meta($selectedInvoice['trangThai'] ?? 'chua_thanh_toan');
          $selectedOwnerName = invoice_display_text($selectedInvoice['hoTenChuQuanLy'] ?? ($selectedInvoice['usernameChuQuanLy'] ?? ''), 'Chủ quản lý');
          ?>
          <div class="request-panel-card">
            <div class="request-panel-head">
              <div>
                <h3>Chi tiết hóa đơn</h3>
                <p>Thông tin phí duy trì gian hàng được lấy từ bảng hoadongianhang.</p>
              </div>
              <span class="status-badge <?php echo htmlspecialchars($selectedStatus['class'], ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($selectedStatus['label'], ENT_QUOTES, 'UTF-8'); ?></span>
            </div>

            <div class="request-highlight">
              <strong>#HDGH-<?php echo str_pad((string) (int) ($selectedInvoice['idHoaDonGianHang'] ?? 0), 4, '0', STR_PAD_LEFT); ?></strong>
              <span><?php echo htmlspecialchars(invoice_money($selectedInvoice['tongTien'] ?? 0), ENT_QUOTES, 'UTF-8'); ?></span>
            </div>

            <div class="request-detail-grid">
              <div>
                <span>Gian hàng</span>
                <strong><?php echo htmlspecialchars(invoice_display_text($selectedInvoice['tenGianHang'] ?? '', 'Gian hàng'), ENT_QUOTES, 'UTF-8'); ?></strong>
                <small>ID gian hàng #<?php echo (int) ($selectedInvoice['idGianHang'] ?? 0); ?></small>
              </div>
              <div>
                <span>Chủ quản lý</span>
                <strong><?php echo htmlspecialchars($selectedOwnerName, ENT_QUOTES, 'UTF-8'); ?></strong>
                <small><?php echo htmlspecialchars(invoice_display_text($selectedInvoice['emailChuQuanLy'] ?? '', 'Chưa có email'), ENT_QUOTES, 'UTF-8'); ?></small>
              </div>
              <div>
                <span>Ngày tạo</span>
                <strong><?php echo htmlspecialchars(invoice_format_datetime($selectedInvoice['ngayTao'] ?? null), ENT_QUOTES, 'UTF-8'); ?></strong>
                <small>Hóa đơn gian hàng #<?php echo (int) ($selectedInvoice['idHoaDonGianHang'] ?? 0); ?></small>
              </div>
              <div>
                <span>Hạn thanh toán</span>
                <strong><?php echo htmlspecialchars(invoice_format_datetime($selectedInvoice['ngayHetHan'] ?? null), ENT_QUOTES, 'UTF-8'); ?></strong>
                <small>Phí tháng hiện tại: <?php echo htmlspecialchars(invoice_money($selectedInvoice['phiHangThang'] ?? 0), ENT_QUOTES, 'UTF-8'); ?></small>
              </div>
            </div>

            <div class="request-description">
              <span>Ghi chú</span>
              <p><?php echo nl2br(htmlspecialchars(invoice_display_text($selectedInvoice['ghiChu'] ?? '', 'Chưa có ghi chú cho hóa đơn này.'), ENT_QUOTES, 'UTF-8')); ?></p>
            </div>

            <div class="request-resolution">
              <span>Trạng thái hiện tại</span>
              <p>
                <?php if (($selectedInvoice['trangThai'] ?? '') === 'da_thanh_toan') { ?>
                  Hóa đơn này đã được ghi nhận thanh toán.
                <?php } elseif (($selectedInvoice['trangThai'] ?? '') === 'qua_han') { ?>
                  Hóa đơn đã quá hạn thanh toán.
                <?php } elseif (($selectedInvoice['trangThai'] ?? '') === 'da_huy') { ?>
                  Hóa đơn đã được hủy.
                <?php } else { ?>
                  Hóa đơn đang chờ thanh toán.
                <?php } ?>
              </p>

              <div class="resolution-meta">
                <div>
                  <span>Địa chỉ gian hàng</span>
                  <strong><?php echo htmlspecialchars(invoice_display_text($selectedInvoice['diaChi'] ?? '', 'Chưa cập nhật'), ENT_QUOTES, 'UTF-8'); ?></strong>
                </div>
                <div>
                  <span>Tổng tiền</span>
                  <strong><?php echo htmlspecialchars(invoice_money($selectedInvoice['tongTien'] ?? 0), ENT_QUOTES, 'UTF-8'); ?></strong>
                </div>
              </div>

              <?php if ($isOwnerInvoiceViewer && in_array(($selectedInvoice['trangThai'] ?? ''), array('chua_thanh_toan', 'qua_han'), true)) { ?>
                <a class="primary-btn approve" href="<?php echo htmlspecialchars(invoice_payment_url((int) ($selectedInvoice['idHoaDonGianHang'] ?? 0)), ENT_QUOTES, 'UTF-8'); ?>">
                  <i class="fa-solid fa-qrcode"></i>
                  <span>Thanh toán</span>
                </a>
              <?php } elseif (!$isOwnerInvoiceViewer) { ?>
                <div class="request-owner-note">
                  <span>Thanh toán</span>
                  <p>Chỉ chủ gian hàng mới có thể mở QR thanh toán cho hóa đơn này.</p>
                </div>
              <?php } ?>
            </div>
          </div>
        <?php } ?>
      </aside>
    </div>
  </section>
</main>
