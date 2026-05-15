<?php
$auth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($auth['idTaiKhoan']) ? (int) $auth['idTaiKhoan'] : 0;
$loaiTaiKhoan = isset($auth['loaiTaiKhoan']) ? (string) $auth['loaiTaiKhoan'] : 'admin';
$idGianHang = isset($_GET['idGianHang']) ? (int) $_GET['idGianHang'] : 0;
$isCreateMode = isset($_GET['mode']) && $_GET['mode'] === 'create';
$pageMessage = null;
$pageError = '';
$ownerOptions = array();
$ownerOptionsError = '';

function store_form_collection_url($role, $idTaiKhoan)
{
    $endpoint = $role === 'chu_quan_ly' ? 'Owner/stores' : 'Admin/stores';
    return backend_api_url($endpoint) . '?idTaiKhoan=' . rawurlencode((string) $idTaiKhoan);
}

function store_form_detail_url($role, $idTaiKhoan, $idGianHang)
{
    $endpoint = $role === 'chu_quan_ly' ? 'Owner/stores/' : 'Admin/stores/';
    $languageCode = isset($_GET['lang']) ? store_form_normalize_language($_GET['lang']) : 'vi';
    return backend_api_url($endpoint . rawurlencode((string) $idGianHang)) . '?idTaiKhoan=' . rawurlencode((string) $idTaiKhoan) . '&lang=' . rawurlencode($languageCode);
}

function store_form_image_upload_url($role, $idTaiKhoan, $idGianHang)
{
    $endpoint = $role === 'chu_quan_ly' ? 'Owner/stores/' : 'Admin/stores/';
    return backend_api_url($endpoint . rawurlencode((string) $idGianHang) . '/image') . '?idTaiKhoan=' . rawurlencode((string) $idTaiKhoan);
}

function store_form_owner_options_url($idTaiKhoan)
{
    return backend_api_url('Admin/owners') . '?idTaiKhoan=' . rawurlencode((string) $idTaiKhoan);
}

function store_form_description_url($idGianHang, $idTaiKhoan)
{
    return backend_api_url('GianHang/' . rawurlencode((string) $idGianHang) . '/update-mo-ta') . '?idTaiKhoan=' . rawurlencode((string) $idTaiKhoan);
}

function store_form_request_collection_url($idTaiKhoan)
{
    return backend_api_url('Owner/store-requests') . '?idTaiKhoan=' . rawurlencode((string) $idTaiKhoan);
}

function store_form_food_management_url($idGianHang)
{
    return admin_url('index1st.php?usecase=menu&idGianHang=' . (int) $idGianHang);
}

function store_form_redirect($url)
{
    $url = (string) $url;

    if (!headers_sent()) {
        header('Location: ' . $url);
        exit;
    }

    $safeUrl = htmlspecialchars($url, ENT_QUOTES, 'UTF-8');
    $jsonUrl = json_encode($url, JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_AMP | JSON_HEX_QUOT);
    if ($jsonUrl === false) {
        $jsonUrl = '""';
    }

    echo '<script>window.location.href=' . $jsonUrl . ';</script>';
    echo '<noscript><meta http-equiv="refresh" content="0;url=' . $safeUrl . '"></noscript>';
    exit;
}

function store_form_fetch_daily_visits($idGianHang)
{
    global $idTaiKhoan;

    $idGianHang = (int) $idGianHang;
    $idTaiKhoan = (int) $idTaiKhoan;
    if ($idGianHang <= 0 || $idTaiKhoan <= 0) {
        return array();
    }

    $apiError = '';
    $apiHttpCode = 0;
    $result = admin_api_call(
        'GET',
        'Admin/stores/' . rawurlencode((string) $idGianHang) . '/daily-visits',
        null,
        $apiError,
        $apiHttpCode,
        array('idTaiKhoan' => $idTaiKhoan)
    );

    return is_array($result) ? $result : array();
}

function store_form_heatmap_level($count, $maxCount)
{
    $count = (int) $count;
    $maxCount = (int) $maxCount;
    if ($count <= 0 || $maxCount <= 0) {
        return 0;
    }

    $ratio = $count / $maxCount;
    if ($ratio <= 0.25) {
        return 1;
    }
    if ($ratio <= 0.5) {
        return 2;
    }
    if ($ratio <= 0.75) {
        return 3;
    }

    return 4;
}

