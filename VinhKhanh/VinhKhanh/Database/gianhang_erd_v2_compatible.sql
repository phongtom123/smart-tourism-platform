-- Schema MySQL/MariaDB theo ERD cải tiến
-- Mục tiêu:
-- 1. Bổ sung các thành phần mới trong ERD như goi dich vu, hoa don gian hang, vong bo.
-- 2. GIU NGUYEN cac bang/cot ma backend hien tai dang query de khong phai sua code.
-- 3. Co seed data toi thieu de app/backend import xong la chay duoc ngay.
--
-- Mapping de giu tuong thich voi code hien tai:
-- - ERD "IDChiNhanh" => giu ten cot "idGianHang"
-- - ERD "IDMon" => giu ten cot "idMonAn"
-- - ERD "Thoi gian dang ky" => giu cot "ngayTao" / "ngayDangKy" theo code hien co
-- - ERD "Hinh anh chi nhanh" => giu bang "hinhanhgianhang"
-- - ERD "Hoa don du khach" => tiep tuc dung bang "hoadon", bo sung them cot email/idPhienVaoApp/idGoi
--
-- File nay co the import truc tiep sau khi DROP DB cu.

DROP DATABASE IF EXISTS `gianhang`;
CREATE DATABASE `gianhang`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `gianhang`;

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

