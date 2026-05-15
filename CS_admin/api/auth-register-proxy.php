<?php
require_once dirname(__DIR__) . '/connect.php';
header('Content-Type: application/json; charset=utf-8');

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
  http_response_code(405);
  echo json_encode(array(
    'success' => false,
    'message' => 'Method not allowed. Use POST.'
  ));
  exit;
}

$rawBody = file_get_contents('php://input');
if ($rawBody === false || trim($rawBody) === '') {
  http_response_code(400);
  echo json_encode(array(
    'success' => false,
    'message' => 'Request body is required.'
  ));
  exit;
}

$apiUrl = backend_api_url('Auth/register');
$ch = curl_init($apiUrl);

curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_POST, true);
curl_setopt($ch, CURLOPT_POSTFIELDS, $rawBody);
curl_setopt($ch, CURLOPT_HTTPHEADER, array(
  'Content-Type: application/json',
  'Accept: application/json'
));
curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, false);
curl_setopt($ch, CURLOPT_FOLLOWLOCATION, true);
curl_setopt($ch, CURLOPT_MAXREDIRS, 3);

$responseBody = curl_exec($ch);
$curlError = curl_error($ch);
$httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);

curl_close($ch);

if ($responseBody === false) {
  http_response_code(502);
  echo json_encode(array(
    'success' => false,
    'message' => $curlError !== '' ? $curlError : 'Cannot connect to register API.'
  ));
  exit;
}

if ($httpCode <= 0) {
  $httpCode = 502;
}

$trimmedBody = trim((string) $responseBody);

if ($trimmedBody === '') {
  $message = 'API đăng ký không trả về dữ liệu.';

  if ($httpCode === 404) {
    $message = 'API đăng ký chưa sẵn sàng trên backend. Hãy restart backend để nạp endpoint mới.';
  } elseif ($httpCode >= 500) {
    $message = 'Máy chủ đăng ký đang gặp lỗi.';
  }

  http_response_code($httpCode);
  echo json_encode(array(
    'success' => false,
    'message' => $message
  ), JSON_UNESCAPED_UNICODE);
  exit;
}

$decodedResponse = json_decode($responseBody, true);
if (!is_array($decodedResponse)) {
  http_response_code($httpCode >= 400 ? $httpCode : 502);
  echo json_encode(array(
    'success' => false,
    'message' => 'Phản hồi từ API đăng ký không đúng định dạng JSON.'
  ), JSON_UNESCAPED_UNICODE);
  exit;
}

if (!array_key_exists('success', $decodedResponse)) {
  $decodedResponse['success'] = $httpCode >= 200 && $httpCode < 300;
}

if (empty($decodedResponse['message'])) {
  if (!empty($decodedResponse['title'])) {
    $decodedResponse['message'] = $decodedResponse['title'];
  } elseif (!empty($decodedResponse['errors']) && is_array($decodedResponse['errors'])) {
    $firstErrors = reset($decodedResponse['errors']);
    if (is_array($firstErrors) && !empty($firstErrors[0])) {
      $decodedResponse['message'] = (string) $firstErrors[0];
    }
  }
}

http_response_code($httpCode);
echo json_encode($decodedResponse, JSON_UNESCAPED_UNICODE);