function store_form_render_heatmap($dailyVisits)
{
    $dailyVisits = is_array($dailyVisits) ? $dailyVisits : array();

    $today = new DateTimeImmutable('today');
    $startDate = $today->sub(new DateInterval('P364D'));
    $gridStart = $startDate->sub(new DateInterval('P' . (int) $startDate->format('w') . 'D'));
    $gridEnd = $today->add(new DateInterval('P' . (6 - (int) $today->format('w')) . 'D'));
    $totalDays = (int) $gridStart->diff($gridEnd)->days + 1;
    $totalWeeks = (int) ceil($totalDays / 7);
    $visitCount = 0;
    $activeDays = 0;
    $maxCount = 0;

    foreach ($dailyVisits as $count) {
        $count = (int) $count;
        $visitCount += $count;
        if ($count > 0) {
            $activeDays++;
        }
        if ($count > $maxCount) {
            $maxCount = $count;
        }
    }

    $monthMarkers = array();
    $seenMonths = array();
    for ($visibleIndex = 0; $visibleIndex < 365; $visibleIndex++) {
        $visibleDate = $startDate->add(new DateInterval('P' . $visibleIndex . 'D'));
        $weekIndex = (int) floor($gridStart->diff($visibleDate)->days / 7);
        $monthKey = $visibleDate->format('Y-m');
        $showMonth = $visibleIndex === 0 || $visibleDate->format('d') === '01';
        if ($showMonth && !isset($seenMonths[$monthKey])) {
            $seenMonths[$monthKey] = true;
            $monthMarkers[] = '<span class="store-heatmap-month" style="grid-column:' . ($weekIndex + 1) . '">' . strtoupper($visibleDate->format('M')) . '</span>';
        }
    }

    $html = '';
    $html .= '<div class="store-heatmap">';
    $html .= '<div class="store-heatmap-header">';
    $html .= '<strong>Daily Visits (Last 365 days)</strong>';
    $html .= '<span>' . number_format($visitCount) . ' visits / ' . number_format($activeDays) . ' active days</span>';
    $html .= '</div>';
    $html .= '<div class="store-heatmap-shell">';
    $html .= '<div class="store-heatmap-months">' . implode('', $monthMarkers) . '</div>';
    $html .= '<div class="store-heatmap-body">';
    $html .= '<div class="store-heatmap-days"><span></span><span class="is-visible">Mon</span><span></span><span class="is-visible">Wed</span><span></span><span class="is-visible">Fri</span><span></span></div>';
    $html .= '<div class="store-heatmap-grid">';

    for ($dayIndex = 0; $dayIndex < $totalDays; $dayIndex++) {
        $current = $gridStart->add(new DateInterval('P' . $dayIndex . 'D'));
        if ($current < $startDate || $current > $today) {
            $html .= '<span class="store-heatmap-day is-empty" aria-hidden="true"></span>';
            continue;
        }

        $dateKey = $current->format('Y-m-d');
        $count = isset($dailyVisits[$dateKey]) ? (int) $dailyVisits[$dateKey] : 0;
        $level = store_form_heatmap_level($count, $maxCount);
        $title = htmlspecialchars($current->format('d/m/Y') . ': ' . $count . ' visits', ENT_QUOTES, 'UTF-8');
        $html .= '<button type="button" class="store-heatmap-day" data-level="' . $level . '" title="' . $title . '" aria-label="' . $title . '"></button>';
    }

    $html .= '</div>';
    $html .= '</div>';
    $html .= '<div class="store-heatmap-legend">';
    $html .= '<span>Less</span>';
    $html .= '<span class="store-heatmap-day is-legend" data-level="0"></span>';
    $html .= '<span class="store-heatmap-day is-legend" data-level="1"></span>';
    $html .= '<span class="store-heatmap-day is-legend" data-level="2"></span>';
    $html .= '<span class="store-heatmap-day is-legend" data-level="3"></span>';
    $html .= '<span class="store-heatmap-day is-legend" data-level="4"></span>';
    $html .= '<span>More</span>';
    $html .= '</div>';
    $html .= '</div>';
    $html .= '</div>';

    return $html;
}

function store_form_call_json($method, $url, $payload, &$error, &$httpCode = 0)
{
    $error = '';
    $httpCode = 0;

    if (!function_exists('curl_init')) {
        $error = 'May chu PHP chua bat cURL de ket noi backend.';
        return null;
    }

    $ch = curl_init($url);
    if ($ch === false) {
        $error = 'Khong khoi tao duoc ket noi backend.';
        return null;
    }

    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_CUSTOMREQUEST, strtoupper($method));
    curl_setopt($ch, CURLOPT_HTTPHEADER, array('Accept: application/json', 'Content-Type: application/json'));
    curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
    curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, false);

    if ($payload !== null) {
        curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($payload, JSON_UNESCAPED_UNICODE));
    }

    $body = curl_exec($ch);
    if ($body === false) {
        $error = curl_error($ch) !== '' ? curl_error($ch) : 'Không thể kết nối backend.';
        curl_close($ch);
        return null;
    }

    $httpCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);

    if ($body === '') {
        if ($httpCode >= 400) {
            $error = 'API trả về HTTP ' . $httpCode . '.';
            return null;
        }

        return array();
    }

    $decoded = json_decode($body, true);
    if ($decoded === null && strtolower(trim($body)) !== 'null') {
        $error = 'Phản hồi từ backend không hợp lệ.';
        return null;
    }

    if ($httpCode >= 400) {
        if (is_array($decoded) && !empty($decoded['message'])) {
            $error = (string) $decoded['message'];
        } else {
            $error = 'API trả về HTTP ' . $httpCode . '.';
        }
        return null;
    }

    return $decoded;
}

function store_form_call_file_upload($url, $fieldName, $fileInfo, &$error, &$httpCode = 0)
{
    $error = '';
    $httpCode = 0;

    if (!function_exists('curl_init') || !class_exists('CURLFile')) {
        $error = 'May chu PHP chua bat cURL/CURLFile de tai anh gian hang.';
        return null;
    }

    if (!is_array($fileInfo) || empty($fileInfo['tmp_name']) || !is_file($fileInfo['tmp_name'])) {
        $error = 'Không tìm thấy file tạm để tải lên.';
        return null;
    }

    $mimeType = !empty($fileInfo['type']) ? (string) $fileInfo['type'] : 'application/octet-stream';
    $fileName = !empty($fileInfo['name']) ? (string) $fileInfo['name'] : 'store-image';

    $payload = array(
        $fieldName => new CURLFile($fileInfo['tmp_name'], $mimeType, $fileName),
    );

    $ch = curl_init($url);
    if ($ch === false) {
        $error = 'Khong khoi tao duoc ket noi tai anh gian hang.';
        return null;
    }

    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_POST, true);
    curl_setopt($ch, CURLOPT_HTTPHEADER, array('Accept: application/json'));
    curl_setopt($ch, CURLOPT_POSTFIELDS, $payload);
    curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
    curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, false);
    curl_setopt($ch, CURLOPT_CONNECTTIMEOUT, 10);
    curl_setopt($ch, CURLOPT_TIMEOUT, 30);

    $body = curl_exec($ch);
    if ($body === false) {
        $error = curl_error($ch) !== '' ? curl_error($ch) : 'Không thể tải ảnh lên backend.';
        curl_close($ch);
        return null;
    }

    $httpCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);

    $decoded = $body !== '' ? json_decode($body, true) : array();
    if ($httpCode >= 400) {
        if (is_array($decoded) && !empty($decoded['message'])) {
            $error = (string) $decoded['message'];
        } else {
            $error = 'API trả về HTTP ' . $httpCode . '.';
        }
        return null;
    }

    if ($decoded === null && trim($body) !== '') {
        $error = 'Phản hồi tải ảnh không hợp lệ.';
        return null;
    }

    return is_array($decoded) ? $decoded : array();
}

