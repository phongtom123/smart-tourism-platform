<?php

if (!function_exists('admin_base_path')) {
    // __DIR__ ở đây luôn là thư mục gốc CS_admin (nơi connect.php nằm),
    // không phụ thuộc vào script nào đang chạy.
    define('_ADMIN_ROOT_DIR', __DIR__);

    function admin_base_path()
    {
        static $basePath = null;

        if ($basePath !== null) {
            return $basePath;
        }

        $docRoot = isset($_SERVER['DOCUMENT_ROOT'])
            ? str_replace('\\', '/', realpath($_SERVER['DOCUMENT_ROOT']))
            : '';
        $adminDir = str_replace('\\', '/', _ADMIN_ROOT_DIR);

        if ($docRoot !== '' && strpos($adminDir, $docRoot) === 0) {
            $relative = ltrim(substr($adminDir, strlen($docRoot)), '/');
            $basePath = $relative !== '' ? '/' . $relative : '';
        } else {
            // Fallback: dùng SCRIPT_NAME nhưng bỏ phần /api/ hoặc /admin/ nếu có
            $scriptName = isset($_SERVER['SCRIPT_NAME']) ? str_replace('\\', '/', (string) $_SERVER['SCRIPT_NAME']) : '';
            $directory = str_replace('\\', '/', dirname($scriptName));
            if ($directory === '/' || $directory === '\\' || $directory === '.') {
                $basePath = '';
            } else {
                $basePath = rtrim($directory, '/');
            }
        }

        return $basePath;
    }
}

if (!function_exists('admin_url')) {
    function admin_url($path = '')
    {
        $path = ltrim((string) $path, '/');
        $basePath = admin_base_path();

        if ($path === '') {
            return $basePath !== '' ? $basePath : '/';
        }

        return ($basePath !== '' ? $basePath : '') . '/' . $path;
    }
}

if (!function_exists('backend_base_url')) {
    function backend_base_url()
    {
        static $baseUrl = null;

        if ($baseUrl !== null) {
            return $baseUrl;
        }

        $configuredUrl = getenv('CSA_BACKEND_BASE_URL');
        if (is_string($configuredUrl) && trim($configuredUrl) !== '') {
            $baseUrl = rtrim(trim($configuredUrl), '/');
            return $baseUrl;
        }

        $baseUrl = backend_port_is_open('127.0.0.1', 7123)
            ? 'https://localhost:7123'
            : 'http://localhost:5114';
        return $baseUrl;
    }
}

if (!function_exists('backend_port_is_open')) {
    function backend_port_is_open($host, $port)
    {
        $errno = 0;
        $errstr = '';
        $socket = @fsockopen((string) $host, (int) $port, $errno, $errstr, 0.15);

        if ($socket === false) {
            return false;
        }

        fclose($socket);
        return true;
    }
}

if (!function_exists('backend_api_url')) {
    function backend_api_url($path = '')
    {
        return backend_base_url() . '/api/' . ltrim((string) $path, '/');
    }
}

if (!function_exists('backend_public_url')) {
    function backend_public_url($path = '')
    {
        return backend_base_url() . '/' . ltrim((string) $path, '/');
    }
}

/**
 * Gọi API backend .NET (VinhKhanh). Trả về dữ liệu JSON đã decode (assoc array)
 * khi 2xx, hoặc null khi lỗi — ghi mô tả lỗi vào $error, mã HTTP vào $httpCode.
 *
 * @param string $method   GET|POST|PUT|PATCH|DELETE
 * @param string $path     Phần path sau /api/, vd: 'Admin/stores'
 * @param mixed  $payload  Body sẽ được json_encode (truyền null nếu không có)
 * @param string $error    OUT — mô tả lỗi nếu có
 * @param int    $httpCode OUT — mã HTTP trả về
 * @param array  $query    Query string assoc array, vd ['idTaiKhoan' => 1]
 * @return mixed|null
 */
if (!function_exists('admin_api_call')) {
    function admin_api_call($method, $path, $payload = null, &$error = '', &$httpCode = 0, $query = array())
    {
        $error = '';
        $httpCode = 0;

        $url = backend_api_url($path);
        if (is_array($query) && count($query) > 0) {
            $url .= (strpos($url, '?') === false ? '?' : '&') . http_build_query($query);
        }

        $ch = curl_init($url);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_CUSTOMREQUEST, strtoupper((string) $method));
        curl_setopt($ch, CURLOPT_HTTPHEADER, array(
            'Accept: application/json',
            'Content-Type: application/json',
        ));
        curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
        curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, false);
        curl_setopt($ch, CURLOPT_CONNECTTIMEOUT, 5);
        curl_setopt($ch, CURLOPT_TIMEOUT, 15);

        if ($payload !== null) {
            curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($payload, JSON_UNESCAPED_UNICODE));
        }

        $body = curl_exec($ch);
        $httpCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
        $curlError = curl_error($ch);
        curl_close($ch);

        if ($body === false) {
            $error = $curlError !== '' ? $curlError : 'Khong the ket noi backend.';
            return null;
        }

        $decoded = json_decode((string) $body, true);

        if ($httpCode >= 400) {
            $error = is_array($decoded) && isset($decoded['message'])
                ? (string) $decoded['message']
                : 'API tra ve HTTP ' . $httpCode . '.';
            return null;
        }

        if ($decoded === null && trim((string) $body) !== '') {
            $error = 'Phan hoi API khong hop le.';
            return null;
        }

        return $decoded;
    }
}

