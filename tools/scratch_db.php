<?php
require_once dirname(__DIR__) . '/CS_admin/connect.php';

$conn = admin_db_connection();
if ($conn) {
    $sql = "CREATE TABLE IF NOT EXISTS `luot_truy_cap_ngay` (
      `id` bigint(20) NOT NULL AUTO_INCREMENT,
      `idGianHang` int(11) NOT NULL,
      `ngay` date NOT NULL,
      `soLuot` int(11) NOT NULL DEFAULT 1,
      PRIMARY KEY (`id`),
      UNIQUE KEY `idx_gianhang_ngay` (`idGianHang`, `ngay`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
    
    if ($conn->query($sql) === TRUE) {
        echo "Table created successfully.\n";
    } else {
        echo "Error creating table: " . $conn->error . "\n";
    }
    
    // Generate dummy data for POI 1, 2, 3, etc. for the last 365 days
    $today = new DateTime();
    $conn->begin_transaction();
    try {
        for ($i = 0; $i < 365; $i++) {
            $dateStr = $today->format('Y-m-d');
            for ($poi = 1; $poi <= 20; $poi++) {
                // Random probability to have visits
                if (rand(1, 100) > 30) {
                    $visits = rand(1, 25);
                    $conn->query("INSERT INTO luot_truy_cap_ngay (idGianHang, ngay, soLuot) VALUES ($poi, '$dateStr', $visits) ON DUPLICATE KEY UPDATE soLuot = soLuot + $visits");
                }
            }
            $today->modify('-1 day');
        }
        $conn->commit();
        echo "Dummy data generated.\n";
    } catch (Exception $e) {
        $conn->rollback();
        echo "Error: " . $e->getMessage() . "\n";
    }
    $conn->close();
}
