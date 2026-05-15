<?php
$auth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($auth['idTaiKhoan']) ? (int) $auth['idTaiKhoan'] : 0;
$loaiTaiKhoan = isset($auth['loaiTaiKhoan']) ? (string) $auth['loaiTaiKhoan'] : 'admin';
$flash = isset($_GET['flash']) ? (string) $_GET['flash'] : '';
$searchTerm = isset($_GET['q']) ? trim((string) $_GET['q']) : '';
$storePageAlert = null;

if ($flash === 'request_sent') {
    $storePageAlert = array(
        'type' => 'success',
        'text' => 'Đã gửi yêu cầu mở gian hàng mới. Admin sẽ xem xét trong mục Yêu cầu trước khi tạo gian hàng.'
    );
}

function store_default_cards()
{
    return array(
        array('idGianHang' => 1, 'ten' => 'Bếp Việt Delights', 'chuQuanLy' => 'Trần Văn Anh', 'tinhTrang' => 'hoat_dong', 'banner' => 'banner-blue', 'logoClass' => 'dark', 'logo' => 'BV', 'leftLabel' => 'DOANH THU', 'leftValue' => '1.250.000.000đ', 'rightLabel' => 'SẢN PHẨM', 'rightValue' => '142 món', 'actionStyle' => 'contact', 'actionLabel' => 'Liên hệ'),
        array('idGianHang' => 2, 'ten' => 'Neo Fashion Hub', 'chuQuanLy' => 'Nguyễn Thị Lan', 'tinhTrang' => 'tam_dung', 'banner' => 'banner-peach', 'logoClass' => 'dark', 'logo' => 'NF', 'leftLabel' => 'DOANH THU', 'leftValue' => '845.200.000đ', 'rightLabel' => 'SẢN PHẨM', 'rightValue' => '2,105 mẫu', 'actionStyle' => 'primary', 'actionLabel' => 'Kích hoạt'),
        array('idGianHang' => 3, 'ten' => 'TechWorld Mart', 'chuQuanLy' => 'Lê Hoàng Nam', 'tinhTrang' => 'cho_duyet', 'banner' => 'banner-purple', 'logoClass' => 'olive', 'logo' => 'TW', 'leftLabel' => 'DOANH THU', 'leftValue' => '0đ', 'rightLabel' => 'SẢN PHẨM', 'rightValue' => '54 món', 'actionStyle' => 'dual', 'actionLabel' => ''),
        array('idGianHang' => 4, 'ten' => 'HomeSweet Home', 'chuQuanLy' => 'Phạm Minh Tú', 'tinhTrang' => 'hoat_dong', 'banner' => 'banner-mint', 'logoClass' => 'dark', 'logo' => 'HS', 'leftLabel' => 'DOANH THU', 'leftValue' => '412.000.000đ', 'rightLabel' => 'SẢN PHẨM', 'rightValue' => '682 SKU', 'actionStyle' => 'contact', 'actionLabel' => 'Liên hệ'),
        array('idGianHang' => 5, 'ten' => 'Glow Beauty Spa', 'chuQuanLy' => 'Vũ Mỹ Linh', 'tinhTrang' => 'hoat_dong', 'banner' => 'banner-pink', 'logoClass' => 'cream', 'logo' => 'GB', 'leftLabel' => 'DOANH THU', 'leftValue' => '2.140.500.000đ', 'rightLabel' => 'SẢN PHẨM', 'rightValue' => '125 dịch vụ', 'actionStyle' => 'contact', 'actionLabel' => 'Liên hệ'),
    );
}

function store_api_url($role, $idTaiKhoan)
{
    $endpoint = $role === 'chu_quan_ly' ? 'Owner/stores' : 'Admin/stores';
    return backend_api_url($endpoint) . '?idTaiKhoan=' . urlencode((string) $idTaiKhoan);
}

function store_detail_api_url($role, $idTaiKhoan, $idGianHang)
{
    $endpoint = $role === 'chu_quan_ly' ? 'Owner/stores/' : 'Admin/stores/';
    return backend_api_url($endpoint . rawurlencode((string) $idGianHang)) . '?idTaiKhoan=' . rawurlencode((string) $idTaiKhoan) . '&lang=vi';
}

function store_image_url($path)
{
    $path = trim((string) $path);
    if ($path === '') {
        return '';
    }

    return admin_url('api/image-proxy.php') . '?path=' . rawurlencode($path);
}

