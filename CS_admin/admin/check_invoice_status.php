<?php
require_once dirname(__DIR__) . '/connect.php';

header('Content-Type: application/json');

$invoiceId = isset($_GET['id']) ? (int) $_GET['id'] : 0;
if ($invoiceId <= 0) {
    echo json_encode(['status' => 'error']);
    exit;
}

$apiError = '';
$apiHttpCode = 0;
$result = admin_api_call(
    'GET',
    'Admin/invoices/' . rawurlencode((string) $invoiceId) . '/status',
    null,
    $apiError,
    $apiHttpCode
);

if (!is_array($result) || !isset($result['trangThai'])) {
    echo json_encode(['status' => 'error']);
    exit;
}

echo json_encode(['status' => (string) $result['trangThai']]);
