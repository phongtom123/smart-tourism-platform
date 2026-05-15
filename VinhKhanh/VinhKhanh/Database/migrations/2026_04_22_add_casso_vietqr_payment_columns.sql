ALTER TABLE `hoadon`
  ADD COLUMN `maThanhToan` varchar(32) DEFAULT NULL AFTER `idGoi`,
  ADD COLUMN `maThietBi` varchar(100) DEFAULT NULL AFTER `maThanhToan`,
  ADD COLUMN `guiEmail` tinyint(1) NOT NULL DEFAULT 0 AFTER `email`,
  ADD COLUMN `thoiGianThanhToan` datetime DEFAULT NULL AFTER `thoiGianTao`,
  ADD COLUMN `cassoTransactionId` varchar(80) DEFAULT NULL AFTER `tinhTrang`;

CREATE UNIQUE INDEX `uq_hoadon_maThanhToan` ON `hoadon` (`maThanhToan`);
CREATE INDEX `idx_hoadon_maThietBi` ON `hoadon` (`maThietBi`);
CREATE INDEX `idx_hoadon_cassoTransactionId` ON `hoadon` (`cassoTransactionId`);