function store_fetch_image_path_from_detail($role, $idTaiKhoan, $idGianHang)
{
    if ($idTaiKhoan <= 0 || $idGianHang <= 0) {
        return '';
    }

    $ch = curl_init(store_detail_api_url($role, $idTaiKhoan, $idGianHang));
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_HTTPHEADER, array('Accept: application/json'));
    curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
    curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, false);

    $responseBody = curl_exec($ch);
    $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);

    if ($responseBody === false || $httpCode >= 400) {
        return '';
    }

    $decoded = json_decode($responseBody, true);
    if (!is_array($decoded) || empty($decoded['hinhAnh'])) {
        return '';
    }

    return (string) $decoded['hinhAnh'];
}

function store_status_meta($status)
{
    $status = strtolower(trim((string) $status));

    switch ($status) {
        case 'dang_hoat_dong':
        case 'hoat_dong':
            return array('label' => 'ĐANG HOẠT ĐỘNG', 'class' => 'green', 'banner' => 'banner-blue');
        case 'tam_ngung':
        case 'tam_dung':
            return array('label' => 'TẠM DỪNG', 'class' => 'yellow', 'banner' => 'banner-peach');
        case 'dong_cua':
        case 'cho_duyet':
            return array('label' => 'CHỜ DUYỆT', 'class' => 'blue', 'banner' => 'banner-purple');
        default:
            return array('label' => 'KHÔNG RÕ', 'class' => 'blue', 'banner' => 'banner-mint');
    }
}

function store_logo_class($index)
{
    $classes = array('dark', 'olive', 'cream');
    return $classes[$index % count($classes)];
}

function store_initials($name)
{
    $name = trim((string) $name);
    if ($name === '') {
        return 'GH';
    }

    $parts = preg_split('/\s+/u', $name);
    $letters = '';
    foreach ($parts as $part) {
        if ($part === '') {
            continue;
        }
        $letters .= function_exists('mb_substr') ? mb_substr($part, 0, 1, 'UTF-8') : substr($part, 0, 1);
        if ((function_exists('mb_strlen') ? mb_strlen($letters, 'UTF-8') : strlen($letters)) >= 2) {
            break;
        }
    }

    return function_exists('mb_strtoupper') ? mb_strtoupper($letters, 'UTF-8') : strtoupper($letters);
}

function store_owner_name($store, $role, $auth)
{
    if ($role === 'chu_quan_ly') {
        if (!empty($auth['hoTen'])) {
            return (string) $auth['hoTen'];
        }
        if (!empty($auth['username'])) {
            return (string) $auth['username'];
        }
        return 'Chủ gian hàng';
    }

    if (!empty($store['tenChuQuanLy'])) {
        return (string) $store['tenChuQuanLy'];
    }
    if (!empty($store['usernameChuQuanLy'])) {
        return (string) $store['usernameChuQuanLy'];
    }
    if (!empty($store['emailChuQuanLy'])) {
        return (string) $store['emailChuQuanLy'];
    }
    return 'Chưa gán chủ gian hàng';
}

function store_format_visit_count($value)
{
    return number_format(max(0, (int) $value), 0, ',', '.');
}

function fetch_real_stores($role, $idTaiKhoan, $auth, &$error)
{
    if ($idTaiKhoan <= 0) {
        $error = 'Thiếu thông tin đăng nhập để tải dữ liệu gian hàng.';
        return array();
    }

    $ch = curl_init(store_api_url($role, $idTaiKhoan));
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_HTTPHEADER, array('Accept: application/json'));
    curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
    curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, false);

    $responseBody = curl_exec($ch);
    $curlError = curl_error($ch);
    $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);

    if ($responseBody === false) {
        $error = $curlError !== '' ? $curlError : 'Không thể kết nối API gian hàng.';
        return array();
    }

    $decoded = json_decode($responseBody, true);
    if (!is_array($decoded)) {
        $error = 'Dữ liệu API gian hàng không hợp lệ.';
        return array();
    }

    if ($httpCode >= 400) {
        $error = isset($decoded['message']) ? (string) $decoded['message'] : ('API trả về HTTP ' . $httpCode . '.');
        return array();
    }

    $cards = array();
    foreach ($decoded as $index => $store) {
        if (!is_array($store)) {
            continue;
        }

        $status = store_status_meta(isset($store['tinhTrang']) ? $store['tinhTrang'] : '');
        $ownerName = store_owner_name($store, $role, $auth);
        $imagePath = !empty($store['hinhAnh']) ? (string) $store['hinhAnh'] : '';
        if ($imagePath === '' && !empty($store['idGianHang'])) {
            $imagePath = store_fetch_image_path_from_detail($role, $idTaiKhoan, (int) $store['idGianHang']);
        }
        $address = !empty($store['diaChi']) ? (string) $store['diaChi'] : 'Chưa cập nhật địa chỉ';

        $visitCount = isset($store['luotTruyCap']) ? (int) $store['luotTruyCap'] : 0;

        $cards[] = array(
            'idGianHang' => isset($store['idGianHang']) ? (int) $store['idGianHang'] : 0,
            'ten' => isset($store['ten']) ? (string) $store['ten'] : 'Gian hàng',
            'chuQuanLy' => $ownerName,
            'banner' => $status['banner'],
            'imageUrl' => $imagePath !== '' ? store_image_url($imagePath) : '',
            'logoClass' => store_logo_class($index),
            'logo' => store_initials(isset($store['ten']) ? $store['ten'] : ''),
            'leftLabel' => 'ĐỊA CHỈ',
            'leftValue' => $address,
            'rightLabel' => 'Lượt visit',
            'rightValue' => store_format_visit_count($visitCount),
            'actionStyle' => 'contact',
            'actionLabel' => 'ID #' . (isset($store['idGianHang']) ? (int) $store['idGianHang'] : 0),
            'statusLabel' => $status['label'],
            'statusClass' => $status['class'],
        );
    }

    return $cards;
}

