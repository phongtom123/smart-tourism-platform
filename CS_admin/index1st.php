<?php
require_once __DIR__ . '/connect.php';
session_start();

if (!isset($_SESSION['admin_auth']) || empty($_SESSION['admin_auth']['isLoggedIn'])) {
    header('Location: ' . admin_url('auth.php?mode=login'));
    exit;
}

if (!headers_sent() && ob_get_level() === 0) {
    ob_start();
}

$storeInvoiceMaintenanceError = '';
admin_run_store_invoice_maintenance($storeInvoiceMaintenanceError);

$accountRole = isset($_SESSION['admin_auth']['loaiTaiKhoan']) ? $_SESSION['admin_auth']['loaiTaiKhoan'] : 'admin';
$useCase = isset($_GET['usecase']) ? $_GET['usecase'] : ($accountRole === 'chu_quan_ly' ? 'store' : 'dashboard');

$availableUseCases = array(
    'dashboard' => array(
        'title' => 'Dashboard Admin',
        'active' => 'dashboard',
        'view' => __DIR__ . '/admin/dashboard.php',
        'styles' => array('asset/admin/css/dashboard.css'),
    ),
    'store' => array(
        'title' => 'Quản lý gian hàng',
        'active' => 'store',
        'view' => __DIR__ . '/admin/store.php',
        'styles' => array('asset/admin/css/store-content.css'),
    ),
    'poi-map' => array(
        'title' => 'Bản đồ POI 3D',
        'active' => 'poi-map',
        'view' => __DIR__ . '/admin/poi_map.php',
        'styles' => array('asset/admin/css/poi-map-content.css'),
    ),
    'branchdetail2' => array(
        'title' => 'Chi tiết gian hàng',
        'active' => 'store',
        'view' => __DIR__ . '/admin/branchdetail2.php',
        'styles' => array('asset/admin/css/branchdetail2-content.css'),
    ),
    'menu' => array(
        'title' => 'Quản lý món ăn',
        'active' => 'store',
        'view' => __DIR__ . '/admin/menu.php',
        'styles' => array('asset/admin/css/menu-content.css'),
    ),
    'request' => array(
        'title' => 'Yêu cầu gian hàng',
        'active' => 'request',
        'view' => __DIR__ . '/admin/request.php',
        'styles' => array('asset/admin/css/request-content.css'),
    ),
    'invoice' => array(
        'title' => 'Hóa đơn gian hàng',
        'active' => 'invoice',
        'view' => __DIR__ . '/admin/invoice.php',
        'styles' => array('asset/admin/css/request-content.css'),
    ),
    'invoice-payment' => array(
        'title' => 'Thanh toán hóa đơn gian hàng',
        'active' => 'invoice',
        'view' => __DIR__ . '/admin/invoice_payment.php',
        'styles' => array('asset/admin/css/invoice-payment-content.css'),
    ),
    'account' => array(
        'title' => 'Quản lý tài khoản',
        'active' => 'account',
        'view' => __DIR__ . '/admin/account.php',
        'styles' => array('asset/admin/css/account-content.css'),
    ),
    'service' => array(
        'title' => 'Quản lý dịch vụ',
        'active' => 'service',
        'view' => __DIR__ . '/admin/service.php',
        'styles' => array('asset/admin/css/service-content.css'),
    ),
    'device' => array(
        'title' => 'Quản lý thiết bị',
        'active' => 'device',
        'view' => __DIR__ . '/admin/device.php',
        'styles' => array('asset/admin/css/device-content.css'),
    ),
    'tour' => array(
        'title' => 'Quản lý tour',
        'active' => 'tour',
        'view' => __DIR__ . '/admin/tour.php',
        'styles' => array('asset/admin/css/tour-content.css'),
    ),
    'report' => array(
        'title' => 'Báo cáo',
        'active' => 'report',
        'view' => __DIR__ . '/admin/dashboard.php',
        'styles' => array('asset/admin/css/dashboard.css'),
    ),
);

if ($accountRole === 'chu_quan_ly') {
    $allowedUseCases = array('store', 'poi-map', 'branchdetail2', 'request', 'invoice', 'invoice-payment', 'menu');
    if (!in_array($useCase, $allowedUseCases, true)) {
        $useCase = 'store';
    }
}

if (!isset($availableUseCases[$useCase])) {
    $useCase = 'store';
}

$currentPage = $availableUseCases[$useCase];
$sidebarActive = $currentPage['active'];
$pageStyles = isset($currentPage['styles']) && is_array($currentPage['styles']) ? $currentPage['styles'] : array();

if ($sidebarActive === 'dashboard' && !headers_sent()) {
    header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');
    header('Pragma: no-cache');
    header('Expires: 0');
}
?>
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title><?php echo htmlspecialchars($currentPage['title'], ENT_QUOTES, 'UTF-8'); ?></title>
    <link rel="stylesheet" href="asset/admin/css/root.css" />
    <link rel="stylesheet" href="asset/admin/css/sidebar.css" />
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.0/css/all.min.css" />
    <?php foreach ($pageStyles as $pageStyle) { ?>
    <?php
    $resolvedStylePath = __DIR__ . '/' . str_replace('/', DIRECTORY_SEPARATOR, $pageStyle);
    $styleHref = $pageStyle;
    if (is_file($resolvedStylePath)) {
        $styleHref .= '?v=' . filemtime($resolvedStylePath);
    }
    ?>
    <link rel="stylesheet" href="<?php echo htmlspecialchars($styleHref, ENT_QUOTES, 'UTF-8'); ?>" />
    <?php } ?>
    <style>
        html,
        body {
            height: 100vh;
            overflow: hidden;
        }

        .dashboard-shell {
            height: 100vh;
            display: grid;
            grid-template-columns: 250px minmax(0, 1fr);
            overflow: hidden;
        }

        .dashboard-shell > .sidebar {
            height: 100vh;
            overflow-y: auto;
            overflow-x: hidden;
        }

        .dashboard-shell > .main-content {
            height: 100vh;
            overflow-y: auto;
            overflow-x: hidden;
        }

        @media (max-width: 1024px) {
            .dashboard-shell {
                grid-template-columns: 78px 1fr;
            }
        }
    </style>
</head>
<body>
    <div class="dashboard-shell">
        <aside class="sidebar">
            <?php include __DIR__ . '/asset/admin/components/sidebar.php'; ?>
        </aside>
        <?php include $currentPage['view']; ?>
    </div>
</body>
</html>