CREATE TABLE `taikhoan` (
  `idTaiKhoan` int NOT NULL AUTO_INCREMENT,
  `email` varchar(100) NOT NULL,
  `matKhau` varchar(255) NOT NULL,
  `username` varchar(50) NOT NULL,
  `loaiTaiKhoan` enum('khach_hang','chu_quan_ly','admin') NOT NULL DEFAULT 'khach_hang',
  `tinhTrang` enum('hoat_dong','khoa') NOT NULL DEFAULT 'hoat_dong',
  `tinhTrangDangKy` enum('cho_duyet','da_duyet','tu_choi') NOT NULL DEFAULT 'da_duyet',
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`idTaiKhoan`),
  UNIQUE KEY `uq_taikhoan_email` (`email`),
  UNIQUE KEY `uq_taikhoan_username` (`username`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `admin` (
  `idAdmin` int NOT NULL AUTO_INCREMENT,
  `idTaiKhoan` int NOT NULL,
  `hoTen` varchar(150) DEFAULT NULL,
  `ghiChu` varchar(255) DEFAULT NULL,
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`idAdmin`),
  UNIQUE KEY `uq_admin_idTaiKhoan` (`idTaiKhoan`),
  CONSTRAINT `fk_admin_taikhoan`
    FOREIGN KEY (`idTaiKhoan`) REFERENCES `taikhoan` (`idTaiKhoan`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `chu_quan_ly` (
  `idChuQuanLy` int NOT NULL AUTO_INCREMENT,
  `idTaiKhoan` int NOT NULL,
  `hoTen` varchar(150) DEFAULT NULL,
  `sdt` varchar(20) DEFAULT NULL,
  `diaChi` varchar(255) DEFAULT NULL,
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`idChuQuanLy`),
  UNIQUE KEY `uq_chuquanly_idTaiKhoan` (`idTaiKhoan`),
  CONSTRAINT `fk_chuquanly_taikhoan`
    FOREIGN KEY (`idTaiKhoan`) REFERENCES `taikhoan` (`idTaiKhoan`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `khachhang` (
  `idKhachHang` int NOT NULL AUTO_INCREMENT,
  `idTaiKhoan` int NOT NULL,
  `sdt` varchar(20) DEFAULT NULL,
  PRIMARY KEY (`idKhachHang`),
  UNIQUE KEY `uq_khachhang_idTaiKhoan` (`idTaiKhoan`),
  CONSTRAINT `fk_khachHang_taiKhoan`
    FOREIGN KEY (`idTaiKhoan`) REFERENCES `taikhoan` (`idTaiKhoan`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `ngonngu` (
  `idNgonNgu` int NOT NULL AUTO_INCREMENT,
  `maNgonNgu` varchar(10) NOT NULL,
  `ten` varchar(50) NOT NULL,
  `trangThai` enum('hoat_dong','an') NOT NULL DEFAULT 'hoat_dong',
  PRIMARY KEY (`idNgonNgu`),
  UNIQUE KEY `uq_ngonngu_maNgonNgu` (`maNgonNgu`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `goidichvu` (
  `idGoi` int NOT NULL AUTO_INCREMENT,
  `ten` varchar(150) NOT NULL,
  `moTa` text DEFAULT NULL,
  `gia` decimal(12,2) NOT NULL DEFAULT 0.00,
  `thoiHanNgay` int NOT NULL DEFAULT 1,
  `trangThai` enum('hoat_dong','tam_ngung','ngung_ap_dung') NOT NULL DEFAULT 'hoat_dong',
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`idGoi`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `thietbi` (
  `idThietBi` int NOT NULL AUTO_INCREMENT,
  `maThietBi` varchar(255) NOT NULL,
  `maKichHoat` varchar(100) DEFAULT NULL,
  `idTaiKhoan` int DEFAULT NULL,
  `daKichHoat` tinyint(1) NOT NULL DEFAULT 0,
  `thoiGianKichHoat` datetime DEFAULT NULL,
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp(),
  `lanCuoiHoatDong` datetime DEFAULT NULL,
  `trangThai` enum('cho_kich_hoat','hoat_dong','khoa') NOT NULL DEFAULT 'cho_kich_hoat',
  PRIMARY KEY (`idThietBi`),
  UNIQUE KEY `uq_thietbi_maThietBi` (`maThietBi`),
  KEY `idx_thietbi_idTaiKhoan` (`idTaiKhoan`),
  CONSTRAINT `fk_thietbi_taikhoan`
    FOREIGN KEY (`idTaiKhoan`) REFERENCES `taikhoan` (`idTaiKhoan`)
    ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `phien_vao_app` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `idThietBi` int DEFAULT NULL,
  `maThietBi` varchar(255) NOT NULL,
  `idGoi` int DEFAULT NULL,
  `qrRaw` text DEFAULT NULL,
  `accessToken` varchar(128) NOT NULL,
  `batDauLuc` datetime NOT NULL DEFAULT current_timestamp(),
  `hetHanLuc` datetime NOT NULL,
  `trangThai` enum('hieu_luc','het_han','huy') NOT NULL DEFAULT 'hieu_luc',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_accessToken` (`accessToken`),
  KEY `idx_phien_vao_app_maThietBi` (`maThietBi`),
  KEY `idx_phien_vao_app_idThietBi` (`idThietBi`),
  KEY `idx_phien_vao_app_idGoi` (`idGoi`),
  CONSTRAINT `fk_phien_vao_app_thietbi_id`
    FOREIGN KEY (`idThietBi`) REFERENCES `thietbi` (`idThietBi`)
    ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_phien_vao_app_thietbi_ma`
    FOREIGN KEY (`maThietBi`) REFERENCES `thietbi` (`maThietBi`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_phien_vao_app_goidichvu`
    FOREIGN KEY (`idGoi`) REFERENCES `goidichvu` (`idGoi`)
    ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `gianhang` (
  `idGianHang` int NOT NULL AUTO_INCREMENT,
  `idChuQuanLy` int DEFAULT NULL,
  `ten` varchar(150) NOT NULL,
  `diaChi` varchar(255) DEFAULT NULL,
  `lat` decimal(10,7) DEFAULT NULL,
  `lon` decimal(10,7) DEFAULT NULL,
  `vongBo` decimal(8,2) NOT NULL DEFAULT 10.00,
  `tinhTrang` enum('dang_hoat_dong','tam_ngung','dong_cua') NOT NULL DEFAULT 'dang_hoat_dong',
  `phiHangThang` decimal(12,2) NOT NULL DEFAULT 0.00,
  `ngayDangKy` datetime NOT NULL DEFAULT current_timestamp(),
  `thoiGianCapNhat` datetime DEFAULT NULL,
  PRIMARY KEY (`idGianHang`),
  KEY `idx_gianhang_idChuQuanLy` (`idChuQuanLy`),
  KEY `idx_gianhang_toado` (`lat`, `lon`),
  CONSTRAINT `fk_gianhang_chuquanly`
    FOREIGN KEY (`idChuQuanLy`) REFERENCES `chu_quan_ly` (`idChuQuanLy`)
    ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

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

CREATE TABLE `gianhangngonngu` (
  `id` int NOT NULL AUTO_INCREMENT,
  `idGianHang` int NOT NULL,
  `idNgonNgu` int NOT NULL,
  `ten` varchar(150) NOT NULL,
  `audioURL` varchar(255) DEFAULT NULL,
  `moTa` text DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_gianHang_ngonNgu` (`idGianHang`, `idNgonNgu`),
  KEY `idx_gianhangngonngu_idNgonNgu` (`idNgonNgu`),
  CONSTRAINT `fk_gianHangNgonNgu_gianHang`
    FOREIGN KEY (`idGianHang`) REFERENCES `gianhang` (`idGianHang`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_gianHangNgonNgu_ngonNgu`
    FOREIGN KEY (`idNgonNgu`) REFERENCES `ngonngu` (`idNgonNgu`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `hinhanhgianhang` (
  `idHinhAnh` int NOT NULL AUTO_INCREMENT,
  `idGianHang` int NOT NULL,
  `duongDan` varchar(255) NOT NULL,
  PRIMARY KEY (`idHinhAnh`),
  KEY `idx_hinhanhgianhang_idGianHang` (`idGianHang`),
  CONSTRAINT `fk_hinhAnhGianHang_gianHang`
    FOREIGN KEY (`idGianHang`) REFERENCES `gianhang` (`idGianHang`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `monan` (
  `idMonAn` int NOT NULL AUTO_INCREMENT,
  `idGianHang` int NOT NULL,
  `ten` varchar(150) NOT NULL,
  `donGia` decimal(12,2) NOT NULL,
  `thoiGianCapNhat` datetime DEFAULT NULL,
  `tinhTrang` enum('con_ban','het_mon','ngung_ban') NOT NULL DEFAULT 'con_ban',
  PRIMARY KEY (`idMonAn`),
  KEY `idx_monan_idGianHang` (`idGianHang`),
  CONSTRAINT `fk_monAn_gianHang`
    FOREIGN KEY (`idGianHang`) REFERENCES `gianhang` (`idGianHang`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `monanngonngu` (
  `id` int NOT NULL AUTO_INCREMENT,
  `idMonAn` int NOT NULL,
  `idNgonNgu` int NOT NULL,
  `ten` varchar(150) NOT NULL,
  `moTa` text DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_monAn_ngonNgu` (`idMonAn`, `idNgonNgu`),
  KEY `idx_monanngonngu_idNgonNgu` (`idNgonNgu`),
  CONSTRAINT `fk_monAnNgonNgu_monAn`
    FOREIGN KEY (`idMonAn`) REFERENCES `monan` (`idMonAn`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_monAnNgonNgu_ngonNgu`
    FOREIGN KEY (`idNgonNgu`) REFERENCES `ngonngu` (`idNgonNgu`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `hinhanhmonan` (
  `idHinhAnh` int NOT NULL AUTO_INCREMENT,
  `idMonAn` int NOT NULL,
  `duongDan` varchar(255) NOT NULL,
  PRIMARY KEY (`idHinhAnh`),
  KEY `idx_hinhanhmonan_idMonAn` (`idMonAn`),
  CONSTRAINT `fk_hinhAnhMonAn_monAn`
    FOREIGN KEY (`idMonAn`) REFERENCES `monan` (`idMonAn`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `hoadon` (
  `idHoaDon` int NOT NULL AUTO_INCREMENT,
  `idKhachHang` int DEFAULT NULL,
  `idPhienVaoApp` bigint DEFAULT NULL,
  `idGoi` int DEFAULT NULL,
  `email` varchar(100) DEFAULT NULL,
  `tongTien` decimal(12,2) NOT NULL DEFAULT 0.00,
  `thoiGianTao` datetime NOT NULL DEFAULT current_timestamp(),
  `tinhTrang` enum('moi_tao','da_thanh_toan','da_huy','het_han') NOT NULL DEFAULT 'moi_tao',
  `ghiChu` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`idHoaDon`),
  KEY `idx_hoadon_idKhachHang` (`idKhachHang`),
  KEY `idx_hoadon_idPhienVaoApp` (`idPhienVaoApp`),
  KEY `idx_hoadon_idGoi` (`idGoi`),
  CONSTRAINT `fk_hoaDon_khachHang`
    FOREIGN KEY (`idKhachHang`) REFERENCES `khachhang` (`idKhachHang`)
    ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_hoadon_phienvaoapp`
    FOREIGN KEY (`idPhienVaoApp`) REFERENCES `phien_vao_app` (`id`)
    ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_hoadon_goidichvu`
    FOREIGN KEY (`idGoi`) REFERENCES `goidichvu` (`idGoi`)
    ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `chitiethoadon` (
  `id` int NOT NULL AUTO_INCREMENT,
  `idHoaDon` int NOT NULL,
  `idMonAn` int NOT NULL,
  `soLuong` int NOT NULL DEFAULT 1,
  `donGia` decimal(12,2) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_chitiethoadon_idHoaDon` (`idHoaDon`),
  KEY `idx_chitiethoadon_idMonAn` (`idMonAn`),
  CONSTRAINT `fk_chiTietHoaDon_hoaDon`
    FOREIGN KEY (`idHoaDon`) REFERENCES `hoadon` (`idHoaDon`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_chiTietHoaDon_monAn`
    FOREIGN KEY (`idMonAn`) REFERENCES `monan` (`idMonAn`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `hoadongianhang` (
  `idHoaDonGianHang` int NOT NULL AUTO_INCREMENT,
  `idGianHang` int NOT NULL,
  `tongTien` decimal(12,2) NOT NULL DEFAULT 0.00,
  `ngayHetHan` datetime DEFAULT NULL,
  `trangThai` enum('chua_thanh_toan','da_thanh_toan','qua_han','da_huy') NOT NULL DEFAULT 'chua_thanh_toan',
  `ghiChu` varchar(255) DEFAULT NULL,
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`idHoaDonGianHang`),
  KEY `idx_hoadongianhang_idGianHang` (`idGianHang`),
  CONSTRAINT `fk_hoadongianhang_gianhang`
    FOREIGN KEY (`idGianHang`) REFERENCES `gianhang` (`idGianHang`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER $$

CREATE TRIGGER `trg_phien_vao_app_before_insert`
BEFORE INSERT ON `phien_vao_app`
FOR EACH ROW
BEGIN
  DECLARE v_idThietBi INT DEFAULT NULL;
  DECLARE v_maThietBi VARCHAR(255) DEFAULT NULL;

  IF NEW.idThietBi IS NULL AND NEW.maThietBi IS NOT NULL AND NEW.maThietBi <> '' THEN
    SELECT tb.idThietBi
      INTO v_idThietBi
    FROM thietbi tb
    WHERE tb.maThietBi = NEW.maThietBi
    LIMIT 1;

    SET NEW.idThietBi = v_idThietBi;
  END IF;

  IF (NEW.maThietBi IS NULL OR NEW.maThietBi = '') AND NEW.idThietBi IS NOT NULL THEN
    SELECT tb.maThietBi
      INTO v_maThietBi
    FROM thietbi tb
    WHERE tb.idThietBi = NEW.idThietBi
    LIMIT 1;

    SET NEW.maThietBi = v_maThietBi;
  END IF;
END$$

CREATE TRIGGER `trg_phien_vao_app_before_update`
BEFORE UPDATE ON `phien_vao_app`
FOR EACH ROW
BEGIN
  DECLARE v_idThietBi INT DEFAULT NULL;
  DECLARE v_maThietBi VARCHAR(255) DEFAULT NULL;

  IF NEW.idThietBi IS NULL AND NEW.maThietBi IS NOT NULL AND NEW.maThietBi <> '' THEN
    SELECT tb.idThietBi
      INTO v_idThietBi
    FROM thietbi tb
    WHERE tb.maThietBi = NEW.maThietBi
    LIMIT 1;

    SET NEW.idThietBi = v_idThietBi;
  END IF;

  IF (NEW.maThietBi IS NULL OR NEW.maThietBi = '') AND NEW.idThietBi IS NOT NULL THEN
    SELECT tb.maThietBi
      INTO v_maThietBi
    FROM thietbi tb
    WHERE tb.idThietBi = NEW.idThietBi
    LIMIT 1;

    SET NEW.maThietBi = v_maThietBi;
  END IF;
END$$

DELIMITER ;

INSERT INTO `taikhoan`
  (`idTaiKhoan`, `email`, `matKhau`, `username`, `loaiTaiKhoan`, `tinhTrang`, `tinhTrangDangKy`, `ngayTao`)
VALUES
  (1, '1', '1', '1', 'admin', 'hoat_dong', 'da_duyet', '2026-03-26 22:17:19'),
  (2, 'kh2@gmail.com', '123456', 'khachhang2', 'khach_hang', 'hoat_dong', 'da_duyet', '2026-03-26 22:17:19'),
  (3, 'chu1@demo.com', '123456', 'chuquanly01', 'chu_quan_ly', 'hoat_dong', 'da_duyet', '2026-04-08 23:22:49'),
  (4, 'kh1@gmail.com', '123456', 'khachhang1', 'khach_hang', 'hoat_dong', 'da_duyet', '2026-03-26 22:17:19');

INSERT INTO `admin`
  (`idAdmin`, `idTaiKhoan`, `hoTen`, `ghiChu`, `ngayTao`)
VALUES
  (1, 1, 'Quan tri he thong', 'Tai khoan admin mac dinh', '2026-03-26 22:17:19');

INSERT INTO `chu_quan_ly`
  (`idChuQuanLy`, `idTaiKhoan`, `hoTen`, `sdt`, `diaChi`, `ngayTao`)
VALUES
  (1, 3, 'Nguyen Van A', '0909000009', 'Quan 4', '2026-04-08 23:22:50');

INSERT INTO `khachhang`
  (`idKhachHang`, `idTaiKhoan`, `sdt`)
VALUES
  (1, 4, '0909000001'),
  (2, 2, '0909000002');

INSERT INTO `ngonngu`
  (`idNgonNgu`, `maNgonNgu`, `ten`, `trangThai`)
VALUES
  (1, 'vi', 'Tieng Viet', 'hoat_dong'),
  (2, 'en', 'English', 'hoat_dong'),
  (3, 'ko', 'Korean', 'hoat_dong'),
  (4, 'ja', 'Japanese', 'hoat_dong');

INSERT INTO `goidichvu`
  (`idGoi`, `ten`, `moTa`, `gia`, `thoiHanNgay`, `trangThai`, `ngayTao`)
VALUES
  (1, 'Goi tham quan ngay', 'Truy cap app trong 1 ngay cho du khach.', 15000.00, 1, 'hoat_dong', NOW()),
  (2, 'Goi tham quan tuan', 'Truy cap app trong 7 ngay cho du khach.', 70000.00, 7, 'hoat_dong', NOW()),
  (3, 'Goi tieu chuan gian hang', 'Goi duy tri gian hang theo chu ky thang.', 500000.00, 30, 'hoat_dong', NOW());

INSERT INTO `thietbi`
  (`idThietBi`, `maThietBi`, `maKichHoat`, `idTaiKhoan`, `daKichHoat`, `thoiGianKichHoat`, `ngayTao`, `lanCuoiHoatDong`, `trangThai`)
VALUES
  (1, 'DEVICE-DEMO-001', 'ACT-001', NULL, 1, NOW(), NOW(), NOW(), 'hoat_dong'),
  (2, 'DEVICE-DEMO-002', 'ACT-002', NULL, 0, NULL, NOW(), NULL, 'cho_kich_hoat');

INSERT INTO `phien_vao_app`
  (`id`, `idThietBi`, `maThietBi`, `idGoi`, `qrRaw`, `accessToken`, `batDauLuc`, `hetHanLuc`, `trangThai`)
VALUES
  (1, 1, 'DEVICE-DEMO-001', 1, 'DEMO-QR-001', 'DEMOACCESS0000000000000000000000000000000000000000000000000001', NOW(), DATE_ADD(NOW(), INTERVAL 30 MINUTE), 'hieu_luc');

INSERT INTO `gianhang`
  (`idGianHang`, `idChuQuanLy`, `ten`, `diaChi`, `lat`, `lon`, `vongBo`, `tinhTrang`, `phiHangThang`, `ngayDangKy`, `thoiGianCapNhat`)
VALUES
  (1, 1, 'Banh trang nuong Co Ba', 'Khu A - Pho am thuc Vinh Khanh', 10.7626220, 106.6601720, 10.00, 'dang_hoat_dong', 500000.00, '2026-03-26 22:17:19', '2026-04-09 20:53:40'),
  (2, 1, 'Tra sua May', 'Khu B - Pho am thuc Vinh Khanh', 10.7631000, 106.6609000, 10.00, 'dang_hoat_dong', 701000.00, '2026-03-26 22:17:19', '2026-04-09 20:40:33'),
  (3, 1, 'Xien que 88', 'Khu C - Pho am thuc Vinh Khanh', 10.7634000, 106.6611000, 10.00, 'dang_hoat_dong', 650000.00, '2026-03-26 22:17:19', NULL);

INSERT INTO `gianhangngonngu`
  (`id`, `idGianHang`, `idNgonNgu`, `ten`, `audioURL`, `moTa`)
VALUES
  (1, 1, 1, 'Banh trang nuong Co Ba', 'audio/gianhang_1_vi.mp3', 'Banh trang nuong gion, thom, an kem trung, hanh la va sot dac trung.'),
  (2, 1, 2, 'Co Ba Grilled Rice Paper', 'audio/gianhang_1_en.mp3', 'A booth specializing in crispy and flavorful traditional grilled rice paper.'),
  (3, 2, 1, 'Tra sua May', 'audio/gianhang_2_vi.mp3', 'Tra sua vi nhe, de uong, phu hop cho du khach di bo tham quan pho am thuc.'),
  (4, 2, 2, 'May Milk Tea', 'audio/gianhang_2_en.mp3', 'A milk tea booth serving familiar drinks with a light and refreshing taste.'),
  (5, 3, 1, 'Xien que 88', 'audio/gianhang_3_vi.mp3', 'Gian hang xien que voi nhieu lua chon an vat nong hoi va de thuong.'),
  (6, 3, 2, 'Skewer 88', 'audio/gianhang_3_en.mp3', 'A street-food booth serving assorted skewers that are easy to grab and enjoy.');

INSERT INTO `hinhanhgianhang`
  (`idHinhAnh`, `idGianHang`, `duongDan`)
VALUES
  (130, 1, 'chucchich.jpg'),
  (131, 2, 'mypham.jpg'),
  (132, 3, 'tet.jpg');

INSERT INTO `monan`
  (`idMonAn`, `idGianHang`, `ten`, `donGia`, `thoiGianCapNhat`, `tinhTrang`)
VALUES
  (1, 1, 'Banh trang nuong trung', 20000.00, NOW(), 'con_ban'),
  (2, 1, 'Banh trang nuong pho mai', 25000.00, NOW(), 'con_ban'),
  (3, 2, 'Tra sua truyen thong', 30000.00, NOW(), 'con_ban'),
  (4, 2, 'Tra dao cam sa', 35000.00, NOW(), 'con_ban'),
  (5, 3, 'Xien bo vien', 15000.00, NOW(), 'con_ban'),
  (6, 3, 'Xien ca vien', 12000.00, NOW(), 'con_ban');

INSERT INTO `monanngonngu`
  (`id`, `idMonAn`, `idNgonNgu`, `ten`, `moTa`)
VALUES
  (1, 1, 1, 'Banh trang nuong trung', 'Banh trang nuong voi trung, hanh la va sot.'),
  (2, 1, 2, 'Grilled Rice Paper with Egg', 'Grilled rice paper with egg, scallion, and sauce.'),
  (3, 2, 1, 'Banh trang nuong pho mai', 'Banh trang nuong phu pho mai beo thom.'),
  (4, 2, 2, 'Grilled Rice Paper with Cheese', 'Grilled rice paper topped with creamy cheese.'),
  (5, 3, 1, 'Tra sua truyen thong', 'Tra sua vi truyen thong, thom tra va beo sua.'),
  (6, 3, 2, 'Traditional Milk Tea', 'Classic milk tea with rich tea flavor and creamy milk.'),
  (7, 4, 1, 'Tra dao cam sa', 'Thuc uong ket hop dao, cam va sa thanh mat.'),
  (8, 4, 2, 'Peach Orange Lemongrass Tea', 'A refreshing drink made with peach, orange, and lemongrass.'),
  (9, 5, 1, 'Xien bo vien', 'Bo vien nuong xien que, an kem tuong ot.'),
  (10, 5, 2, 'Beef Ball Skewer', 'Grilled beef ball skewer served with chili sauce.'),
  (11, 6, 1, 'Xien ca vien', 'Ca vien chien hoac nuong, phu hop an vat.'),
  (12, 6, 2, 'Fish Ball Skewer', 'Fried or grilled fish ball skewer, suitable for snacks.');

INSERT INTO `hinhanhmonan`
  (`idHinhAnh`, `idMonAn`, `duongDan`)
VALUES
  (1, 1, 'bt_trung.jpg'),
  (2, 2, 'bt_phomai.jpg'),
  (3, 3, 'ts_truyenthong.jpg'),
  (4, 4, 'tra_dao_cam_sa.jpg'),
  (5, 5, 'xien_bo_vien.jpg'),
  (6, 6, 'xien_ca_vien.jpg');

INSERT INTO `hoadon`
  (`idHoaDon`, `idKhachHang`, `idPhienVaoApp`, `idGoi`, `email`, `tongTien`, `thoiGianTao`, `tinhTrang`, `ghiChu`)
VALUES
  (1, 1, NULL, NULL, 'kh1@gmail.com', 50000.00, '2026-03-26 22:17:19', 'moi_tao', 'It cay'),
  (2, 2, NULL, NULL, 'kh2@gmail.com', 65000.00, '2026-03-26 22:17:19', 'da_thanh_toan', 'Khong da'),
  (3, 1, 1, 1, 'kh1@gmail.com', 15000.00, NOW(), 'da_thanh_toan', 'Mua goi tham quan ngay');

INSERT INTO `chitiethoadon`
  (`id`, `idHoaDon`, `idMonAn`, `soLuong`, `donGia`)
VALUES
  (1, 1, 1, 1, 20000.00),
  (2, 1, 3, 1, 30000.00),
  (3, 2, 4, 1, 35000.00),
  (4, 2, 5, 2, 15000.00);

INSERT INTO `hoadongianhang`
  (`idHoaDonGianHang`, `idGianHang`, `tongTien`, `ngayHetHan`, `trangThai`, `ghiChu`, `ngayTao`)
VALUES
  (1, 1, 500000.00, DATE_ADD(NOW(), INTERVAL 30 DAY), 'chua_thanh_toan', 'Phi duy tri thang hien tai', NOW()),
  (2, 2, 701000.00, DATE_ADD(NOW(), INTERVAL 30 DAY), 'chua_thanh_toan', 'Phi duy tri thang hien tai', NOW()),
  (3, 3, 650000.00, DATE_ADD(NOW(), INTERVAL 30 DAY), 'chua_thanh_toan', 'Phi duy tri thang hien tai', NOW());

ALTER TABLE `taikhoan` AUTO_INCREMENT = 5;
ALTER TABLE `admin` AUTO_INCREMENT = 2;
ALTER TABLE `chu_quan_ly` AUTO_INCREMENT = 2;
ALTER TABLE `khachhang` AUTO_INCREMENT = 3;
ALTER TABLE `ngonngu` AUTO_INCREMENT = 5;
ALTER TABLE `goidichvu` AUTO_INCREMENT = 4;
ALTER TABLE `thietbi` AUTO_INCREMENT = 3;
ALTER TABLE `phien_vao_app` AUTO_INCREMENT = 2;
ALTER TABLE `gianhang` AUTO_INCREMENT = 4;
ALTER TABLE `gianhangngonngu` AUTO_INCREMENT = 7;
ALTER TABLE `hinhanhgianhang` AUTO_INCREMENT = 133;
ALTER TABLE `monan` AUTO_INCREMENT = 7;
ALTER TABLE `monanngonngu` AUTO_INCREMENT = 13;
ALTER TABLE `hinhanhmonan` AUTO_INCREMENT = 7;
ALTER TABLE `hoadon` AUTO_INCREMENT = 4;
ALTER TABLE `chitiethoadon` AUTO_INCREMENT = 5;
ALTER TABLE `hoadongianhang` AUTO_INCREMENT = 4;

SET FOREIGN_KEY_CHECKS = 1;