if (!function_exists('backend_connection_settings')) {
    function backend_connection_settings()
    {
        static $settings = null;

        if ($settings !== null) {
            return $settings;
        }

        // Prefer ignored local settings, then fall back to the safe public defaults.
        $backendConfigDir = dirname(__DIR__) . '/VinhKhanh/VinhKhanh';
        $appSettingsPath = $backendConfigDir . '/appsettings.Development.json';
        if (!is_file($appSettingsPath)) {
            $appSettingsPath = $backendConfigDir . '/appsettings.json';
        }
        if (!is_file($appSettingsPath)) {
            $settings = array();
            return $settings;
        }

        $raw = file_get_contents($appSettingsPath);
        $decoded = is_string($raw) ? json_decode($raw, true) : null;
        if (!is_array($decoded) || empty($decoded['ConnectionStrings']['DefaultConnection'])) {
            $settings = array();
            return $settings;
        }

        $parts = explode(';', (string) $decoded['ConnectionStrings']['DefaultConnection']);
        $parsed = array();
        foreach ($parts as $part) {
            $part = trim($part);
            if ($part === '' || strpos($part, '=') === false) {
                continue;
            }

            list($key, $value) = explode('=', $part, 2);
            $parsed[strtolower(trim($key))] = trim($value);
        }

        $settings = array(
            'host' => isset($parsed['server']) ? $parsed['server'] : '127.0.0.1',
            'port' => isset($parsed['port']) ? (int) $parsed['port'] : 3306,
            'database' => isset($parsed['database']) ? $parsed['database'] : 'gianhang',
            'username' => isset($parsed['uid']) ? $parsed['uid'] : 'root',
            'password' => isset($parsed['pwd']) ? $parsed['pwd'] : '',
        );

        return $settings;
    }
}

if (!function_exists('admin_db_connection')) {
    function admin_db_connection()
    {
        if (!class_exists('mysqli')) {
            return null;
        }

        $settings = backend_connection_settings();
        if (empty($settings['database'])) {
            return null;
        }

        $mysqli = @new mysqli(
            $settings['host'],
            $settings['username'],
            $settings['password'],
            $settings['database'],
            (int) $settings['port']
        );

        if ($mysqli->connect_errno) {
            return null;
        }

        $mysqli->set_charset('utf8mb4');
        return $mysqli;
    }
}