function store_form_status_options()
{
    return array(
        'dang_hoat_dong' => 'Đang hoạt động',
        'tam_ngung' => 'Tạm ngừng',
        'dong_cua' => 'Đóng cửa',
    );
}

function store_form_language_options()
{
    return array(
        'vi' => 'Tiếng Việt',
        'en' => 'English',
        'ja' => '日本語',
        'ko' => '한국어',
    );
}

function store_form_normalize_language($languageCode)
{
    $languageCode = strtolower(trim((string) $languageCode));
    $allowed = array_keys(store_form_language_options());
    return in_array($languageCode, $allowed, true) ? $languageCode : 'vi';
}

function store_form_language_label($languageCode)
{
    $languageCode = store_form_normalize_language($languageCode);
    $options = store_form_language_options();
    return $options[$languageCode];
}

function store_form_normalize_status($status)
{
    $status = strtolower(trim((string) $status));
    $allowed = array_keys(store_form_status_options());
    return in_array($status, $allowed, true) ? $status : 'dang_hoat_dong';
}

function store_form_coordinate_error($value, $min, $max, $label, $required)
{
    $value = trim((string) $value);
    if ($value === '') {
        return $required ? $label . ' không được để trống khi gian hàng đang hoạt động.' : '';
    }

    if (!is_numeric($value)) {
        return $label . ' không hợp lệ.';
    }

    $number = (float) $value;
    if ($number < $min || $number > $max) {
        return $label . ' phải nằm trong khoảng ' . $min . ' đến ' . $max . '.';
    }

    return '';
}

function store_form_positive_number_error($value, $label, $required)
{
    $value = trim((string) $value);
    if ($value === '') {
        return $required ? $label . ' không được để trống.' : '';
    }

    if (!is_numeric($value)) {
        return $label . ' không hợp lệ.';
    }

    if ((float) $value <= 0) {
        return $label . ' phải lớn hơn 0.';
    }

    return '';
}

function store_form_status_meta($status)
{
    $status = store_form_normalize_status($status);
    $map = array(
        'dang_hoat_dong' => array('label' => 'Đang hoạt động', 'class' => 'active'),
        'tam_ngung' => array('label' => 'Tạm ngừng', 'class' => 'paused'),
        'dong_cua' => array('label' => 'Đóng cửa', 'class' => 'maintenance'),
    );

    return $map[$status];
}

function store_form_prepare_form_data($store)
{
    return array(
        'ten' => isset($store['ten']) ? (string) $store['ten'] : '',
        'diaChi' => isset($store['diaChi']) && $store['diaChi'] !== null ? (string) $store['diaChi'] : '',
        'moTa' => isset($store['moTa']) && $store['moTa'] !== null ? (string) $store['moTa'] : '',
        'lat' => isset($store['lat']) && $store['lat'] !== null ? (string) $store['lat'] : '',
        'lon' => isset($store['lon']) && $store['lon'] !== null ? (string) $store['lon'] : '',
        'vongBo' => isset($store['vongBo']) && $store['vongBo'] !== null ? (string) $store['vongBo'] : '10',
        'phiHangThang' => isset($store['phiHangThang']) ? (string) $store['phiHangThang'] : '0',
        'currentPhiHangThang' => isset($store['phiHangThang']) ? (string) $store['phiHangThang'] : '0',
        'tinhTrang' => store_form_normalize_status(isset($store['tinhTrang']) ? $store['tinhTrang'] : ''),
        'emailChuQuanLy' => isset($store['emailChuQuanLy']) && $store['emailChuQuanLy'] !== null ? (string) $store['emailChuQuanLy'] : '',
    );
}

function store_form_image_url($path)
{
    $path = trim((string) $path);
    if ($path === '') {
        return '';
    }

    return admin_url('api/image-proxy.php') . '?path=' . rawurlencode($path);
}

function store_form_initials($name)
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

function store_form_format_datetime($value, $emptyText = 'Chưa cập nhật')
{
    if (empty($value)) {
        return $emptyText;
    }

    $timestamp = strtotime((string) $value);
    if ($timestamp === false) {
        return $emptyText;
    }

    return date('d/m/Y H:i', $timestamp);
}

function store_form_display_value($value, $emptyText = 'Chưa có')
{
    $value = trim((string) $value);
    return $value !== '' ? $value : $emptyText;
}

function store_form_owner_option_label($owner)
{
    $parts = array();

    if (!empty($owner['hoTen'])) {
        $parts[] = (string) $owner['hoTen'];
    }
    if (!empty($owner['username'])) {
        $parts[] = '@' . (string) $owner['username'];
    }
    if (!empty($owner['email'])) {
        $parts[] = (string) $owner['email'];
    }

    if (count($parts) === 0) {
        return 'Chủ gian hàng #' . (int) ($owner['idChuQuanLy'] ?? 0);
    }

    return implode(' - ', $parts);
}

function store_form_format_money($value)
{
    return number_format((float) $value, 0, ',', '.') . ' đ';
}

$flash = isset($_GET['flash']) ? (string) $_GET['flash'] : '';
$selectedLanguage = isset($_GET['lang']) ? store_form_normalize_language($_GET['lang']) : 'vi';
if ($flash === 'created') {
    $pageMessage = array('type' => 'success', 'text' => 'Đã tạo gian hàng mới và lưu vào cơ sở dữ liệu.');
} elseif ($flash === 'created_partial') {
    $pageMessage = array('type' => 'warning', 'text' => 'Đã tạo gian hàng mới nhưng mô tả chưa cập nhật được.');
} elseif ($flash === 'request_sent') {
    $pageMessage = array('type' => 'success', 'text' => 'Đã gửi yêu cầu mở gian hàng mới. Vui lòng chờ admin duyệt.');
}

