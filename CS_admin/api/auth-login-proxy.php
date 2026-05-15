<?php
require_once dirname(__DIR__) . '/connect.php';
header('Content-Type: application/json; charset=utf-8');
session_start();

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

$apiUrl = backend_api_url('Auth/login');
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
    'message' => $curlError !== '' ? $curlError : 'Cannot connect to auth API.'
  ));
  exit;
}

if ($httpCode <= 0) {
  $httpCode = 502;
}

$decodedResponse = json_decode($responseBody, true);
if (
  is_array($decodedResponse)
  && isset($decodedResponse['success'])
  && $decodedResponse['success'] === true
) {
  $loaiTaiKhoan = isset($decodedResponse['loaiTaiKhoan']) ? $decodedResponse['loaiTaiKhoan'] : null;

  if (!empty($decodedResponse['idAdmin'])) {
    $loaiTaiKhoan = 'admin';
  } elseif (!empty($decodedResponse['idChuQuanLy'])) {
    $loaiTaiKhoan = 'chu_quan_ly';
  }

  if ($loaiTaiKhoan !== 'admin' && $loaiTaiKhoan !== 'chu_quan_ly') {
    http_response_code(403);
    echo json_encode(array(
      'success' => false,
      'message' => 'Trang này chỉ hỗ trợ đăng nhập cho Admin và Chủ quản lý.'
    ));
    exit;
  }

  $_SESSION['admin_auth'] = array(
    'isLoggedIn' => true,
    'idTaiKhoan' => isset($decodedResponse['idTaiKhoan']) ? $decodedResponse['idTaiKhoan'] : null,
    'username' => isset($decodedResponse['username']) ? $decodedResponse['username'] : null,
    'email' => isset($decodedResponse['email']) ? $decodedResponse['email'] : null,
    'loaiTaiKhoan' => $loaiTaiKhoan,
    'hoTen' => isset($decodedResponse['hoTen']) ? $decodedResponse['hoTen'] : null,
    'idAdmin' => isset($decodedResponse['idAdmin']) ? $decodedResponse['idAdmin'] : null,
    'idChuQuanLy' => isset($decodedResponse['idChuQuanLy']) ? $decodedResponse['idChuQuanLy'] : null,
    'loginAt' => date('c')
  );

  $decodedResponse['loaiTaiKhoan'] = $loaiTaiKhoan;
  $decodedResponse['redirectUrl'] = admin_url('index1st.php?usecase=store');

  http_response_code($httpCode);
  echo json_encode($decodedResponse, JSON_UNESCAPED_UNICODE);
  exit;
}

http_response_code($httpCode);
echo $responseBody;
