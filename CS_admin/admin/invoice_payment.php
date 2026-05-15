<?php
$auth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($auth['idTaiKhoan']) ? (int) $auth['idTaiKhoan'] : 0;
$isOwnerInvoiceViewer = isset($auth['loaiTaiKhoan']) && $auth['loaiTaiKhoan'] === 'chu_quan_ly';
$invoiceId = isset($_GET['idHoaDonGianHang']) ? (int) $_GET['idHoaDonGianHang'] : 0;
$paymentError = '';

function invoice_payment_money($value)
{
    return number_format((float) $value, 0, ',', '.') . 'đ';
}

function invoice_payment_datetime($value)
{
    if (empty($value)) {
        return 'Chưa có';
    }

    $timestamp = strtotime((string) $value);
    return $timestamp === false ? 'Chưa có' : date('d/m/Y H:i', $timestamp);
}

function invoice_payment_text($value, $emptyText = 'Chưa có')
{
    $value = trim((string) $value);
    return $value !== '' ? $value : $emptyText;
}

function invoice_payment_redirect($url)
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

function invoice_payment_settings()
{
    $settings = array(
        'payOsEndpoint' => '',
        'payOsClientId' => '',
        'payOsApiKey' => '',
        'payOsChecksumKey' => '',
        'payOsReturnUrl' => '',
        'payOsCancelUrl' => '',
    );

    $appSettingsPath = dirname(__DIR__, 2) . '/VinhKhanh/VinhKhanh/appsettings.json';
    if (!is_file($appSettingsPath)) {
        return $settings;
    }

    $raw = file_get_contents($appSettingsPath);
    $decoded = is_string($raw) ? json_decode($raw, true) : null;
    if (!is_array($decoded)) {
        return $settings;
    }

    $payOs = array();
    if (!empty($decoded['Payment']['PayOS']) && is_array($decoded['Payment']['PayOS'])) {
      $payOs = $decoded['Payment']['PayOS'];
    }

    $settings['payOsEndpoint'] = isset($payOs['Endpoint']) ? trim((string) $payOs['Endpoint']) : 'https://api-merchant.payos.vn';
    if ($settings['payOsEndpoint'] === '') {
      $settings['payOsEndpoint'] = 'https://api-merchant.payos.vn';
    }
    $settings['payOsClientId'] = isset($payOs['ClientId']) ? trim((string) $payOs['ClientId']) : '';
    $settings['payOsApiKey'] = isset($payOs['ApiKey']) ? trim((string) $payOs['ApiKey']) : '';
    $settings['payOsChecksumKey'] = isset($payOs['ChecksumKey']) ? trim((string) $payOs['ChecksumKey']) : '';
    $settings['payOsReturnUrl'] = isset($payOs['ReturnUrl']) ? trim((string) $payOs['ReturnUrl']) : '';
    $settings['payOsCancelUrl'] = isset($payOs['CancelUrl']) ? trim((string) $payOs['CancelUrl']) : '';

    return $settings;
}

  function invoice_payment_create_order_code($invoiceId)
  {
    $invoiceId = (int) $invoiceId;
    $invoiceSuffix = $invoiceId % 10000;
    return (int) (time() * 10000 + $invoiceSuffix);
  }

  function invoice_payment_compute_signature($data, $checksumKey)
  {
    ksort($data, SORT_STRING);

    $pairs = array();
    foreach ($data as $key => $value) {
      $pairs[] = $key . '=' . $value;
    }

    $payload = implode('&', $pairs);
    return strtolower(hash_hmac('sha256', $payload, $checksumKey));
  }

  function invoice_payment_http_post_json($url, $headers, $payload, &$httpCode, &$responseBody, &$error)
  {
    $httpCode = 0;
    $responseBody = '';
    $error = '';

    if (function_exists('curl_init')) {
      $ch = curl_init($url);
      curl_setopt($ch, CURLOPT_POST, true);
      curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);
      curl_setopt($ch, CURLOPT_POSTFIELDS, $payload);
      curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
      curl_setopt($ch, CURLOPT_TIMEOUT, 20);

      $result = curl_exec($ch);
      if ($result === false) {
        $error = 'Khong goi duoc PayOS: ' . curl_error($ch);
        curl_close($ch);
        return false;
      }

      $httpCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
      $responseBody = (string) $result;
      curl_close($ch);
      return true;
    }

    $context = stream_context_create(array(
      'http' => array(
        'method' => 'POST',
        'header' => implode("\r\n", $headers),
        'content' => $payload,
        'timeout' => 20,
        'ignore_errors' => true,
      ),
    ));

    $result = @file_get_contents($url, false, $context);
    if ($result === false) {
      $error = 'Khong goi duoc PayOS qua HTTP client hien tai.';
      return false;
    }

    $responseBody = (string) $result;
    if (isset($http_response_header) && is_array($http_response_header)) {
      foreach ($http_response_header as $headerLine) {
        if (preg_match('/^HTTP\/\S+\s+(\d{3})/i', (string) $headerLine, $matches)) {
          $httpCode = (int) $matches[1];
          break;
        }
      }
    }

    return true;
  }

  function invoice_payment_create_payos_link($invoiceId, $amount, $paymentContent, $settings, &$error)
  {
    $error = '';
    $requiredFields = array('payOsClientId', 'payOsApiKey', 'payOsChecksumKey', 'payOsReturnUrl', 'payOsCancelUrl');
    foreach ($requiredFields as $field) {
      if (empty($settings[$field])) {
        $error = 'Chua cau hinh du Payment:PayOS (ClientId, ApiKey, ChecksumKey, ReturnUrl, CancelUrl).';
        return null;
      }
    }

    $amountInt = (int) ceil((float) $amount);
    if ($amountInt <= 0) {
      $error = 'So tien hoa don khong hop le de tao thanh toan PayOS.';
      return null;
    }

    $description = trim((string) preg_replace('/\s+/', ' ', (string) $paymentContent));
    if ($description === '') {
      $description = 'HDGH' . (int) $invoiceId;
    }
    $description = substr($description, 0, 25);

    $orderCode = invoice_payment_create_order_code($invoiceId);
    $expiredAt = time() + (30 * 60);

    $signData = array(
      'amount' => (string) $amountInt,
      'cancelUrl' => (string) $settings['payOsCancelUrl'],
      'description' => $description,
      'orderCode' => (string) $orderCode,
      'returnUrl' => (string) $settings['payOsReturnUrl'],
    );
    $signature = invoice_payment_compute_signature($signData, (string) $settings['payOsChecksumKey']);

    $payloadArray = array(
      'orderCode' => $orderCode,
      'amount' => $amountInt,
      'description' => $description,
      'cancelUrl' => (string) $settings['payOsCancelUrl'],
      'returnUrl' => (string) $settings['payOsReturnUrl'],
      'expiredAt' => $expiredAt,
      'signature' => $signature,
    );
    $payload = json_encode($payloadArray, JSON_UNESCAPED_UNICODE);
    if (!is_string($payload)) {
      $error = 'Khong tao duoc payload JSON cho PayOS.';
      return null;
    }

    $endpoint = rtrim((string) $settings['payOsEndpoint'], '/');
    $url = $endpoint . '/v2/payment-requests';
    $headers = array(
      'Content-Type: application/json',
      'x-client-id: ' . $settings['payOsClientId'],
      'x-api-key: ' . $settings['payOsApiKey'],
    );

    $httpCode = 0;
    $responseBody = '';
    if (!invoice_payment_http_post_json($url, $headers, $payload, $httpCode, $responseBody, $error)) {
      return null;
    }

    $decoded = json_decode($responseBody, true);
    if (!is_array($decoded)) {
      $error = 'PayOS tra ve du lieu khong hop le.';
      return null;
    }

    $code = isset($decoded['code']) ? (string) $decoded['code'] : '';
    if ($httpCode >= 400 || $code !== '00') {
      $desc = isset($decoded['desc']) ? trim((string) $decoded['desc']) : '';
      $error = $desc !== '' ? $desc : ('PayOS API loi HTTP ' . $httpCode . '.');
      return null;
    }

    $data = isset($decoded['data']) && is_array($decoded['data']) ? $decoded['data'] : array();
    $checkoutUrl = isset($data['checkoutUrl']) ? trim((string) $data['checkoutUrl']) : '';
    $qrCode = isset($data['qrCode']) ? trim((string) $data['qrCode']) : '';
    $paymentLinkId = isset($data['paymentLinkId']) ? trim((string) $data['paymentLinkId']) : '';

    if ($checkoutUrl === '' && $qrCode === '') {
      $error = 'PayOS khong tra ve checkoutUrl hoac qrCode.';
      return null;
    }

    return array(
      'orderCode' => $orderCode,
      'description' => $description,
      'checkoutUrl' => $checkoutUrl,
      'qrCode' => $qrCode,
      'paymentLinkId' => $paymentLinkId,
    );
  }