if (!function_exists('admin_ensure_store_request_table')) {
    function admin_ensure_store_request_table($conn)
    {
        if (!$conn instanceof mysqli) {
            return false;
        }

        $sql = "
            CREATE TABLE IF NOT EXISTS yeucaugianhang (
                idYeuCau INT NOT NULL AUTO_INCREMENT,
                idChuQuanLy INT NOT NULL,
                tenDeNghi VARCHAR(150) NOT NULL,
                diaChiDeNghi VARCHAR(255) DEFAULT NULL,
                ghiChuGui TEXT DEFAULT NULL,
                trangThai ENUM('cho_duyet','da_duyet','tu_choi') NOT NULL DEFAULT 'cho_duyet',
                idGianHang INT DEFAULT NULL,
                ngayGui DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP(),
                ngayXuLy DATETIME DEFAULT NULL,
                PRIMARY KEY (idYeuCau),
                KEY idx_yeucaugianhang_owner (idChuQuanLy),
                KEY idx_yeucaugianhang_status (trangThai),
                KEY idx_yeucaugianhang_store (idGianHang),
                CONSTRAINT fk_yeucaugianhang_owner
                    FOREIGN KEY (idChuQuanLy) REFERENCES chu_quan_ly (idChuQuanLy)
                    ON DELETE CASCADE ON UPDATE CASCADE,
                CONSTRAINT fk_yeucaugianhang_store
                    FOREIGN KEY (idGianHang) REFERENCES gianhang (idGianHang)
                    ON DELETE SET NULL ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ";

        return $conn->query($sql) === true;
    }
}

if (!function_exists('admin_run_store_invoice_maintenance')) {
    function admin_run_store_invoice_maintenance(&$error = '')
    {
        static $hasRun = false;
        $error = '';

        if ($hasRun) {
            return array(
                'expiredInvoices' => 0,
                'pausedStores' => 0,
                'createdInvoices' => 0,
            );
        }

        $hasRun = true;
        $conn = admin_db_connection();
        if (!$conn instanceof mysqli) {
            $error = 'Không thể mở kết nối DB để bảo trì hóa đơn gian hàng.';
            return false;
        }

        $stats = array(
            'expiredInvoices' => 0,
            'pausedStores' => 0,
            'createdInvoices' => 0,
        );

        $conn->begin_transaction();

        try {
            $expireSql = "
                UPDATE hoadongianhang
                SET trangThai = 'qua_han'
                WHERE trangThai = 'chua_thanh_toan'
                  AND ngayHetHan IS NOT NULL
                  AND ngayHetHan < NOW()
            ";

            if (!$conn->query($expireSql)) {
                throw new RuntimeException($conn->error);
            }
            $stats['expiredInvoices'] = max(0, (int) $conn->affected_rows);

            $pauseSql = "
                UPDATE gianhang gh
                SET gh.tinhTrang = 'tam_ngung',
                    gh.thoiGianCapNhat = NOW()
                WHERE gh.tinhTrang = 'dang_hoat_dong'
                  AND EXISTS (
                      SELECT 1
                      FROM hoadongianhang hdgh
                      WHERE hdgh.idGianHang = gh.idGianHang
                        AND hdgh.trangThai = 'qua_han'
                        AND hdgh.ngayHetHan IS NOT NULL
                        AND hdgh.ngayHetHan < NOW()
                  )
            ";

            if (!$conn->query($pauseSql)) {
                throw new RuntimeException($conn->error);
            }
            $stats['pausedStores'] = max(0, (int) $conn->affected_rows);

            $createSql = "
                INSERT INTO hoadongianhang
                    (idGianHang, tongTien, ngayHetHan, trangThai, ghiChu, ngayTao)
                SELECT
                    gh.idGianHang,
                    CASE
                        WHEN COALESCE(gh.phiHangThang, 0) > 0 THEN gh.phiHangThang
                        ELSE COALESCE(latest.tongTien, 0)
                    END AS tongTien,
                    DATE_ADD(latest.ngayHetHan, INTERVAL 1 MONTH) AS ngayHetHan,
                    'chua_thanh_toan' AS trangThai,
                    CONCAT('Phi duy tri thang ', DATE_FORMAT(DATE_ADD(latest.ngayHetHan, INTERVAL 1 MONTH), '%m/%Y')) AS ghiChu,
                    NOW() AS ngayTao
                FROM gianhang gh
                INNER JOIN (
                    SELECT hdgh.*
                    FROM hoadongianhang hdgh
                    INNER JOIN (
                        SELECT idGianHang, MAX(idHoaDonGianHang) AS latestId
                        FROM hoadongianhang
                        GROUP BY idGianHang
                    ) last_invoice ON last_invoice.latestId = hdgh.idHoaDonGianHang
                ) latest ON latest.idGianHang = gh.idGianHang
                WHERE gh.tinhTrang <> 'dong_cua'
                  AND latest.ngayHetHan IS NOT NULL
                  AND latest.ngayHetHan < NOW()
                  AND NOT EXISTS (
                      SELECT 1
                      FROM hoadongianhang duplicate_invoice
                      WHERE duplicate_invoice.idGianHang = gh.idGianHang
                        AND duplicate_invoice.ngayHetHan = DATE_ADD(latest.ngayHetHan, INTERVAL 1 MONTH)
                  )
            ";

            for ($i = 0; $i < 12; $i++) {
                if (!$conn->query($createSql)) {
                    throw new RuntimeException($conn->error);
                }

                $createdThisRound = max(0, (int) $conn->affected_rows);
                $stats['createdInvoices'] += $createdThisRound;
                if ($createdThisRound === 0) {
                    break;
                }
            }

            if (!$conn->query($expireSql)) {
                throw new RuntimeException($conn->error);
            }
            $stats['expiredInvoices'] += max(0, (int) $conn->affected_rows);

            if (!$conn->query($pauseSql)) {
                throw new RuntimeException($conn->error);
            }
            $stats['pausedStores'] += max(0, (int) $conn->affected_rows);

            $conn->commit();
        } catch (Throwable $exception) {
            $conn->rollback();
            $error = 'Không thể bảo trì hóa đơn gian hàng: ' . $exception->getMessage();
            $conn->close();
            return false;
        }

        $conn->close();
        return $stats;
    }
}
