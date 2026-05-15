<?php
require_once dirname(__DIR__) . '/connect.php';

// Support both Casso Webhook and GET parameters for testing
$body = file_get_contents('php://input');
$payload = json_decode($body, true);

// Extract transactions
$transactions = [];

if (isset($payload['data']) && is_array($payload['data'])) {
    // Casso webhook format
    $transactions = $payload['data'];
} elseif (isset($_GET['content']) && isset($_GET['amount'])) {
    // Testing via GET
    $transactions[] = [
        'description' => $_GET['content'],
        'amount' => (float)$_GET['amount']
    ];
}

if (empty($transactions)) {
    http_response_code(400);
    echo json_encode(['success' => false, 'message' => 'No valid transactions found']);
    exit;
}

$conn = admin_db_connection();
if (!$conn) {
    http_response_code(500);
    echo json_encode(['success' => false, 'message' => 'Database connection failed']);
    exit;
}

$processedCount = 0;

foreach ($transactions as $tx) {
    $content = isset($tx['description']) ? $tx['description'] : '';
    $amount = isset($tx['amount']) ? (float)$tx['amount'] : 0;

    if ($amount <= 0 || $content === '') continue;

    // Extract HDGH number
    if (preg_match('/HDGH0*(\d+)/i', $content, $matches)) {
        $invoiceId = (int) $matches[1];
        
        $stmt = $conn->prepare("UPDATE hoadongianhang SET trangThai = 'da_thanh_toan' WHERE idHoaDonGianHang = ? AND tongTien <= ? AND trangThai = 'chua_thanh_toan'");
        $stmt->bind_param('id', $invoiceId, $amount);
        $stmt->execute();
        
        if ($stmt->affected_rows > 0) {
            // Also update yeucaugianhang to 'da_duyet' if it was 'cho_thanh_toan'
            $stmt2 = $conn->prepare("UPDATE yeucaugianhang SET trangThai = 'da_duyet' WHERE idGianHang = (SELECT idGianHang FROM hoadongianhang WHERE idHoaDonGianHang = ?) AND trangThai = 'cho_thanh_toan'");
            $stmt2->bind_param('i', $invoiceId);
            $stmt2->execute();
            $stmt2->close();
            
            // Update gianhang to 'dang_hoat_dong'
            $stmt3 = $conn->prepare("UPDATE gianhang SET tinhTrang = 'dang_hoat_dong' WHERE idGianHang = (SELECT idGianHang FROM hoadongianhang WHERE idHoaDonGianHang = ?)");
            $stmt3->bind_param('i', $invoiceId);
            $stmt3->execute();
            $stmt3->close();
            
            $processedCount++;
        }
        $stmt->close();
    }
}

$conn->close();

echo json_encode([
    'success' => true, 
    'message' => "Successfully processed $processedCount transaction(s)",
    'error' => 0 // Casso requires error = 0 to acknowledge success
]);