function invoice_payment_fetch($invoiceId, $idTaiKhoan, $isOwnerInvoiceViewer, &$error)
{
    $error = '';
    if ($invoiceId <= 0) {
        $error = 'Không xác định được hóa đơn cần thanh toán.';
        return null;
    }

    $apiError = '';
    $apiHttpCode = 0;
    $query = array(
        'idTaiKhoan' => $idTaiKhoan,
        'ownerOnly' => $isOwnerInvoiceViewer ? 'true' : 'false',
    );

    $invoice = admin_api_call(
        'GET',
        'Admin/invoices/' . rawurlencode((string) $invoiceId),
        null,
        $apiError,
        $apiHttpCode,
        $query
    );

    if (!is_array($invoice)) {
        $error = $apiError !== '' ? $apiError : 'Không tìm thấy hóa đơn hoặc bạn không có quyền xem hóa đơn này.';
        return null;
    }

    return $invoice;
}

function invoice_payment_content($invoiceId)
{
    $content = 'HDGH' . str_pad((string) $invoiceId, 4, '0', STR_PAD_LEFT);
    return substr($content, 0, 25);
}

function invoice_payment_bypass($invoiceId, $idTaiKhoan, &$error)
{
    $error = '';
    $apiHttpCode = 0;
    $result = admin_api_call(
        'POST',
        'Owner/invoices/' . rawurlencode((string) $invoiceId) . '/bypass-payment',
        null,
        $error,
        $apiHttpCode,
        array('idTaiKhoan' => $idTaiKhoan)
    );

    if (is_array($result) && !empty($result['success'])) {
        return true;
    }

    if ($error === '' && is_array($result) && !empty($result['message'])) {
        $error = (string) $result['message'];
    }
    if ($error === '') {
        $error = 'Khong bypass duoc hoa don nay.';
    }

    return false;
}