$isOwnerRequestMode = $isCreateMode && $loaiTaiKhoan === 'chu_quan_ly';
$isOwnerStoreEditMode = !$isCreateMode && $loaiTaiKhoan === 'chu_quan_ly';
$showOwnerEmailInput = $loaiTaiKhoan === 'admin';
$canEditVongBo = $loaiTaiKhoan === 'admin';
if ($showOwnerEmailInput && $idTaiKhoan > 0) {
    $ownerOptionsResult = store_form_call_json('GET', store_form_owner_options_url($idTaiKhoan), null, $ownerOptionsError);
    if (is_array($ownerOptionsResult)) {
        foreach ($ownerOptionsResult as $ownerOption) {
            if (is_array($ownerOption) && !empty($ownerOption['email'])) {
                $ownerOptions[] = $ownerOption;
            }
        }
    }
}

$store = null;
$formData = array(
    'ten' => '',
    'diaChi' => '',
    'moTa' => '',
    'lat' => '',
    'lon' => '',
    'vongBo' => '10',
    'phiHangThang' => '0',
    'currentPhiHangThang' => '0',
    'tinhTrang' => 'dang_hoat_dong',
    'emailChuQuanLy' => '',
);

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['store_form_submit'])) {
    $formData = array(
        'ten' => trim((string) ($_POST['ten'] ?? '')),
        'diaChi' => trim((string) ($_POST['diaChi'] ?? '')),
        'moTa' => trim((string) ($_POST['moTa'] ?? '')),
        'lat' => trim((string) ($_POST['lat'] ?? '')),
        'lon' => trim((string) ($_POST['lon'] ?? '')),
        'vongBo' => trim((string) ($_POST['vongBo'] ?? '10')),
        'phiHangThang' => trim((string) ($_POST['phiHangThang'] ?? '0')),
        'currentPhiHangThang' => trim((string) ($_POST['currentPhiHangThang'] ?? '0')),
        'tinhTrang' => store_form_normalize_status($_POST['tinhTrang'] ?? ''),
        'emailChuQuanLy' => trim((string) ($_POST['emailChuQuanLy'] ?? '')),
    );

    $uploadedImage = isset($_FILES['storeImage']) && is_array($_FILES['storeImage']) ? $_FILES['storeImage'] : null;
    $hasUploadedImage = $uploadedImage !== null && isset($uploadedImage['error']) && (int) $uploadedImage['error'] === UPLOAD_ERR_OK;
    $imageUploadError = '';
    if ($uploadedImage !== null && isset($uploadedImage['error'])) {
        $uploadErrorCode = (int) $uploadedImage['error'];
        if ($uploadErrorCode !== UPLOAD_ERR_OK && $uploadErrorCode !== UPLOAD_ERR_NO_FILE) {
            $imageUploadError = 'Tải file ảnh lên thất bại. Vui lòng chọn lại ảnh hợp lệ.';
        }
    }

    $requiresCoordinates = !$isOwnerRequestMode && $formData['tinhTrang'] === 'dang_hoat_dong';
    $latError = !$isOwnerRequestMode ? store_form_coordinate_error($formData['lat'], -90, 90, 'Vĩ độ', $requiresCoordinates) : '';
    $lonError = !$isOwnerRequestMode ? store_form_coordinate_error($formData['lon'], -180, 180, 'Kinh độ', $requiresCoordinates) : '';
    $vongBoError = $canEditVongBo ? store_form_positive_number_error($formData['vongBo'], 'Vòng bò', true) : '';
    if ($formData['ten'] === '') {
        $pageMessage = array('type' => 'error', 'text' => 'Tên gian hàng không được để trống.');
    } elseif ($idTaiKhoan <= 0) {
        $pageMessage = array('type' => 'error', 'text' => 'Phiên đăng nhập không hợp lệ, không thể lưu dữ liệu.');
    } elseif ($showOwnerEmailInput && $formData['emailChuQuanLy'] === '') {
        $pageMessage = array('type' => 'error', 'text' => 'Admin cần nhập email của chủ gian hàng.');
    } elseif ($latError !== '') {
        $pageMessage = array('type' => 'error', 'text' => $latError);
    } elseif ($lonError !== '') {
        $pageMessage = array('type' => 'error', 'text' => $lonError);
    } elseif ($vongBoError !== '') {
        $pageMessage = array('type' => 'error', 'text' => $vongBoError);
    } elseif ($imageUploadError !== '') {
        $pageMessage = array('type' => 'error', 'text' => $imageUploadError);
    } else {
        $effectivePhiHangThang = 0;
        if ($isOwnerRequestMode) {
            $effectivePhiHangThang = 0;
        } elseif ($isOwnerStoreEditMode) {
            $effectivePhiHangThang = $formData['currentPhiHangThang'] !== '' ? (float) $formData['currentPhiHangThang'] : 0;
        } else {
            $effectivePhiHangThang = $formData['phiHangThang'] !== '' ? (float) $formData['phiHangThang'] : 0;
        }

        $payload = array(
            'ten' => $formData['ten'],
            'diaChi' => $formData['diaChi'] !== '' ? $formData['diaChi'] : null,
            'lat' => !$isOwnerRequestMode && $formData['lat'] !== '' ? (float) $formData['lat'] : null,
            'lon' => !$isOwnerRequestMode && $formData['lon'] !== '' ? (float) $formData['lon'] : null,
            'phiHangThang' => $effectivePhiHangThang,
            'tinhTrang' => $formData['tinhTrang'],
        );

        if ($canEditVongBo) {
            $payload['vongBo'] = (float) $formData['vongBo'];
        }

        if ($showOwnerEmailInput) {
            $payload['emailChuQuanLy'] = $formData['emailChuQuanLy'];
        }

        $descriptionError = '';
        $descriptionResult = array();
        $imageError = '';
        $imageResult = array();

        if ($isCreateMode) {
            if ($loaiTaiKhoan === 'chu_quan_ly') {
                $requestPayload = array(
                    'ten' => $payload['ten'],
                    'diaChi' => $payload['diaChi'],
                    'moTa' => $formData['moTa'] !== '' ? $formData['moTa'] : null,
                );

                $requestError = '';
                $requestHttpCode = 0;
                $requestResult = store_form_call_json('POST', store_form_request_collection_url($idTaiKhoan), $requestPayload, $requestError, $requestHttpCode);

                if ($requestResult !== null) {
                    store_form_redirect(admin_url('index1st.php?usecase=store&flash=request_sent'));
                }

                $pageMessage = array('type' => 'error', 'text' => 'Gửi yêu cầu thất bại: ' . ($requestError !== '' ? $requestError : 'backend API chưa sẵn sàng.'));
            } else {
                $createError = '';
                $createResult = store_form_call_json('POST', store_form_collection_url($loaiTaiKhoan, $idTaiKhoan), $payload, $createError);

                if ($createResult === null) {
                    $pageMessage = array('type' => 'error', 'text' => 'Tạo gian hàng thất bại: ' . $createError);
                } else {
                    $newId = isset($createResult['idGianHang']) ? (int) $createResult['idGianHang'] : 0;
                    if ($newId <= 0) {
                        $pageMessage = array('type' => 'error', 'text' => 'Backend đã phản hồi nhưng không trả về id gian hàng mới.');
                    } elseif ($descriptionResult === null) {
                        $pageMessage = array('type' => 'warning', 'text' => 'Đã lưu thông tin chính nhưng mô tả và ảnh chưa cập nhật được: ' . $descriptionError . ' | ' . $imageError);
                    } elseif ($hasUploadedImage && $imageResult === null) {
                        $pageMessage = array('type' => 'warning', 'text' => 'Đã lưu thông tin gian hàng nhưng ảnh chưa cập nhật được: ' . $imageError);
                    } else {
                        $descriptionError = '';
                        $descriptionResult = store_form_call_json(
                            'PUT',
                            store_form_description_url($newId, $idTaiKhoan),
                            array('languageCode' => $selectedLanguage, 'moTa' => $formData['moTa']),
                            $descriptionError
                        );

                        $imageError = '';
                        $imageResult = null;
                        if ($hasUploadedImage) {
                            $imageResult = store_form_call_file_upload(
                                store_form_image_upload_url($loaiTaiKhoan, $idTaiKhoan, $newId),
                                'image',
                                $uploadedImage,
                                $imageError
                            );
                        }

                        $flashTarget = ($descriptionResult === null || ($hasUploadedImage && $imageResult === null)) ? 'created_partial' : 'created';
                        store_form_redirect(admin_url('index1st.php?usecase=branchdetail2&idGianHang=' . $newId . '&lang=' . rawurlencode($selectedLanguage) . '&flash=' . rawurlencode($flashTarget)));
                    }
                }
            }
        } else {
            $updateError = '';
            $updateResult = store_form_call_json('PUT', store_form_detail_url($loaiTaiKhoan, $idTaiKhoan, $idGianHang), $payload, $updateError);

            if ($updateResult === null) {
                $pageMessage = array('type' => 'error', 'text' => 'Lưu thông tin gian hàng thất bại: ' . $updateError);
            } else {
                $descriptionError = '';
                $descriptionResult = store_form_call_json(
                    'PUT',
                    store_form_description_url($idGianHang, $idTaiKhoan),
                    array('languageCode' => $selectedLanguage, 'moTa' => $formData['moTa']),
                    $descriptionError
                );

                $imageError = '';
                $imageResult = null;
                if ($hasUploadedImage) {
                    $imageResult = store_form_call_file_upload(
                        store_form_image_upload_url($loaiTaiKhoan, $idTaiKhoan, $idGianHang),
                        'image',
                        $uploadedImage,
                        $imageError
                    );
                }

                if ($descriptionResult === null || ($hasUploadedImage && $imageResult === null)) {
                    $pageMessage = array('type' => 'warning', 'text' => 'Đã lưu thông tin chính nhưng mô tả chưa cập nhật được: ' . $descriptionError);
                } else {
                    $pageMessage = array('type' => 'success', 'text' => 'Đã lưu thông tin gian hàng xuống cơ sở dữ liệu.');
                }
            }
        }
    }
}

