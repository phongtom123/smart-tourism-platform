-- =============================================
-- DATABASE MIGRATIONS - VINH KHANH SYSTEM
-- DATE: 2026-04-24
-- =============================================

-- 1. Thêm cột luotTruyCap vào bảng gianhang để theo dõi tổng lượt ghé thăm
-- Chạy lệnh này nếu bảng gianhang chưa có cột này
ALTER TABLE `gianhang` ADD COLUMN IF NOT EXISTS `luotTruyCap` INT DEFAULT 0;

-- 2. Tạo bảng luot_truy_cap_ngay để lưu trữ dữ liệu Heatmap theo từng ngày
CREATE TABLE IF NOT EXISTS `luot_truy_cap_ngay` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `idGianHang` int(11) NOT NULL,
  `ngay` date NOT NULL,
  `soLuot` int(11) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `idx_gianhang_ngay` (`idGianHang`, `ngay`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Ghi chú: Logic Backend đã được cập nhật để tự động ghi nhận vào cả 2 nơi khi có khách nghe Audio.