$invoice = null;
if (!$isOwnerInvoiceViewer) {
    $paymentError = 'Chỉ chủ gian hàng mới có thể thanh toán hóa đơn gian hàng.';
} else {
    $invoice = invoice_payment_fetch($invoiceId, $idTaiKhoan, $isOwnerInvoiceViewer, $paymentError);
}
$settings = invoice_payment_settings();
$paymentContent = $invoice ? invoice_payment_content((int) $invoice['idHoaDonGianHang']) : '';
$amount = $invoice ? (float) ($invoice['tongTien'] ?? 0) : 0;
$qrImageUrl = '';
$checkoutUrl = '';
$paymentLinkId = '';
$orderCode = '';
$payOsPayload = null;
$canPayInvoice = $invoice && in_array(($invoice['trangThai'] ?? ''), array('chua_thanh_toan', 'qua_han'), true);

if ($invoice && !$canPayInvoice && $paymentError === '') {
    $paymentError = 'Hóa đơn này không ở trạng thái chờ thanh toán.';
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['invoice_bypass_submit'])) {
    if (!$isOwnerInvoiceViewer) {
        $paymentError = 'Chi chu gian hang moi co the bypass thanh toan hoa don.';
    } elseif (!$invoice) {
        $paymentError = 'Khong tim thay hoa don can bypass.';
    } elseif (!$canPayInvoice) {
        $paymentError = 'Hoa don nay khong o trang thai cho thanh toan.';
    } else {
        $bypassError = '';
        if (invoice_payment_bypass((int) $invoice['idHoaDonGianHang'], $idTaiKhoan, $bypassError)) {
            invoice_payment_redirect(admin_url('index1st.php?usecase=invoice&status=da_thanh_toan&selected=' . (int) $invoice['idHoaDonGianHang']));
        }
        $paymentError = $bypassError;
    }
}