if (!$isCreateMode) {
    if ($idGianHang <= 0) {
        $pageError = 'Không xác định được gian hàng cần chỉnh sửa.';
    } else {
        $detailError = '';
        $detailResult = store_form_call_json('GET', store_form_detail_url($loaiTaiKhoan, $idTaiKhoan, $idGianHang), null, $detailError);

        if (is_array($detailResult)) {
            $store = $detailResult;
            if ($_SERVER['REQUEST_METHOD'] !== 'POST' || $pageMessage === null || $pageMessage['type'] !== 'error') {
                $formData = store_form_prepare_form_data($store);
            }
        } elseif ($pageError === '') {
            $pageError = 'Không tải được thông tin gian hàng: ' . $detailError;
        }
    }
}

$statusMeta = store_form_status_meta($formData['tinhTrang']);
$imageUrl = $store !== null && !empty($store['hinhAnh']) ? store_form_image_url($store['hinhAnh']) : '';
$ownerName = $store !== null
    ? store_form_display_value($store['tenChuQuanLy'] ?? '', 'Chưa gán chủ gian hàng')
    : ($showOwnerEmailInput ? 'Sẽ cập nhật theo email bạn nhập' : store_form_display_value($auth['hoTen'] ?? ($auth['username'] ?? ''), 'Chủ gian hàng hiện tại'));
$ownerEmail = $store !== null
    ? store_form_display_value($store['emailChuQuanLy'] ?? '', 'Chưa có email')
    : ($showOwnerEmailInput ? store_form_display_value($formData['emailChuQuanLy'], 'Nhập email để gán chủ') : store_form_display_value($auth['email'] ?? '', 'Chưa có email'));
