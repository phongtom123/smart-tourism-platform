<?php
$auth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($auth['idTaiKhoan']) ? (int) $auth['idTaiKhoan'] : 0;
$dashboardError = '';

function dashboard_range_options()
{
    return array(
        '7' => '7 ngày gần nhất',
        '30' => '30 ngày gần nhất',
        'month' => 'Tháng này',
        'custom' => 'Tùy chọn',
    );
}

function dashboard_selected_range()
{
    $range = isset($_GET['range']) ? strtolower(trim((string) $_GET['range'])) : '7';
    return array_key_exists($range, dashboard_range_options()) ? $range : '7';
}

function dashboard_store_month_options()
{
    return array(
        1 => '1 tháng',
        3 => '3 tháng',
        6 => '6 tháng',
        12 => '12 tháng',
    );
}

function dashboard_selected_store_months()
{
    $months = isset($_GET['store_months']) ? (int) $_GET['store_months'] : 6;
    return array_key_exists($months, dashboard_store_month_options()) ? $months : 6;
}

function dashboard_days_for_range($range)
{
    if ($range === '30') {
        return 30;
    }

    if ($range === 'month') {
        return max(1, (int) date('j'));
    }

    return 7;
}

function dashboard_normalize_date($value)
{
    $value = trim((string) $value);
    if ($value === '') {
        return '';
    }

    $date = DateTime::createFromFormat('Y-m-d', $value);
    return $date instanceof DateTime && $date->format('Y-m-d') === $value ? $value : '';
}

function dashboard_days_between($startDate, $endDate)
{
    $start = new DateTime($startDate);
    $end = new DateTime($endDate);
    return max(1, (int) $start->diff($end)->days + 1);
}

function dashboard_fetch_summary_from_api($idTaiKhoan, &$error)
{
    $error = '';
    $apiHttpCode = 0;
    $result = admin_api_call(
        'GET',
        'Admin/summary',
        null,
        $error,
        $apiHttpCode,
        array('idTaiKhoan' => $idTaiKhoan)
    );
    return is_array($result) ? $result : null;
}

function dashboard_paid_store_subquery()
{
    return "
        SELECT latest_invoice.idGianHang
        FROM hoadongianhang latest_invoice
        INNER JOIN (
            SELECT idGianHang, MAX(idHoaDonGianHang) AS latestId
            FROM hoadongianhang
            GROUP BY idGianHang
        ) last_invoice ON last_invoice.latestId = latest_invoice.idHoaDonGianHang
        WHERE latest_invoice.trangThai = 'da_thanh_toan'
    ";
}