if ($canPayInvoice && $paymentError === '') {
  $payOsPayload = invoice_payment_create_payos_link(
    (int) $invoice['idHoaDonGianHang'],
    $amount,
    $paymentContent,
    $settings,
    $paymentError
  );

  if (is_array($payOsPayload)) {
    $checkoutUrl = isset($payOsPayload['checkoutUrl']) ? (string) $payOsPayload['checkoutUrl'] : '';
    $paymentLinkId = isset($payOsPayload['paymentLinkId']) ? (string) $payOsPayload['paymentLinkId'] : '';
    $orderCode = isset($payOsPayload['orderCode']) ? (string) $payOsPayload['orderCode'] : '';
    $qrPayload = isset($payOsPayload['qrCode']) && $payOsPayload['qrCode'] !== ''
      ? (string) $payOsPayload['qrCode']
      : $checkoutUrl;

    if ($qrPayload !== '') {
      $qrImageUrl = 'https://api.qrserver.com/v1/create-qr-code/?size=320x320&data=' . rawurlencode($qrPayload);
    }
  }
} elseif ($canPayInvoice && $paymentError === '') {
  $paymentError = 'Chua the tao thanh toan PayOS cho hoa don nay.';
}
?>
<main class="main-content">
  <section class="invoice-payment-page">
    <div class="payment-shell">
      <a class="back-link" href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=invoice&selected=' . (int) $invoiceId), ENT_QUOTES, 'UTF-8'); ?>">
        <i class="fa-solid fa-arrow-left"></i>
        <span>Quay lại hóa đơn</span>
      </a>

      <?php if ($paymentError !== '') { ?>
      <div class="payment-alert"><?php echo htmlspecialchars($paymentError, ENT_QUOTES, 'UTF-8'); ?></div>
      <?php } ?>

      <?php if ($invoice) { ?>
      <div class="payment-card">
        <div class="payment-info">
          <span class="payment-kicker">Thanh toán hóa đơn gian hàng</span>
          <h2>#HDGH-<?php echo str_pad((string) (int) $invoice['idHoaDonGianHang'], 4, '0', STR_PAD_LEFT); ?></h2>
          <p><?php echo htmlspecialchars(invoice_payment_text($invoice['tenGianHang'] ?? '', 'Gian hàng'), ENT_QUOTES, 'UTF-8'); ?></p>

          <div class="amount-box">
            <span>Số tiền cần thanh toán</span>
            <strong><?php echo htmlspecialchars(invoice_payment_money($invoice['tongTien'] ?? 0), ENT_QUOTES, 'UTF-8'); ?></strong>
          </div>

          <div class="payment-grid">
            <div>
              <span>Ngày tạo</span>
              <strong><?php echo htmlspecialchars(invoice_payment_datetime($invoice['ngayTao'] ?? null), ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <div>
              <span>Hạn thanh toán</span>
              <strong><?php echo htmlspecialchars(invoice_payment_datetime($invoice['ngayHetHan'] ?? null), ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <div>
              <span>Nội dung chuyển khoản</span>
              <strong><?php echo htmlspecialchars($paymentContent, ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <?php if ($orderCode !== '') { ?>
            <div>
              <span>PayOS orderCode</span>
              <strong><?php echo htmlspecialchars($orderCode, ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
            <?php } ?>
            <div>
              <span>Trạng thái</span>
              <strong><?php echo htmlspecialchars(invoice_payment_text($invoice['trangThai'] ?? '', 'Chưa có'), ENT_QUOTES, 'UTF-8'); ?></strong>
            </div>
          </div>

          <div class="bank-box">
            <span>Phuong thuc thanh toan</span>
            <strong>PayOS</strong>
            <p>
              <?php if ($paymentLinkId !== '') { ?>
                PaymentLinkId: <?php echo htmlspecialchars($paymentLinkId, ENT_QUOTES, 'UTF-8'); ?>
              <?php } else { ?>
                Quet QR hoac mo checkout PayOS de thanh toan hoa don.
              <?php } ?>
            </p>
          </div>
        </div>

        <div class="qr-panel">
          <div class="qr-frame">
            <?php if ($qrImageUrl !== '') { ?>
            <img src="<?php echo htmlspecialchars($qrImageUrl, ENT_QUOTES, 'UTF-8'); ?>" alt="QR thanh toán hóa đơn gian hàng" />
            <?php } else { ?>
            <div class="qr-empty">Chưa thể tạo QR</div>
            <?php } ?>
          </div>
          <?php if ($checkoutUrl !== '') { ?>
          <p><a class="back-link" href="<?php echo htmlspecialchars($checkoutUrl, ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener noreferrer">Mo trang checkout PayOS</a></p>
          <?php } ?>
          <p>Quet QR PayOS hoac mo checkout link de hoan tat thanh toan.</p>
          <?php if ($canPayInvoice && $isOwnerInvoiceViewer) { ?>
          <form method="post" class="bypass-payment-form" onsubmit="return confirm('Bypass thanh toan va danh dau hoa don nay da thanh toan?');">
            <input type="hidden" name="invoice_bypass_submit" value="1" />
            <button class="bypass-payment-btn" type="submit">
              <i class="fa-solid fa-bolt"></i>
              <span>Bypass thanh toan</span>
            </button>
            <small>Chuc nang test giong app: bo qua PayOS va kich hoat hoa don ngay.</small>
          </form>
          <?php } ?>
        </div>
      </div>
      <?php } ?>
    </div>
  </section>
</main>
<?php if ($invoice && ($invoice['trangThai'] ?? '') === 'chua_thanh_toan') { ?>
<script>
  setInterval(() => {
    fetch('admin/check_invoice_status.php?id=<?php echo (int)$invoice['idHoaDonGianHang']; ?>')
      .then(res => res.json())
      .then(data => {
        if (data.status === 'da_thanh_toan') {
          window.location.reload();
        }
      })
      .catch(err => console.error(err));
  }, 3000);
</script>
<?php } ?>