$ownerUsername = $store !== null
    ? store_form_display_value($store['usernameChuQuanLy'] ?? '', 'Chưa có username')
    : store_form_display_value($auth['username'] ?? '', 'Chưa có username');
$pageHeading = $isOwnerRequestMode ? 'Gửi yêu cầu mở gian hàng' : ($isCreateMode ? 'Thêm gian hàng mới' : 'Chỉnh sửa thông tin gian hàng');
$pageIntro = $isOwnerRequestMode
    ? 'Chủ quản lý sẽ gửi yêu cầu mở gian hàng mới để admin xem xét và phê duyệt trước khi tạo gian hàng thật.'
    : ($isCreateMode
        ? 'Tạo gian hàng mới và lưu trực tiếp vào cơ sở dữ liệu qua backend hiện tại.'
        : 'Cập nhật trực tiếp dữ liệu gian hàng đang lưu trên hệ thống quản trị. Admin có thể đổi chủ gian hàng bằng email.');
$pageIntro .= ' Đang xem: ' . store_form_language_label($selectedLanguage) . '.';
$submitLabel = $isOwnerRequestMode ? 'Gửi yêu cầu' : ($isCreateMode ? 'Tạo gian hàng' : 'Lưu thay đổi');
$formAction = $isCreateMode
    ? admin_url('index1st.php?usecase=branchdetail2&mode=create&lang=' . rawurlencode($selectedLanguage))
    : admin_url('index1st.php?usecase=branchdetail2&idGianHang=' . (int) $idGianHang . '&lang=' . rawurlencode($selectedLanguage));
$displayStoreName = $store !== null && !empty($store['tenHienThi'])
    ? (string) $store['tenHienThi']
    : $formData['ten'];