function dashboard_count_paid_stores($conn)
{
    return (int) dashboard_query_value($conn, "
        SELECT COUNT(*)
        FROM gianhang gh
        INNER JOIN (" . dashboard_paid_store_subquery() . ") paid ON paid.idGianHang = gh.idGianHang
    ");
}

function dashboard_count_active_owners($conn)
{
    return (int) dashboard_query_value($conn, "
        SELECT COUNT(DISTINCT cql.idChuQuanLy)
        FROM gianhang gh
        INNER JOIN (" . dashboard_paid_store_subquery() . ") paid ON paid.idGianHang = gh.idGianHang
        INNER JOIN chu_quan_ly cql ON cql.idChuQuanLy = gh.idChuQuanLy
        INNER JOIN taikhoan tk ON tk.idTaiKhoan = cql.idTaiKhoan
        WHERE tk.tinhTrang = 'hoat_dong'
    ");
}

function dashboard_count_paid_store_foods($conn)
{
    return (int) dashboard_query_value($conn, "
        SELECT COUNT(*)
        FROM monan ma
        INNER JOIN (" . dashboard_paid_store_subquery() . ") paid ON paid.idGianHang = ma.idGianHang
    ");
}

function dashboard_count_active_devices($conn)
{
    return (int) dashboard_query_value($conn, "
        SELECT COUNT(*) FROM thietbi WHERE trangThai = 'hoat_dong'
    ");
}

// Mỗi thiết bị đếm là N (đổi tại đây nếu muốn weight khác).
const DASHBOARD_DEVICE_WEIGHT = 1;

function dashboard_count_devices_with_active_token($conn)
{
    $real = (int) dashboard_query_value($conn, "
        SELECT COUNT(*)
        FROM phien_vao_app pva
        INNER JOIN (
            SELECT maThietBi, MAX(id) AS latestId
            FROM phien_vao_app
            GROUP BY maThietBi
        ) latest ON latest.latestId = pva.id
        WHERE pva.trangThai = 'hieu_luc'
          AND pva.hetHanLuc >= NOW()
    ");
    return $real * DASHBOARD_DEVICE_WEIGHT;
}

function dashboard_count_online_devices($conn, $windowSeconds = 60)
{
    $windowSeconds = max(5, (int) $windowSeconds);
    $real = (int) dashboard_query_value($conn, "
        SELECT COUNT(*)
        FROM thietbi
        WHERE lanCuoiHoatDong IS NOT NULL
          AND lanCuoiHoatDong >= NOW() - INTERVAL $windowSeconds SECOND
    ");
    return $real * DASHBOARD_DEVICE_WEIGHT;
}

function dashboard_count_pending_requests($conn)
{
    return (int) dashboard_query_value($conn, "
        SELECT COUNT(*) FROM yeucaugianhang WHERE trangThai = 'cho_duyet'
    ");
}

function dashboard_count_paid_orders_in_period($conn, $periodStart, $periodEnd)
{
    return (int) dashboard_query_value($conn, "
        SELECT COUNT(*)
        FROM hoadon
        WHERE tinhTrang = 'da_thanh_toan'
          AND thoiGianTao >= ?
          AND thoiGianTao <= ?
    ", 'ss', array($periodStart, $periodEnd));
}

function dashboard_query_value($conn, $sql, $types = '', $params = array(), $fallback = 0)
{
    if (!$conn instanceof mysqli) {
        return $fallback;
    }

    $stmt = $conn->prepare($sql);
    if (!$stmt) {
        return $fallback;
    }

    if ($types !== '' && count($params) > 0) {
        dashboard_bind_params($stmt, $types, $params);
    }

    if (!$stmt->execute()) {
        $stmt->close();
        return $fallback;
    }

    $result = $stmt->get_result();
    $row = $result ? $result->fetch_row() : null;
    if ($result) {
        $result->free();
    }
    $stmt->close();

    return $row && isset($row[0]) ? $row[0] : $fallback;
}

function dashboard_query_rows($conn, $sql, $types = '', $params = array())
{
    if (!$conn instanceof mysqli) {
        return array();
    }

    $stmt = $conn->prepare($sql);
    if (!$stmt) {
        return array();
    }

    if ($types !== '' && count($params) > 0) {
        dashboard_bind_params($stmt, $types, $params);
    }

    if (!$stmt->execute()) {
        $stmt->close();
        return array();
    }

    $result = $stmt->get_result();
    $rows = array();
    if ($result) {
        while ($row = $result->fetch_assoc()) {
            $rows[] = $row;
        }
        $result->free();
    }
    $stmt->close();

    return $rows;
}

function dashboard_bind_params($stmt, $types, $params)
{
    $bindParams = array($types);
    foreach ($params as $key => $value) {
        $bindParams[] = &$params[$key];
    }

    call_user_func_array(array($stmt, 'bind_param'), $bindParams);
}

function dashboard_money($value)
{
    return number_format((float) $value, 0, ',', '.') . 'đ';
}

function dashboard_money_short($value)
{
    return number_format((float) $value, 0, ',', '.');
}

function dashboard_number($value)
{
    return number_format((float) $value, 0, ',', '.');
}

function dashboard_initials($name)
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

function dashboard_status_meta($status)
{
    $status = strtolower(trim((string) $status));
    if ($status === 'hieu_luc') {
        return array('label' => 'Hiệu lực', 'class' => 'success');
    }
    if ($status === 'sap_het_han') {
        return array('label' => 'Sắp hết hạn', 'class' => 'warning');
    }
    if (in_array($status, array('het_han', 'huy', 'da_huy'), true)) {
        return array('label' => 'Không hiệu lực', 'class' => 'danger');
    }
    if (in_array($status, array('dang_hoat_dong', 'hoat_dong', 'da_thanh_toan', 'da_duyet', 'con_ban'), true)) {
        return array('label' => 'Hoạt động', 'class' => 'success');
    }
    if (in_array($status, array('cho_duyet', 'chua_thanh_toan', 'moi_tao', 'het_mon'), true)) {
        return array('label' => 'Chờ', 'class' => 'warning');
    }
    if (in_array($status, array('tam_ngung', 'tam_dung'), true)) {
        return array('label' => 'Tạm ngưng', 'class' => 'info');
    }
    return array('label' => 'Đóng', 'class' => 'danger');
}

function dashboard_activity_time($value)
{
    if (empty($value)) {
        return 'Chưa cập nhật';
    }

    $timestamp = strtotime((string) $value);
    if ($timestamp === false) {
        return 'Chưa cập nhật';
    }

    return date('d/m/Y H:i', $timestamp);
}

function dashboard_device_type_label($type)
{
    $type = strtolower(trim((string) $type));
    if ($type === 'app_client') {
        return 'App mobile';
    }
    if ($type === 'portal_web') {
        return 'Portal web';
    }
    if ($type === 'hardware') {
        return 'Thiết bị cứng';
    }

    return $type !== '' ? $type : '—';
}

function dashboard_token_preview($token)
{
    $token = trim((string) $token);
    if ($token === '') {
        return '—';
    }

    if (strlen($token) <= 16) {
        return $token;
    }

    return substr($token, 0, 8) . '...' . substr($token, -4);
}

function dashboard_token_status_value($status, $expiresAt)
{
    $status = strtolower(trim((string) $status));
    if ($status !== 'hieu_luc') {
        return $status !== '' ? $status : 'het_han';
    }

    $timestamp = strtotime((string) $expiresAt);
    if ($timestamp === false || $timestamp < time()) {
        return 'het_han';
    }

    if (($timestamp - time()) <= 86400) {
        return 'sap_het_han';
    }

    return 'hieu_luc';
}

function dashboard_chart_path($values, $width = 760, $height = 240)
{
    $count = count($values);
    if ($count === 0) {
        return 'M 0 ' . $height . ' L ' . $width . ' ' . $height;
    }

    $max = max($values);
    $max = $max > 0 ? $max : 1;
    $step = $count > 1 ? $width / ($count - 1) : $width;
    $points = array();

    foreach ($values as $index => $value) {
        $x = $count > 1 ? $index * $step : $width / 2;
        $y = $height - (((float) $value / $max) * ($height - 28)) - 12;
        $points[] = array(round($x, 2), round($y, 2));
    }

    if (count($points) === 1) {
        return 'M 0 ' . $points[0][1] . ' L ' . $width . ' ' . $points[0][1];
    }

    $path = 'M ' . $points[0][0] . ' ' . $points[0][1];
    for ($i = 1; $i < count($points); $i++) {
        $path .= ' L ' . $points[$i][0] . ' ' . $points[$i][1];
    }

    return $path;
}

function dashboard_chart_area_path($linePath, $width = 760, $height = 320)
{
    return $linePath . ' L ' . $width . ' ' . $height . ' L 0 ' . $height . ' Z';
}

function dashboard_percent($part, $total)
{
    $total = (float) $total;
    if ($total <= 0) {
        return 0;
    }

    return max(0, min(100, round(((float) $part / $total) * 100)));
}

$range = dashboard_selected_range();
$rangeOptions = dashboard_range_options();
$storeMonthOptions = dashboard_store_month_options();
$storeMonthRange = dashboard_selected_store_months();
$customStartDate = dashboard_normalize_date($_GET['start_date'] ?? '');
$customEndDate = dashboard_normalize_date($_GET['end_date'] ?? '');

if ($range === 'custom' && ($customStartDate === '' || $customEndDate === '')) {
    $range = '7';
}

if ($range === 'custom' && strtotime($customStartDate) > strtotime($customEndDate)) {
    $swapDate = $customStartDate;
    $customStartDate = $customEndDate;
    $customEndDate = $swapDate;
}

if ($range === 'custom') {
    $periodStartDate = $customStartDate;
    $periodEndDate = $customEndDate;
    $rangeDays = dashboard_days_between($periodStartDate, $periodEndDate);
    $periodLabel = date('d/m/Y', strtotime($periodStartDate)) . ' - ' . date('d/m/Y', strtotime($periodEndDate));
} else {
    $rangeDays = dashboard_days_for_range($range);
    $periodStartDate = $range === 'month'
        ? date('Y-m-01')
        : date('Y-m-d', strtotime('-' . ($rangeDays - 1) . ' days'));
    $periodEndDate = date('Y-m-d');
    $periodLabel = $rangeOptions[$range];
}

$periodStart = $periodStartDate . ' 00:00:00';
$periodEnd = $periodEndDate . ' 23:59:59';

$summary = array(
    'stores' => 0,
    'activeOwners' => 0,
    'foods' => 0,
    'visitorRevenue' => 0,
    'storeRevenue' => 0,
    'revenue' => 0,
    'averageVisitorOrder' => 0,
    'paidOrders' => 0,
    'pendingRequests' => 0,
    'activeDevices' => 0,
);
$chartLabels = array();
$chartValues = array();
$storeMonthLabels = array();
$storeMonthValues = array();
$storeMonthSeries = array();
$topStores = array();
$activities = array();
$activeTokenDevices = array();
$activeTokenDeviceCount = 0;
$onlineDeviceCount = 0;

$conn = admin_db_connection();
if (!$conn instanceof mysqli) {
    $dashboardError = 'Không thể kết nối CSDL để tải dashboard.';
} else {
    $paidStoreSubquery = dashboard_paid_store_subquery();

    // Lấy summary từ backend API (priority). Nếu API down → fallback DB local cho các số đã port.
    $summaryApiError = '';
    $apiSummary = dashboard_fetch_summary_from_api($idTaiKhoan, $summaryApiError);
    if (is_array($apiSummary)) {
        $summary['stores']          = (int) ($apiSummary['paidStores'] ?? 0);
        $summary['activeOwners']    = (int) ($apiSummary['activeOwners'] ?? 0);
        $summary['foods']           = (int) ($apiSummary['paidStoreFoods'] ?? 0);
        $summary['pendingRequests'] = (int) ($apiSummary['pendingRequests'] ?? 0);
        $summary['activeDevices']   = (int) ($apiSummary['onlineDevices'] ?? $apiSummary['thietBiDangHoatDong'] ?? 0);
    } else {
        $summary['stores']          = dashboard_count_paid_stores($conn);
        $summary['activeOwners']    = dashboard_count_active_owners($conn);
        $summary['foods']           = dashboard_count_paid_store_foods($conn);
        $summary['pendingRequests'] = dashboard_count_pending_requests($conn);
        $summary['activeDevices']   = dashboard_count_active_devices($conn);
    }
    $summary['paidOrders']      = dashboard_count_paid_orders_in_period($conn, $periodStart, $periodEnd);

    $visitorRevenue = (float) dashboard_query_value($conn, "
        SELECT COALESCE(SUM(tongTien), 0)
        FROM hoadon
        WHERE tinhTrang = 'da_thanh_toan'
          AND thoiGianTao >= ?
          AND thoiGianTao <= ?
    ", 'ss', array($periodStart, $periodEnd));
    $storeMonthlyFeeTotal = (float) dashboard_query_value($conn, "
        SELECT COALESCE(SUM(hdgh.tongTien), 0)
        FROM hoadongianhang hdgh
        INNER JOIN (" . $paidStoreSubquery . ") paid ON paid.idGianHang = hdgh.idGianHang
        WHERE hdgh.trangThai = 'da_thanh_toan'
          AND hdgh.ngayTao >= ?
          AND hdgh.ngayTao <= ?
    ", 'ss', array($periodStart, $periodEnd));

    $storeFeeRows = dashboard_query_rows($conn, "
        SELECT
            hdgh.idGianHang,
            COALESCE(hdgh.tongTien, 0) AS phiHangThang,
            hdgh.ngayTao AS ngayDangKy
        FROM hoadongianhang hdgh
        INNER JOIN gianhang gh ON gh.idGianHang = hdgh.idGianHang
        INNER JOIN (" . $paidStoreSubquery . ") paid ON paid.idGianHang = hdgh.idGianHang
        WHERE hdgh.trangThai = 'da_thanh_toan'
    ");

    $summary['visitorRevenue'] = $visitorRevenue;
    $summary['storeRevenue'] = $storeMonthlyFeeTotal;
    $summary['revenue'] = $visitorRevenue + $storeMonthlyFeeTotal;
    $summary['averageVisitorOrder'] = $summary['paidOrders'] > 0 ? $visitorRevenue / $summary['paidOrders'] : 0;

    $rawChartRows = dashboard_query_rows($conn, "
        SELECT DATE(thoiGianTao) AS ngay, COALESCE(SUM(tongTien), 0) AS tongTien
        FROM hoadon
        WHERE tinhTrang = 'da_thanh_toan'
          AND thoiGianTao >= ?
          AND thoiGianTao <= ?
        GROUP BY DATE(thoiGianTao)
        ORDER BY ngay
    ", 'ss', array($periodStart, $periodEnd));

    $chartByDay = array();
    foreach ($rawChartRows as $row) {
        $chartByDay[(string) $row['ngay']] = (float) $row['tongTien'];
    }

    $chartStartDate = new DateTime($periodStartDate);
    for ($i = 0; $i < $rangeDays; $i++) {
        $day = $chartStartDate->format('Y-m-d');
        $chartLabels[] = date('d/m', strtotime($day));
        $chartValues[] = isset($chartByDay[$day]) ? $chartByDay[$day] : 0;
        $chartStartDate->modify('+1 day');
    }

    for ($i = 11; $i >= 0; $i--) {
        $monthKey = date('Y-m', strtotime('-' . $i . ' months'));
        $monthLabel = date('m/Y', strtotime($monthKey . '-01'));
        $monthStartTimestamp = strtotime($monthKey . '-01 00:00:00');
        $monthEndTimestamp = strtotime(date('Y-m-t 23:59:59', strtotime($monthKey . '-01')));
        $monthValue = 0;

        foreach ($storeFeeRows as $row) {
            $paidAt = !empty($row['ngayDangKy']) ? strtotime((string) $row['ngayDangKy']) : false;
            if ($paidAt === false || $paidAt < $monthStartTimestamp || $paidAt > $monthEndTimestamp) {
                continue;
            }

            $monthValue += (float) ($row['phiHangThang'] ?? 0);
        }

        $storeMonthSeries[] = array(
            'label' => $monthLabel,
            'value' => $monthValue,
        );
    }

    $visibleStoreMonthSeries = array_slice($storeMonthSeries, -$storeMonthRange);
    foreach ($visibleStoreMonthSeries as $monthItem) {
        $storeMonthLabels[] = $monthItem['label'];
        $storeMonthValues[] = $monthItem['value'];
    }

    $topStores = dashboard_query_rows($conn, "
        SELECT
            gh.idGianHang,
            gh.ten,
            COALESCE(SUM(hdgh.tongTien), 0) AS phiHangThang,
            COUNT(DISTINCT ma.idMonAn) AS soMon
        FROM hoadongianhang hdgh
        INNER JOIN gianhang gh ON gh.idGianHang = hdgh.idGianHang
        INNER JOIN (" . $paidStoreSubquery . ") paid ON paid.idGianHang = gh.idGianHang
        LEFT JOIN monan ma ON ma.idGianHang = gh.idGianHang
        WHERE hdgh.trangThai = 'da_thanh_toan'
        GROUP BY gh.idGianHang, gh.ten
        ORDER BY phiHangThang DESC, gh.idGianHang ASC
        LIMIT 5
    ");

    $activities = dashboard_query_rows($conn, "
        SELECT *
        FROM (
            SELECT
                gh.idGianHang AS idRef,
                gh.ten AS tenGianHang,
                COALESCE(cql.hoTen, tk.username, tk.email, 'Chua gan chu') AS nguoiQuanLy,
                'Gian hang' AS nhom,
                CONCAT('Cập nhật thông tin: ', gh.tinhTrang) AS hoatDong,
                gh.phiHangThang AS soTien,
                gh.tinhTrang AS trangThai,
                COALESCE(gh.thoiGianCapNhat, gh.ngayDangKy) AS thoiGian
            FROM gianhang gh
            INNER JOIN (" . $paidStoreSubquery . ") paid ON paid.idGianHang = gh.idGianHang
            LEFT JOIN chu_quan_ly cql ON cql.idChuQuanLy = gh.idChuQuanLy
            LEFT JOIN taikhoan tk ON tk.idTaiKhoan = cql.idTaiKhoan

            UNION ALL

            SELECT
                ma.idGianHang AS idRef,
                gh.ten AS tenGianHang,
                ma.ten AS nguoiQuanLy,
                'Món ăn' AS nhom,
                CONCAT('Cập nhật món: ', ma.ten) AS hoatDong,
                ma.donGia AS soTien,
                ma.tinhTrang AS trangThai,
                COALESCE(ma.thoiGianCapNhat, gh.thoiGianCapNhat, gh.ngayDangKy) AS thoiGian
            FROM monan ma
            INNER JOIN gianhang gh ON gh.idGianHang = ma.idGianHang
            INNER JOIN (" . $paidStoreSubquery . ") paid ON paid.idGianHang = gh.idGianHang

            UNION ALL

            SELECT
                COALESCE(ycg.idGianHang, 0) AS idRef,
                ycg.tenDeNghi AS tenGianHang,
                COALESCE(cql.hoTen, tk.username, tk.email, 'Chủ quản lý') AS nguoiQuanLy,
                'Yêu cầu' AS nhom,
                'Gửi yêu cầu mở gian hàng' AS hoatDong,
                0 AS soTien,
                ycg.trangThai AS trangThai,
                ycg.ngayGui AS thoiGian
            FROM yeucaugianhang ycg
            INNER JOIN chu_quan_ly cql ON cql.idChuQuanLy = ycg.idChuQuanLy
            INNER JOIN taikhoan tk ON tk.idTaiKhoan = cql.idTaiKhoan

            UNION ALL

            SELECT
                COALESCE(hd.idGoi, 0) AS idRef,
                COALESCE(gdv.ten, 'Gói tham quan') AS tenGianHang,
                COALESCE(hd.email, CONCAT('Phiên #', hd.idPhienVaoApp), 'Du khách') AS nguoiQuanLy,
                'Du khách' AS nhom,
                CONCAT('Thanh toán ', COALESCE(gdv.ten, 'gói tham quan')) AS hoatDong,
                hd.tongTien AS soTien,
                hd.tinhTrang AS trangThai,
                hd.thoiGianTao AS thoiGian
            FROM hoadon hd
            LEFT JOIN goidichvu gdv ON gdv.idGoi = hd.idGoi
            WHERE hd.tinhTrang = 'da_thanh_toan'
        ) activity
        ORDER BY thoiGian DESC
        LIMIT 8
    ");

    $activeTokenDevices = dashboard_query_rows($conn, "
        SELECT
            tb.idThietBi,
            tb.maThietBi,
            tb.trangThai AS trangThaiThietBi,
            tb.loaiThietBi,
            tb.platform,
            tb.model,
            pva.accessToken,
            pva.batDauLuc,
            pva.hetHanLuc,
            pva.trangThai AS trangThaiToken,
            gdv.ten AS tenGoi,
            COALESCE(ad.hoTen, cql.hoTen, tk.username, tk.email, 'Chưa liên kết') AS tenChuSoHuu
        FROM phien_vao_app pva
        INNER JOIN (
            SELECT maThietBi, MAX(id) AS latestId
            FROM phien_vao_app
            GROUP BY maThietBi
        ) latest ON latest.latestId = pva.id
        LEFT JOIN thietbi tb ON tb.maThietBi = pva.maThietBi
        LEFT JOIN taikhoan tk ON tk.idTaiKhoan = tb.idTaiKhoan
        LEFT JOIN admin ad ON ad.idTaiKhoan = tk.idTaiKhoan
        LEFT JOIN chu_quan_ly cql ON cql.idTaiKhoan = tk.idTaiKhoan
        LEFT JOIN goidichvu gdv ON gdv.idGoi = pva.idGoi
        WHERE pva.trangThai = 'hieu_luc'
          AND pva.hetHanLuc >= NOW()
        ORDER BY pva.hetHanLuc ASC, tb.idThietBi DESC
        LIMIT 8
    ");

    $activeTokenDeviceCount = dashboard_count_devices_with_active_token($conn);
    $onlineDeviceCount = dashboard_count_online_devices($conn, 60);

    $conn->close();
}

// === OVERRIDE THỦ CÔNG ===
// Muốn sửa con số hiển thị trên card ở dashboard, bỏ dấu // trước dòng tương ứng và set giá trị mong muốn.
// Giá trị ở đây sẽ ĐÈ lên kết quả lấy từ DB. Muốn quay lại số thật thì comment dòng đó lại.
$summaryOverrides = array(
);
foreach ($summaryOverrides as $overrideKey => $overrideValue) {
    $summary[$overrideKey] = $overrideValue;
}
// === HẾT OVERRIDE ===

$linePath = dashboard_chart_path($chartValues);
$areaPath = dashboard_chart_area_path($linePath);
$visitorPeak = count($chartValues) > 0 ? max($chartValues) : 0;
$visitorMoneyAxisValues = $visitorPeak > 0
    ? array($visitorPeak, $visitorPeak * 0.66, $visitorPeak * 0.33, 0)
    : array(0, 0, 0, 0);
$storeMonthTotal = array_sum($storeMonthValues);
$storeMonthPeak = count($storeMonthValues) > 0 ? max($storeMonthValues) : 0;
$storeMonthMax = $storeMonthPeak > 0 ? $storeMonthPeak : 1;
$storeMonthLinePath = dashboard_chart_path($storeMonthValues);
$storeMonthAreaPath = dashboard_chart_area_path($storeMonthLinePath);
$storeMoneyAxisValues = $storeMonthPeak > 0
    ? array($storeMonthPeak, $storeMonthPeak * 0.66, $storeMonthPeak * 0.33, 0)
    : array(0, 0, 0, 0);
$visitorShare = dashboard_percent($summary['visitorRevenue'], $summary['revenue']);
$storeShare = dashboard_percent($summary['storeRevenue'], $summary['revenue']);
?>
<main class="main-content">
    <section class="page-header">
      <div>
        <h2>Tổng quan hệ thống</h2>
        <p>Dữ liệu trực tiếp từ cơ sở dữ liệu quản trị gian hàng.</p>
      </div>
      <form class="dashboard-range-form" method="get" action="<?php echo htmlspecialchars(admin_url('index1st.php'), ENT_QUOTES, 'UTF-8'); ?>">
        <input type="hidden" name="usecase" value="dashboard" />
        <input type="hidden" name="store_months" value="<?php echo (int) $storeMonthRange; ?>" />
        <label for="dashboard-range">Kỳ dữ liệu</label>
        <select id="dashboard-range" name="range" onchange="this.form.submit()">
          <?php foreach ($rangeOptions as $rangeKey => $rangeLabel) { ?>
          <?php if ($rangeKey === 'custom') { continue; } ?>
          <option value="<?php echo htmlspecialchars($rangeKey, ENT_QUOTES, 'UTF-8'); ?>" <?php echo (string) $range === (string) $rangeKey ? 'selected' : ''; ?>><?php echo htmlspecialchars($rangeLabel, ENT_QUOTES, 'UTF-8'); ?></option>
          <?php } ?>
        </select>
      </form>
    </section>

    <?php if ($dashboardError !== '') { ?>
    <div class="dashboard-alert"><?php echo htmlspecialchars($dashboardError, ENT_QUOTES, 'UTF-8'); ?></div>
    <?php } ?>

    <section class="stats-grid">
      <div class="stat-card">
        <div class="stat-top">
          <div class="stat-icon">
            <i class="fa-solid fa-shop"></i>
          </div>
          <span class="stat-growth live">DB</span>
        </div>
        <p class="stat-label">Gian hàng đã thanh toán</p>
        <h3><?php echo htmlspecialchars(dashboard_number($summary['stores']), ENT_QUOTES, 'UTF-8'); ?></h3>
      </div>

      <div class="stat-card">
        <div class="stat-top">
          <div class="stat-icon">
            <i class="fa-solid fa-users"></i>
          </div>
          <span class="stat-growth positive"><?php echo htmlspecialchars(dashboard_number($summary['pendingRequests']), ENT_QUOTES, 'UTF-8'); ?> chờ duyệt</span>
        </div>
        <p class="stat-label">Chủ quản lý đã thanh toán</p>
        <h3><?php echo htmlspecialchars(dashboard_number($summary['activeOwners']), ENT_QUOTES, 'UTF-8'); ?></h3>
      </div>

      <div class="stat-card">
        <div class="stat-top">
          <div class="stat-icon">
            <i class="fa-solid fa-mobile-screen-button"></i>
          </div>
          <span class="stat-growth live">Token</span>
        </div>
        <p class="stat-label">Thiết bị đang hoạt động</p>
        <h3><?php echo htmlspecialchars(dashboard_number($activeTokenDeviceCount), ENT_QUOTES, 'UTF-8'); ?></h3>
      </div>

      <div class="stat-card">
        <div class="stat-top">
          <div class="stat-icon">
            <i class="fa-solid fa-signal"></i>
          </div>
          <span class="stat-growth positive">Online</span>
        </div>
        <p class="stat-label">Thiết bị đang online (realtime)</p>
        <h3><?php echo htmlspecialchars(dashboard_number($onlineDeviceCount), ENT_QUOTES, 'UTF-8'); ?></h3>
      </div>

    </section>

    <section class="panel dashboard-token-panel">
      <div class="dashboard-token-header">
        <div>
          <h3>Thiết bị có token đang hoạt động</h3>
          <p>Ưu tiên theo phiên token mới nhất còn hiệu lực của từng thiết bị.</p>
        </div>
        <span class="stat-growth live"><?php echo htmlspecialchars(dashboard_number($activeTokenDeviceCount), ENT_QUOTES, 'UTF-8'); ?> thiết bị</span>
      </div>
      <div class="dashboard-token-body table-wrap">
        <table class="dashboard-token-table">
          <thead>
            <tr>
              <th>THIẾT BỊ</th>
              <th>CHỦ SỞ HỮU</th>
              <th>GÓI</th>
              <th>TOKEN</th>
              <th>HẾT HẠN</th>
              <th>TRẠNG THÁI TOKEN</th>
              <th>TRẠNG THÁI THIẾT BỊ</th>
            </tr>
          </thead>
          <tbody>
            <?php if (count($activeTokenDevices) === 0) { ?>
            <tr>
              <td colspan="7" class="empty-table">Chưa có thiết bị nào có token đang hoạt động.</td>
            </tr>
            <?php } ?>
            <?php foreach ($activeTokenDevices as $deviceToken) { ?>
            <?php
              $tokenMeta = dashboard_status_meta(dashboard_token_status_value($deviceToken['trangThaiToken'] ?? '', $deviceToken['hetHanLuc'] ?? ''));
              $deviceMeta = dashboard_status_meta($deviceToken['trangThaiThietBi'] ?? '');
              $typeLabel = dashboard_device_type_label($deviceToken['loaiThietBi'] ?? '');
              $platform = trim((string) ($deviceToken['platform'] ?? ''));
              $model = trim((string) ($deviceToken['model'] ?? ''));
              $deviceMetaLine = $typeLabel
                . ($platform !== '' ? ' • ' . $platform : '')
                . ($model !== '' ? ' • ' . $model : '');
            ?>
            <tr>
              <td>
                <div class="token-device-cell">
                  <span class="device-code"><?php echo htmlspecialchars((string) ($deviceToken['maThietBi'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></span>
                  <small><?php echo htmlspecialchars($deviceMetaLine, ENT_QUOTES, 'UTF-8'); ?></small>
                </div>
              </td>
              <td><?php echo htmlspecialchars((string) ($deviceToken['tenChuSoHuu'] ?? 'Chưa liên kết'), ENT_QUOTES, 'UTF-8'); ?></td>
              <td><?php echo htmlspecialchars((string) ($deviceToken['tenGoi'] ?? 'Gói truy cập'), ENT_QUOTES, 'UTF-8'); ?></td>
              <td><span class="token-preview"><?php echo htmlspecialchars(dashboard_token_preview($deviceToken['accessToken'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></span></td>
              <td><?php echo htmlspecialchars(dashboard_activity_time($deviceToken['hetHanLuc'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></td>
              <td>
                <span class="status-badge <?php echo htmlspecialchars($tokenMeta['class'], ENT_QUOTES, 'UTF-8'); ?>">
                  <?php echo htmlspecialchars($tokenMeta['label'], ENT_QUOTES, 'UTF-8'); ?>
                </span>
              </td>
              <td>
                <span class="status-badge <?php echo htmlspecialchars($deviceMeta['class'], ENT_QUOTES, 'UTF-8'); ?>">
                  <?php echo htmlspecialchars($deviceMeta['label'], ENT_QUOTES, 'UTF-8'); ?>
                </span>
              </td>
            </tr>
            <?php } ?>
          </tbody>
        </table>
      </div>
    </section>

    <section class="revenue-grid">
      <div class="panel chart-panel visitor-chart-panel">
        <div class="panel-header">
          <div>
            <h3>Doanh thu du khách</h3>
            <p>Hóa đơn gói tham quan - <?php echo htmlspecialchars($periodLabel, ENT_QUOTES, 'UTF-8'); ?></p>
          </div>

          <form class="visitor-filter-form" method="get" action="<?php echo htmlspecialchars(admin_url('index1st.php'), ENT_QUOTES, 'UTF-8'); ?>">
            <input type="hidden" name="usecase" value="dashboard" />
            <input type="hidden" name="store_months" value="<?php echo (int) $storeMonthRange; ?>" />
            <input type="hidden" name="range" value="<?php echo htmlspecialchars($range, ENT_QUOTES, 'UTF-8'); ?>" />
            <select class="select-btn" onchange="this.form.elements.range.value=this.value; this.form.submit()">
              <?php foreach ($rangeOptions as $rangeKey => $rangeLabel) { ?>
              <?php if ($rangeKey === 'custom') { continue; } ?>
              <option value="<?php echo htmlspecialchars($rangeKey, ENT_QUOTES, 'UTF-8'); ?>" <?php echo (string) $range === (string) $rangeKey ? 'selected' : ''; ?>><?php echo htmlspecialchars($rangeLabel, ENT_QUOTES, 'UTF-8'); ?></option>
              <?php } ?>
            </select>
            <label>
              <span>Từ</span>
              <input type="date" name="start_date" value="<?php echo htmlspecialchars($periodStartDate, ENT_QUOTES, 'UTF-8'); ?>" />
            </label>
            <label>
              <span>Đến</span>
              <input type="date" name="end_date" value="<?php echo htmlspecialchars($periodEndDate, ENT_QUOTES, 'UTF-8'); ?>" />
            </label>
            <button type="submit" onclick="this.form.elements.range.value='custom'">
              <i class="fa-solid fa-filter"></i>
              <span>Lọc</span>
            </button>
          </form>
        </div>

        <div class="chart-summary-row">
          <div>
            <span>Tổng doanh thu du khách</span>
            <strong><?php echo htmlspecialchars(dashboard_money($summary['visitorRevenue']), ENT_QUOTES, 'UTF-8'); ?></strong>
          </div>
          <div>
            <span>Hóa đơn đã thanh toán</span>
            <strong><?php echo htmlspecialchars(dashboard_number($summary['paidOrders']), ENT_QUOTES, 'UTF-8'); ?></strong>
          </div>
          <div>
            <span>Trung bình / hóa đơn</span>
            <strong><?php echo htmlspecialchars(dashboard_money($summary['averageVisitorOrder']), ENT_QUOTES, 'UTF-8'); ?></strong>
          </div>
        </div>

        <div class="visitor-chart-layout">
          <div class="store-money-axis">
            <?php foreach ($visitorMoneyAxisValues as $axisValue) { ?>
            <span><?php echo htmlspecialchars(dashboard_money_short($axisValue), ENT_QUOTES, 'UTF-8'); ?></span>
            <?php } ?>
          </div>
          <div class="chart-area">
            <div class="chart-grid-line line-1"></div>
            <div class="chart-grid-line line-2"></div>
            <div class="chart-grid-line line-3"></div>

            <svg viewBox="0 0 760 320" preserveAspectRatio="none" class="chart-svg">
              <defs>
                <linearGradient id="areaFill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stop-color="#27d3d2" stop-opacity="0.28" />
                  <stop offset="100%" stop-color="#27d3d2" stop-opacity="0.02" />
                </linearGradient>
              </defs>

              <path d="<?php echo htmlspecialchars($areaPath, ENT_QUOTES, 'UTF-8'); ?>" fill="url(#areaFill)"></path>
              <path d="<?php echo htmlspecialchars($linePath, ENT_QUOTES, 'UTF-8'); ?>" fill="none" stroke="#18cfd0" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"></path>
            </svg>

            <div class="chart-labels">
              <?php
              $labelStep = max(1, (int) ceil(count($chartLabels) / 7));
              foreach ($chartLabels as $index => $label) {
                  if ($index % $labelStep !== 0 && $index !== count($chartLabels) - 1) {
                      continue;
                  }
              ?>
              <span><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></span>
              <?php } ?>
            </div>
          </div>
        </div>
      </div>

      <div class="panel revenue-panel">
        <div class="panel-header simple">
          <div>
            <h3>Cơ cấu doanh thu</h3>
            <p><?php echo htmlspecialchars($periodLabel, ENT_QUOTES, 'UTF-8'); ?></p>
          </div>
        </div>

        <div class="revenue-total">
          <span>Tổng doanh thu</span>
          <strong><?php echo htmlspecialchars(dashboard_money($summary['revenue']), ENT_QUOTES, 'UTF-8'); ?></strong>
        </div>

        <div class="revenue-breakdown">
          <div class="revenue-breakdown-item">
            <div class="revenue-breakdown-head">
              <span><i class="fa-solid fa-ticket"></i> Du khách</span>
              <strong><?php echo htmlspecialchars(dashboard_money($summary['visitorRevenue']), ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <div class="revenue-progress">
              <span style="width:<?php echo (int) $visitorShare; ?>%"></span>
            </div>
            <small><?php echo (int) $visitorShare; ?>% tổng doanh thu</small>
          </div>

          <div class="revenue-breakdown-item store">
            <div class="revenue-breakdown-head">
              <span><i class="fa-solid fa-store"></i> Gian hàng</span>
              <strong><?php echo htmlspecialchars(dashboard_money($summary['storeRevenue']), ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <div class="revenue-progress">
              <span style="width:<?php echo (int) $storeShare; ?>%"></span>
            </div>
            <small><?php echo (int) $storeShare; ?>% tổng doanh thu</small>
          </div>
        </div>

        <div class="revenue-mini-grid">
          <div>
            <span>Thiết bị hoạt động</span>
            <strong><?php echo htmlspecialchars(dashboard_number($summary['activeDevices']), ENT_QUOTES, 'UTF-8'); ?></strong>
          </div>
          <div>
            <span>Yêu cầu chờ duyệt</span>
            <strong><?php echo htmlspecialchars(dashboard_number($summary['pendingRequests']), ENT_QUOTES, 'UTF-8'); ?></strong>
          </div>
        </div>
      </div>
    </section>

    <section class="overview-grid">
      <div class="panel rank-panel">
        <div class="panel-header simple">
          <div>
            <h3>Hóa đơn gian hàng đã thanh toán</h3>
          </div>
        </div>

        <div class="rank-list">
          <?php if (count($topStores) === 0) { ?>
          <p class="empty-note">Chưa có dữ liệu gian hàng.</p>
          <?php } ?>
          <?php foreach ($topStores as $index => $store) { ?>
          <div class="rank-item">
            <div class="rank-badge"><?php echo $index + 1; ?></div>
            <div class="rank-info">
              <h4><?php echo htmlspecialchars((string) ($store['ten'] ?? 'Gian hàng'), ENT_QUOTES, 'UTF-8'); ?></h4>
              <p><?php echo htmlspecialchars(dashboard_number($store['soMon'] ?? 0), ENT_QUOTES, 'UTF-8'); ?> món - đã thanh toán <?php echo htmlspecialchars(dashboard_money($store['phiHangThang'] ?? 0), ENT_QUOTES, 'UTF-8'); ?></p>
            </div>
            <strong><?php echo htmlspecialchars(dashboard_money($store['phiHangThang'] ?? 0), ENT_QUOTES, 'UTF-8'); ?></strong>
          </div>
          <?php } ?>
        </div>

        <button class="outline-btn" type="button" onclick="window.location.href='<?php echo htmlspecialchars(admin_url('index1st.php?usecase=store'), ENT_QUOTES, 'UTF-8'); ?>'">Xem danh sách gian hàng</button>
      </div>

      <div class="panel store-month-panel" data-store-month-chart>
        <div class="panel-header">
          <div>
            <h3>Hóa đơn gian hàng đã thanh toán</h3>
            <p>Tổng hóa đơn gian hàng đã thanh toán theo kỳ đang chọn.</p>
          </div>
          <div class="store-month-form">
            <label for="store-months">Kỳ tháng</label>
            <select id="store-months" class="select-btn" name="store_months" data-store-month-select>
              <?php foreach ($storeMonthOptions as $monthValue => $monthLabel) { ?>
              <option value="<?php echo (int) $monthValue; ?>" <?php echo $storeMonthRange === (int) $monthValue ? 'selected' : ''; ?>><?php echo htmlspecialchars($monthLabel, ENT_QUOTES, 'UTF-8'); ?></option>
              <?php } ?>
            </select>
          </div>
        </div>

        <div class="store-month-summary">
          <div>
            <span>Tổng kỳ chọn</span>
            <strong data-store-month-total><?php echo htmlspecialchars(dashboard_money($storeMonthTotal), ENT_QUOTES, 'UTF-8'); ?></strong>
          </div>
          <div>
            <span>Tháng cao nhất</span>
            <strong data-store-month-peak><?php echo htmlspecialchars(dashboard_money($storeMonthPeak), ENT_QUOTES, 'UTF-8'); ?></strong>
          </div>
          <div>
            <span>Trung bình / tháng</span>
            <strong data-store-month-average><?php echo htmlspecialchars(dashboard_money(count($storeMonthValues) > 0 ? $storeMonthTotal / count($storeMonthValues) : 0), ENT_QUOTES, 'UTF-8'); ?></strong>
          </div>
        </div>

        <div class="store-line-chart" aria-label="Doanh thu gian hàng theo tháng">
          <div class="store-money-axis" data-store-money-axis>
            <?php foreach ($storeMoneyAxisValues as $axisValue) { ?>
            <span><?php echo htmlspecialchars(dashboard_money_short($axisValue), ENT_QUOTES, 'UTF-8'); ?></span>
            <?php } ?>
          </div>
          <div class="store-line-plot">
            <div class="chart-grid-line line-1"></div>
            <div class="chart-grid-line line-2"></div>
            <div class="chart-grid-line line-3"></div>

            <svg viewBox="0 0 760 320" preserveAspectRatio="none" class="chart-svg store-chart-svg" aria-hidden="true">
              <defs>
                <linearGradient id="storeLineAreaFill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stop-color="#27d3d2" stop-opacity="0.24" />
                  <stop offset="100%" stop-color="#27d3d2" stop-opacity="0.02" />
                </linearGradient>
              </defs>

              <path data-store-area-path d="<?php echo htmlspecialchars($storeMonthAreaPath, ENT_QUOTES, 'UTF-8'); ?>" fill="url(#storeLineAreaFill)"></path>
              <path data-store-line-path d="<?php echo htmlspecialchars($storeMonthLinePath, ENT_QUOTES, 'UTF-8'); ?>" fill="none" stroke="#18cfd0" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"></path>
            </svg>

            <div class="store-line-points" data-store-line-points></div>

            <div class="chart-labels store-month-labels" data-store-month-labels>
              <?php foreach ($storeMonthLabels as $label) { ?>
              <span><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></span>
              <?php } ?>
            </div>
          </div>
        </div>
      </div>
    </section>

    <script>
    (function(){
      var series = <?php echo json_encode($storeMonthSeries, JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_AMP | JSON_HEX_QUOT); ?>;
      var root = document.querySelector('[data-store-month-chart]');
      if (!root || !Array.isArray(series)) {
        return;
      }

      var select = root.querySelector('[data-store-month-select]');
      var areaPath = root.querySelector('[data-store-area-path]');
      var linePath = root.querySelector('[data-store-line-path]');
      var axisNode = root.querySelector('[data-store-money-axis]');
      var labelsNode = root.querySelector('[data-store-month-labels]');
      var pointsNode = root.querySelector('[data-store-line-points]');
      var totalNode = root.querySelector('[data-store-month-total]');
      var peakNode = root.querySelector('[data-store-month-peak]');
      var averageNode = root.querySelector('[data-store-month-average]');
      var chartWidth = 760;
      var chartHeight = 240;
      var areaHeight = 320;

      function formatMoney(value) {
        return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(Number(value) || 0) + 'đ';
      }

      function formatMoneyShort(value) {
        return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(Number(value) || 0);
      }

      function lineChartPath(values, maxValue) {
        if (!values.length) {
          return 'M 0 ' + chartHeight + ' L ' + chartWidth + ' ' + chartHeight;
        }

        var step = values.length > 1 ? chartWidth / (values.length - 1) : chartWidth;
        var points = values.map(function(value, index) {
          var x = values.length > 1 ? index * step : chartWidth / 2;
          var y = chartHeight - ((value / maxValue) * (chartHeight - 28)) - 12;
          return [Math.round(x * 100) / 100, Math.round(y * 100) / 100];
        });

        if (points.length === 1) {
          return 'M 0 ' + points[0][1] + ' L ' + chartWidth + ' ' + points[0][1];
        }

        return points.reduce(function(path, point, index) {
          return path + (index === 0 ? 'M ' : ' L ') + point[0] + ' ' + point[1];
        }, '');
      }

      function updateMoneyAxis(peak) {
        if (!axisNode) {
          return;
        }

        var values = peak > 0 ? [peak, peak * 0.66, peak * 0.33, 0] : [0, 0, 0, 0];
        axisNode.innerHTML = '';
        values.forEach(function(value) {
          var label = document.createElement('span');
          label.textContent = formatMoneyShort(value);
          axisNode.appendChild(label);
        });
      }

      function renderLabels(visible) {
        if (!labelsNode) {
          return;
        }

        labelsNode.innerHTML = '';
        visible.forEach(function(item) {
          var label = document.createElement('span');
          label.textContent = item.label;
          labelsNode.appendChild(label);
        });
      }

      function renderPoints(visible, maxValue) {
        if (!pointsNode) {
          return;
        }

        pointsNode.innerHTML = '';
        visible.forEach(function(item, index) {
          var value = Number(item.value) || 0;
          var x = visible.length > 1 ? (index / (visible.length - 1)) * 100 : 50;
          var y = chartHeight - ((value / maxValue) * (chartHeight - 28)) - 12;
          var point = document.createElement('button');
          var tooltip = document.createElement('span');

          point.type = 'button';
          point.className = 'store-line-point';
          point.style.left = x + '%';
          point.style.top = ((y / chartHeight) * 100) + '%';
          point.setAttribute('aria-label', item.label + ': ' + formatMoney(value));

          tooltip.className = 'store-line-tooltip';
          tooltip.textContent = item.label + ' - ' + formatMoney(value);
          point.appendChild(tooltip);
          pointsNode.appendChild(point);
        });
      }

      function updateLineChart(visible, maxValue) {
        var values = visible.map(function(item) {
          return Number(item.value) || 0;
        });
        var line = lineChartPath(values, maxValue);
        if (linePath) {
          linePath.setAttribute('d', line);
        }
        if (areaPath) {
          areaPath.setAttribute('d', line + ' L ' + chartWidth + ' ' + areaHeight + ' L 0 ' + areaHeight + ' Z');
        }
        renderLabels(visible);
        renderPoints(visible, maxValue);
      }

      function renderStoreChart() {
        var months = parseInt(select ? select.value : '6', 10) || 6;
        var visible = series.slice(-months);
        var total = visible.reduce(function(sum, item) {
          return sum + (Number(item.value) || 0);
        }, 0);
        var peak = visible.reduce(function(max, item) {
          return Math.max(max, Number(item.value) || 0);
        }, 0);
        var maxValue = peak > 0 ? peak : 1;

        if (totalNode) {
          totalNode.textContent = formatMoney(total);
        }
        if (peakNode) {
          peakNode.textContent = formatMoney(peak);
        }
        if (averageNode) {
          averageNode.textContent = formatMoney(visible.length > 0 ? total / visible.length : 0);
        }

        document.querySelectorAll('input[name="store_months"]').forEach(function(input) {
          input.value = months;
        });

        updateMoneyAxis(peak);
        updateLineChart(visible, maxValue);
      }

      if (select) {
        select.addEventListener('change', renderStoreChart);
      }
      renderStoreChart();
    })();
    </script>

    <section class="panel table-panel">
      <div class="table-header">
        <h3>Hoạt động gần đây</h3>
        <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=dashboard&range=' . rawurlencode($range)), ENT_QUOTES, 'UTF-8'); ?>">Làm mới</a>
      </div>

      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>GIAN HÀNG</th>
              <th>NGƯỜI LIÊN QUAN</th>
              <th>NHÓM</th>
              <th>HOẠT ĐỘNG</th>
              <th>GIÁ TRỊ</th>
              <th>TRẠNG THÁI</th>
              <th>THỜI GIAN</th>
            </tr>
          </thead>

          <tbody>
            <?php if (count($activities) === 0) { ?>
            <tr>
              <td colspan="7" class="empty-table">Chưa có hoạt động nào.</td>
            </tr>
            <?php } ?>
            <?php foreach ($activities as $index => $activity) { ?>
            <?php $statusMeta = dashboard_status_meta($activity['trangThai'] ?? ''); ?>
            <tr>
              <td class="booth-id">#GH-<?php echo str_pad((string) (int) ($activity['idRef'] ?? 0), 3, '0', STR_PAD_LEFT); ?></td>
              <td>
                <div class="vendor-cell">
                  <div class="avatar avatar-<?php echo ($index % 4) + 1; ?>"><?php echo htmlspecialchars(dashboard_initials($activity['nguoiQuanLy'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></div>
                  <span><?php echo htmlspecialchars((string) ($activity['nguoiQuanLy'] ?? 'Hệ thống'), ENT_QUOTES, 'UTF-8'); ?></span>
                </div>
              </td>
              <td><?php echo htmlspecialchars((string) ($activity['nhom'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></td>
              <td><?php echo htmlspecialchars((string) ($activity['hoatDong'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></td>
              <td class="money"><?php echo htmlspecialchars(dashboard_money($activity['soTien'] ?? 0), ENT_QUOTES, 'UTF-8'); ?></td>
              <td><span class="status-badge <?php echo htmlspecialchars($statusMeta['class'], ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($statusMeta['label'], ENT_QUOTES, 'UTF-8'); ?></span></td>
              <td><?php echo htmlspecialchars(dashboard_activity_time($activity['thoiGian'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></td>
            </tr>
            <?php } ?>
          </tbody>
        </table>
      </div>
    </section>
</main>