function store_search_normalize($value)
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

function store_filter_cards_by_name($cards, $query)
{
    $query = trim((string) $query);
    if ($query === '') {
        return $cards;
    }

    $normalizedQuery = store_search_normalize($query);

    return array_values(array_filter($cards, function ($card) use ($normalizedQuery) {
        $name = isset($card['ten']) ? (string) $card['ten'] : '';
        return strpos(store_search_normalize($name), $normalizedQuery) !== false;
    }));
}

$storeError = '';
$allCards = fetch_real_stores($loaiTaiKhoan, $idTaiKhoan, $auth, $storeError);
$cards = store_filter_cards_by_name($allCards, $searchTerm);
$totalCards = count($allCards);
?>
<main class="main-content">
  <section class="booth-page">
    <div class="page-title-row">
      <div>
        <h2>Danh sách Gian hàng Quản lý</h2>
        <p>Theo dõi, kiểm soát và tối ưu hoạt động của các đối tác kinh doanh.</p>
      </div>

      <button class="primary-btn" type="button" onclick="window.location.href='<?php echo htmlspecialchars(admin_url('index1st.php?usecase=branchdetail2&mode=create'), ENT_QUOTES, 'UTF-8'); ?>'">
        <i class="fa-solid fa-circle-plus"></i>
        <span>Thêm gian hàng mới</span>
      </button>
    </div>

    <div class="toolbar-panel">
      <div class="toolbar-top">
        <div class="toolbar-search">
          <form class="store-search-inline" method="get" action="<?php echo htmlspecialchars(admin_url('index1st.php'), ENT_QUOTES, 'UTF-8'); ?>">
            <input type="hidden" name="usecase" value="store" />
            <i class="fa-solid fa-magnifying-glass"></i>
            <input type="search" name="q" value="<?php echo htmlspecialchars($searchTerm, ENT_QUOTES, 'UTF-8'); ?>" placeholder="Tìm kiếm theo tên gian hàng..." />
            <button type="submit" aria-label="Tim kiem"><i class="fa-solid fa-arrow-right"></i></button>
            <?php if ($searchTerm !== '') { ?>
            <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=store'), ENT_QUOTES, 'UTF-8'); ?>" aria-label="Xoa tim kiem"><i class="fa-solid fa-xmark"></i></a>
            <?php } ?>
          </form>
        </div>

      </div>
    </div>

    <?php if ($storeError !== '') { ?>
      <div class="store-debug-panel">
        Không tải được dữ liệu gian hàng.
        <span><?php echo htmlspecialchars($storeError, ENT_QUOTES, 'UTF-8'); ?></span>
      </div>
    <?php } ?>

    <?php if ($storePageAlert !== null) { ?>
      <div class="store-debug-panel <?php echo htmlspecialchars($storePageAlert['type'], ENT_QUOTES, 'UTF-8'); ?>">
        <?php echo htmlspecialchars($storePageAlert['text'], ENT_QUOTES, 'UTF-8'); ?>
      </div>
    <?php } ?>

    <div class="booth-grid">
      <?php foreach ($cards as $card) { ?>
      <?php
      $bannerStyle = '';
      if (!empty($card['imageUrl'])) {
          $escapedImageUrl = htmlspecialchars($card['imageUrl'], ENT_QUOTES, 'UTF-8');
          $bannerStyle = "background-image:linear-gradient(rgba(15,23,42,.16), rgba(15,23,42,.16)), url('{$escapedImageUrl}');";
      }
      ?>
      <div class="booth-card">
        <div class="booth-banner <?php echo htmlspecialchars($card['banner'], ENT_QUOTES, 'UTF-8'); ?><?php echo !empty($card['imageUrl']) ? ' has-image' : ''; ?>"<?php echo $bannerStyle !== '' ? ' style="' . $bannerStyle . '"' : ''; ?>>
          <span class="state-badge <?php echo htmlspecialchars(isset($card['statusClass']) ? $card['statusClass'] : 'blue', ENT_QUOTES, 'UTF-8'); ?>">
            <?php echo htmlspecialchars(isset($card['statusLabel']) ? $card['statusLabel'] : 'KHÔNG RÕ', ENT_QUOTES, 'UTF-8'); ?>
          </span>
        </div>
        <div class="booth-body">
          <div class="booth-logo <?php echo htmlspecialchars($card['logoClass'], ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($card['logo'], ENT_QUOTES, 'UTF-8'); ?></div>

          <div class="booth-title-row">
            <div>
              <h3><?php echo htmlspecialchars($card['ten'], ENT_QUOTES, 'UTF-8'); ?></h3>
              <p><i class="fa-solid fa-user"></i> <?php echo htmlspecialchars($card['chuQuanLy'], ENT_QUOTES, 'UTF-8'); ?></p>
            </div>
            <button class="more-btn"><i class="fa-solid fa-ellipsis-vertical"></i></button>
          </div>

          <div class="booth-stats">
            <div>
              <span><?php echo htmlspecialchars($card['leftLabel'], ENT_QUOTES, 'UTF-8'); ?></span>
              <strong class="stat-multiline"><?php echo htmlspecialchars($card['leftValue'], ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <div>
              <span><?php echo htmlspecialchars($card['rightLabel'], ENT_QUOTES, 'UTF-8'); ?></span>
              <strong class="stat-multiline"><?php echo htmlspecialchars($card['rightValue'], ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
          </div>

          <?php if ($card['actionStyle'] === 'dual') { ?>
          <div class="booth-actions dual">
            <button class="approve-btn">Duyệt ngay</button>
            <button class="reject-btn">Từ chối</button>
          </div>
          <?php } elseif ($card['actionStyle'] === 'primary') { ?>
          <div class="booth-actions">
            <button class="ghost-btn" type="button" onclick="window.location.href='<?php echo htmlspecialchars(admin_url('index1st.php?usecase=branchdetail2&idGianHang=' . (int) $card['idGianHang']), ENT_QUOTES, 'UTF-8'); ?>'">Chi tiết</button>
            <button class="primary-small"><?php echo htmlspecialchars($card['actionLabel'], ENT_QUOTES, 'UTF-8'); ?></button>
          </div>
          <?php } else { ?>
          <div class="booth-actions">
            <button class="ghost-btn" type="button" onclick="window.location.href='<?php echo htmlspecialchars(admin_url('index1st.php?usecase=branchdetail2&idGianHang=' . (int) $card['idGianHang']), ENT_QUOTES, 'UTF-8'); ?>'">Chi tiết</button>
            <button class="soft-btn"><?php echo htmlspecialchars($card['actionLabel'], ENT_QUOTES, 'UTF-8'); ?></button>
          </div>
          <?php } ?>
        </div>
      </div>
      <?php } ?>

      <?php if (count($cards) === 0 && $storeError === '' && $searchTerm !== '') { ?>
      <div class="add-card search-empty-card">
        <div class="add-circle"><i class="fa-solid fa-magnifying-glass"></i></div>
        <h3>Không tìm thấy gian hàng</h3>
        <p>Không có gian hàng nào khớp với "<?php echo htmlspecialchars($searchTerm, ENT_QUOTES, 'UTF-8'); ?>".</p>
      </div>
      <?php } elseif (count($cards) === 0 && $storeError === '') { ?>
      <div class="add-card" onclick="window.location.href='<?php echo htmlspecialchars(admin_url('index1st.php?usecase=branchdetail2&mode=create'), ENT_QUOTES, 'UTF-8'); ?>'">
        <div class="add-circle"><i class="fa-solid fa-plus"></i></div>
        <h3>Chưa có gian hàng</h3>
        <p><?php echo $loaiTaiKhoan === 'chu_quan_ly' ? 'Gửi yêu cầu mở gian hàng mới để admin duyệt.' : 'Thêm gian hàng mới cho hệ thống.'; ?></p>
      </div>
      <?php } ?>
    </div>

    <div class="bottom-row">
      <p class="store-count-summary">Hiển thị <?php echo count($cards); ?> trên tổng số <?php echo $totalCards; ?> gian hàng</p>
      <div class="pagination">
        <button><i class="fa-solid fa-chevron-left"></i></button>
        <button class="active">1</button>
        <button><i class="fa-solid fa-chevron-right"></i></button>
      </div>
    </div>
  </section>
</main>