$storeDailyVisits = !$isCreateMode ? store_form_fetch_daily_visits($idGianHang) : array();
?>
<main class="main-content">
  <section class="branch-page">
    <div class="page-head">
      <div>
        <h2><?php echo htmlspecialchars($pageHeading, ENT_QUOTES, 'UTF-8'); ?></h2>
        <p><?php echo htmlspecialchars($pageIntro, ENT_QUOTES, 'UTF-8'); ?></p>
      </div>

      <div class="page-head-actions">
        <form class="language-switcher" method="get" action="<?php echo htmlspecialchars(admin_url('index1st.php'), ENT_QUOTES, 'UTF-8'); ?>">
          <input type="hidden" name="usecase" value="branchdetail2" />
          <?php if ($isCreateMode) { ?>
          <input type="hidden" name="mode" value="create" />
          <?php } else { ?>
          <input type="hidden" name="idGianHang" value="<?php echo (int) $idGianHang; ?>" />
          <?php } ?>
          <label for="store-language-select">Ngôn ngữ xem</label>
          <select id="store-language-select" name="lang" onchange="this.form.submit()">
            <?php foreach (store_form_language_options() as $languageCode => $languageLabel) { ?>
            <option value="<?php echo htmlspecialchars($languageCode, ENT_QUOTES, 'UTF-8'); ?>" <?php echo $selectedLanguage === $languageCode ? 'selected' : ''; ?>><?php echo htmlspecialchars($languageLabel, ENT_QUOTES, 'UTF-8'); ?></option>
            <?php } ?>
          </select>
        </form>
        <a class="secondary-btn link-btn" href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=store'), ENT_QUOTES, 'UTF-8'); ?>">Quay lại danh sách</a>
        <?php if ($pageError === '') { ?>
        <button class="primary-btn" type="submit" form="store-edit-form">
          <i class="fa-solid fa-floppy-disk"></i>
          <span><?php echo htmlspecialchars($submitLabel, ENT_QUOTES, 'UTF-8'); ?></span>
        </button>
        <?php } ?>
      </div>
    </div>

    <?php if ($pageMessage !== null) { ?>
    <div class="store-edit-alert <?php echo htmlspecialchars($pageMessage['type'], ENT_QUOTES, 'UTF-8'); ?>">
      <?php echo htmlspecialchars($pageMessage['text'], ENT_QUOTES, 'UTF-8'); ?>
    </div>
    <?php } ?>

    <?php if ($pageError !== '') { ?>
    <div class="store-edit-alert error">
      <?php echo htmlspecialchars($pageError, ENT_QUOTES, 'UTF-8'); ?>
    </div>
    <?php } else { ?>
    <form id="store-edit-form" method="post" action="<?php echo htmlspecialchars($formAction, ENT_QUOTES, 'UTF-8'); ?>" enctype="multipart/form-data">
      <input type="hidden" name="store_form_submit" value="1" />

      <div class="stats-row">
        <div class="panel stat-card">
          <p class="stat-label">Mã gian hàng</p>
          <h3><?php echo $isCreateMode ? 'Tạo mới' : '#GH-' . str_pad((string) $idGianHang, 3, '0', STR_PAD_LEFT); ?></h3>
        </div>

        <div class="panel stat-card">
          <p class="stat-label">Trạng thái hiện tại</p>
          <h3 class="accent"><?php echo htmlspecialchars($statusMeta['label'], ENT_QUOTES, 'UTF-8'); ?></h3>
        </div>

        <div class="panel stat-card">
          <p class="stat-label"><?php echo $isCreateMode ? 'Tình trạng lưu' : 'Cập nhật lần cuối'; ?></p>
          <h3 class="success-text"><?php echo htmlspecialchars($isCreateMode ? 'Chưa lưu' : store_form_format_datetime($store['thoiGianCapNhat'] ?? null), ENT_QUOTES, 'UTF-8'); ?></h3>
        </div>
      </div>

      <div class="bottom-grid store-edit-grid">
        <div class="panel form-panel">
          <div class="card-head">
            <h3><i class="fa-solid fa-store"></i> Thông tin lưu xuống DB</h3>
          </div>

          <div class="store-form">
            <div class="form-grid">
              <label class="form-field full-width">
                <span>Tên gian hàng</span>
                <input type="text" name="ten" value="<?php echo htmlspecialchars($formData['ten'], ENT_QUOTES, 'UTF-8'); ?>" required />
              </label>

              <?php if ($showOwnerEmailInput) { ?>
              <label class="form-field full-width">
                <span>Email chủ quản lý</span>
                <input type="email" name="emailChuQuanLy" list="owner-email-options" value="<?php echo htmlspecialchars($formData['emailChuQuanLy'], ENT_QUOTES, 'UTF-8'); ?>" placeholder="owner@example.com" required />
                <small class="form-help">Nhập email tài khoản của chủ gian hàng. Khi lưu, hệ thống sẽ tự đổi `idChuQuanLy` theo email này.</small>
                <?php if ($ownerOptionsError !== '') { ?>
                <small class="form-help warning">Không tải được gợi ý email chủ gian hàng. Bạn vẫn có thể nhập email trực tiếp.</small>
                <?php } ?>
              </label>
              <?php if (count($ownerOptions) > 0) { ?>
              <datalist id="owner-email-options">
                <?php foreach ($ownerOptions as $ownerOption) { ?>
                <option value="<?php echo htmlspecialchars((string) $ownerOption['email'], ENT_QUOTES, 'UTF-8'); ?>" label="<?php echo htmlspecialchars(store_form_owner_option_label($ownerOption), ENT_QUOTES, 'UTF-8'); ?>"></option>
                <?php } ?>
              </datalist>
              <?php } ?>
              <?php } ?>

              <label class="form-field full-width">
                <span>Địa chỉ</span>
                <input type="text" name="diaChi" value="<?php echo htmlspecialchars($formData['diaChi'], ENT_QUOTES, 'UTF-8'); ?>" />
              </label>

              <label class="form-field full-width">
                <span><?php echo $isOwnerRequestMode ? 'Ghi chú gửi' : ('Mô tả (' . htmlspecialchars(store_form_language_label($selectedLanguage), ENT_QUOTES, 'UTF-8') . ')'); ?></span>
                <textarea name="moTa" rows="5"><?php echo htmlspecialchars($formData['moTa'], ENT_QUOTES, 'UTF-8'); ?></textarea>
                <?php if ($isOwnerRequestMode) { ?>
                <small class="form-help">Chủ quản lý chỉ gửi ghi chú cho admin tham khảo khi duyệt yêu cầu.</small>
                <?php } else { ?>
                <small class="form-help">Nội dung này được tải và lưu theo ngôn ngữ đang chọn ở góc trên.</small>
                <?php } ?>
              </label>

              <?php if (!$isOwnerRequestMode) { ?>
              <label class="form-field">
                <span>Vĩ độ (Lat)</span>
                <input type="number" min="-90" max="90" step="0.000001" name="lat" value="<?php echo htmlspecialchars($formData['lat'], ENT_QUOTES, 'UTF-8'); ?>" />
              </label>

              <label class="form-field">
                <span>Kinh độ (Lon)</span>
                <input type="number" min="-180" max="180" step="0.000001" name="lon" value="<?php echo htmlspecialchars($formData['lon'], ENT_QUOTES, 'UTF-8'); ?>" />
              </label>

              <label class="form-field">
                <span>Vòng bò (m)</span>
                <?php if ($canEditVongBo) { ?>
                <input type="number" min="0.01" step="0.01" name="vongBo" value="<?php echo htmlspecialchars($formData['vongBo'], ENT_QUOTES, 'UTF-8'); ?>" required />
                <small class="form-help">Bán kính geofence của gian hàng, phải lớn hơn 0 mét.</small>
                <?php } else { ?>
                <input type="hidden" name="vongBo" value="<?php echo htmlspecialchars($formData['vongBo'], ENT_QUOTES, 'UTF-8'); ?>" />
                <input type="text" value="<?php echo htmlspecialchars($formData['vongBo'] . ' m', ENT_QUOTES, 'UTF-8'); ?>" readonly />
                <small class="form-help">Chỉ admin mới được chỉnh sửa vòng bò của gian hàng.</small>
                <?php } ?>
              </label>

              <label class="form-field">
                <span>Trạng thái</span>
                <select name="tinhTrang">
                  <?php foreach (store_form_status_options() as $value => $label) { ?>
                  <option value="<?php echo htmlspecialchars($value, ENT_QUOTES, 'UTF-8'); ?>" <?php echo $formData['tinhTrang'] === $value ? 'selected' : ''; ?>><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></option>
                  <?php } ?>
                </select>
              </label>
              <?php } ?>

              <label class="form-field">
                <span>Phí hàng tháng</span>
                <?php if ($isOwnerRequestMode) { ?>
                <input type="text" value="Admin sẽ nhập khi xử lý yêu cầu" readonly />
                <small class="form-help">Chủ quản lý không được tự đặt phí hàng tháng. Admin sẽ cập nhật mục này khi duyệt yêu cầu.</small>
                <?php } elseif ($isOwnerStoreEditMode) { ?>
                <input type="hidden" name="currentPhiHangThang" value="<?php echo htmlspecialchars($formData['currentPhiHangThang'] !== '' ? $formData['currentPhiHangThang'] : $formData['phiHangThang'], ENT_QUOTES, 'UTF-8'); ?>" />
                <input type="text" value="<?php echo htmlspecialchars(store_form_format_money($formData['currentPhiHangThang'] !== '' ? $formData['currentPhiHangThang'] : $formData['phiHangThang']), ENT_QUOTES, 'UTF-8'); ?>" readonly />
                <small class="form-help">Mức phí này do admin quản lý. Chủ gian hàng chỉ được xem, không thể chỉnh sửa trực tiếp.</small>
                <?php } else { ?>
                <input type="number" min="0" step="1000" name="phiHangThang" value="<?php echo htmlspecialchars($formData['phiHangThang'], ENT_QUOTES, 'UTF-8'); ?>" />
                <?php } ?>
              </label>
            </div>
          </div>
        </div>

        <div class="panel side-panel">
          <div class="card-head">
            <h3><i class="fa-solid fa-image"></i> Tổng quan gian hàng</h3>
          </div>

          <div id="storeImagePreview" class="cover-preview <?php echo $imageUrl !== '' ? 'has-image' : ''; ?>"<?php echo $imageUrl !== '' ? ' style="background-image:url(\'' . htmlspecialchars($imageUrl, ENT_QUOTES, 'UTF-8') . '\')"' : ''; ?>>
            <div class="cover-badge"><?php echo $isCreateMode ? 'Gian hàng mới' : 'Ảnh gian hàng'; ?></div>
            <?php if ($imageUrl === '') { ?>
            <span class="cover-empty"><?php echo $isCreateMode ? 'Ảnh sẽ hiển thị sau khi bạn cập nhật media riêng' : 'Chưa có ảnh hiển thị'; ?></span>
            <?php } ?>
          </div>

          <div class="readonly-item">
            <span>Image Upload</span>
            <input id="storeImageInput" type="file" name="storeImage" accept="image/*" />
          </div>

          <div class="logo-row">
            <div class="logo-preview"><?php echo htmlspecialchars(store_form_initials($formData['ten']), ENT_QUOTES, 'UTF-8'); ?></div>
            <div class="logo-meta">
              <strong><?php echo htmlspecialchars(store_form_display_value($displayStoreName, $isCreateMode ? 'Gian hàng mới' : 'Chưa có tên'), ENT_QUOTES, 'UTF-8'); ?></strong>
              <p><?php echo htmlspecialchars($isCreateMode ? 'Dữ liệu sẽ được tạo mới trong DB' : ('Đang sửa gian hàng #' . (int) $idGianHang), ENT_QUOTES, 'UTF-8'); ?></p>
            </div>
          </div>

          <div class="readonly-stack">
            <div class="readonly-item">
              <span>Chủ quản lý hiện tại</span>
              <strong><?php echo htmlspecialchars($ownerName, ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>

            <div class="readonly-item">
              <span>Email tài khoản hiện tại</span>
              <strong><?php echo htmlspecialchars($ownerEmail, ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>

            <div class="readonly-item">
              <span>Username</span>
              <strong><?php echo htmlspecialchars($ownerUsername, ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>

            <div class="readonly-item">
              <span>Ngôn ngữ đang xem</span>
              <strong><?php echo htmlspecialchars(store_form_language_label($selectedLanguage), ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>

            <div class="readonly-item">
              <span><?php echo $isCreateMode ? 'Sau khi tạo' : 'Ngày đăng ký'; ?></span>
              <strong><?php echo htmlspecialchars($isCreateMode ? 'Hệ thống sẽ sinh bản ghi mới ngay khi lưu.' : store_form_format_datetime($store['ngayDangKy'] ?? null, 'Chưa có dữ liệu'), ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
          </div>

          <?php if (!$isCreateMode) { ?>
          <div class="menu-shortcut-card">
            <div class="card-head">
              <h3><i class="fa-solid fa-bowl-food"></i> Quản lý món ăn</h3>
            </div>
            <p>Mở danh sách món ăn của gian hàng này để thêm món mới, cập nhật giá bán và thay đổi trạng thái phục vụ.</p>
            <a class="primary-btn link-btn menu-link-btn" href="<?php echo htmlspecialchars(store_form_food_management_url($idGianHang), ENT_QUOTES, 'UTF-8'); ?>">
              <i class="fa-solid fa-utensils"></i>
              <span>Quản lý món ăn</span>
            </a>
          </div>
          <?php } ?>

          <p class="panel-note"><?php echo htmlspecialchars(
              $isCreateMode
                  ? 'Form này đang gọi trực tiếp endpoint tạo mới của backend. Sau khi tạo xong, trang sẽ tự mở sang chế độ chỉnh sửa của gian hàng vừa tạo.'
                  : ($isOwnerStoreEditMode
                      ? 'Chủ gian hàng có thể cập nhật tên gian hàng, địa chỉ, mô tả, tọa độ và trạng thái. Riêng phí hàng tháng do admin quản lý.'
                      : 'Trang này hiện lưu thật các trường: tên gian hàng, địa chỉ, mô tả, tọa độ, trạng thái, phí hàng tháng và có thể đổi chủ bằng email.'),
              ENT_QUOTES,
              'UTF-8'
          ); ?></p>
        </div>
      </div>

      <?php if (!$isCreateMode) { ?>
      <div class="panel store-heatmap-panel">
        <div class="card-head">
          <h3><i class="fa-solid fa-chart-simple"></i> Daily Visits</h3>
          <span class="store-heatmap-note">Admin va chu gian hang deu xem duoc</span>
        </div>
        <?php echo store_form_render_heatmap($storeDailyVisits); ?>
      </div>
      <?php } ?>
    </form>
    <?php } ?>
  </section>
</main>
<script>
(function () {
  var input = document.getElementById('storeImageInput');
  var preview = document.getElementById('storeImagePreview');
  if (!input || !preview || !window.URL || !window.URL.createObjectURL) {
    return;
  }

  var initialBackground = preview.style.backgroundImage;
  var initialHadImage = preview.classList.contains('has-image');
  var objectUrl = '';

  input.addEventListener('change', function () {
    if (objectUrl) {
      URL.revokeObjectURL(objectUrl);
      objectUrl = '';
    }

    var file = input.files && input.files.length > 0 ? input.files[0] : null;
    if (!file || !file.type || file.type.indexOf('image/') !== 0) {
      preview.style.backgroundImage = initialBackground;
      preview.classList.toggle('has-image', initialHadImage);
      return;
    }

    objectUrl = URL.createObjectURL(file);
    preview.style.backgroundImage = 'url("' + objectUrl.replace(/"/g, '%22') + '")';
    preview.classList.add('has-image');
  });
})();
</script>
