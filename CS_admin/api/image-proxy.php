<?php
declare(strict_types=1);
require_once dirname(__DIR__) . '/connect.php';

$rawPath = isset($_GET['path']) ? trim((string) $_GET['path']) : '';
if ($rawPath === '') {
    http_response_code(400);
    header('Content-Type: text/plain; charset=UTF-8');
    echo 'Missing image path.';
    exit;
}

if (stripos($rawPath, 'http://') === 0 || stripos($rawPath, 'https://') === 0) {
    $targetUrl = $rawPath;
} else {
    $cleanPath = str_replace('\\', '/', $rawPath);
    $cleanPath = ltrim($cleanPath, '/');

    if (
        stripos($cleanPath, 'images/') !== 0 &&
        stripos($cleanPath, 'uploads/') !== 0 &&
        stripos($cleanPath, 'content/') !== 0
    ) {
        $cleanPath = 'images/' . $cleanPath;
    }

    $targetUrl = backend_public_url($cleanPath);
}

$ch = curl_init($targetUrl);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
curl_setopt($ch, CURLOPT_FOLLOWLOCATION, true);
curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, false);
curl_setopt($ch, CURLOPT_HTTPHEADER, array('Accept: image/*'));

$body = curl_exec($ch);
$contentType = (string) curl_getinfo($ch, CURLINFO_CONTENT_TYPE);
$httpCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
$curlError = curl_error($ch);
curl_close($ch);

if ($body === false || $httpCode >= 400) {
    http_response_code($httpCode >= 400 ? $httpCode : 502);
    header('Content-Type: text/plain; charset=UTF-8');
    echo $curlError !== '' ? $curlError : 'Unable to load image.';
    exit;
}

header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');
header('Pragma: no-cache');
header('Content-Type: ' . ($contentType !== '' ? $contentType : 'image/jpeg'));
echo $body;
