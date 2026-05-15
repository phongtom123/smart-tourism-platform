-- Migration yeucaugianhang ve schema don gian hon theo ERD moi.
-- Chay tren DB hien tai dang dung schema cu.
-- Co tao bang backup de ban doi chieu neu can.

USE `gianhang`;

SET FOREIGN_KEY_CHECKS = 0;

DROP TABLE IF EXISTS `yeucaugianhang_backup_20260416`;
CREATE TABLE `yeucaugianhang_backup_20260416` AS
SELECT * FROM `yeucaugianhang`;

DROP TABLE IF EXISTS `yeucaugianhang`;

CREATE TABLE `yeucaugianhang` (
  `idYeuCau` int NOT NULL AUTO_INCREMENT,
  `idChuQuanLy` int NOT NULL,
  `tenDeNghi` varchar(150) NOT NULL,
  `diaChiDeNghi` varchar(255) DEFAULT NULL,
  `ghiChuGui` text DEFAULT NULL,
  `trangThai` enum('cho_duyet','da_duyet','tu_choi') NOT NULL DEFAULT 'cho_duyet',
  `idGianHang` int DEFAULT NULL,
  `ngayGui` datetime NOT NULL DEFAULT current_timestamp(),
  `ngayXuLy` datetime DEFAULT NULL,
  PRIMARY KEY (`idYeuCau`),
  KEY `idx_yeucaugianhang_owner` (`idChuQuanLy`),
  KEY `idx_yeucaugianhang_status` (`trangThai`),
  KEY `idx_yeucaugianhang_store` (`idGianHang`),
  CONSTRAINT `fk_yeucaugianhang_owner`
    FOREIGN KEY (`idChuQuanLy`) REFERENCES `chu_quan_ly` (`idChuQuanLy`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_yeucaugianhang_store`
    FOREIGN KEY (`idGianHang`) REFERENCES `gianhang` (`idGianHang`)
    ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `yeucaugianhang`
(
  `idYeuCau`,
  `idChuQuanLy`,
  `tenDeNghi`,
  `diaChiDeNghi`,
  `ghiChuGui`,
  `trangThai`,
  `idGianHang`,
  `ngayGui`,
  `ngayXuLy`
)
SELECT
  `idYeuCau`,
  `idChuQuanLy`,
  `tenGianHang`,
  `diaChi`,
  `moTa`,
  `trangThaiYeuCau`,
  `idGianHang`,
  `ngayTao`,
  `thoiGianXuLy`
FROM `yeucaugianhang_backup_20260416`;

ALTER TABLE `yeucaugianhang` AUTO_INCREMENT = 1;

SET FOREIGN_KEY_CHECKS = 1;
