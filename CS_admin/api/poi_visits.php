<?php
require_once dirname(__DIR__) . '/connect.php';

header('Content-Type: application/json');

$idGianHang = isset($_GET['id']) ? (int) $_GET['id'] : 0;
if ($idGianHang <= 0) {
    echo json_encode(array('error' => 'Invalid ID'));
    exit;
}

$session = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($session['idTaiKhoan']) ? (int) $session['idTaiKhoan'] : 0;

$apiError = '';
$apiHttpCode = 0;
$result = $idTaiKhoan > 0
    ? admin_api_call(
        'GET',
        'Admin/stores/' . rawurlencode((string) $idGianHang) . '/daily-visits',
        null,
        $apiError,
        $apiHttpCode,
        array('idTaiKhoan' => $idTaiKhoan)
    )
    : null;

if (!is_array($result)) {
    echo json_encode(array('error' => $apiError !== '' ? $apiError : 'Backend API not available'));
    exit;
}

echo json_encode($result);
