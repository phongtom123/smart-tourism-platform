-- phpMyAdmin SQL Dump
-- version 5.2.1
-- https://www.phpmyadmin.net/
--
-- Máy chủ: 127.0.0.1
-- Thời gian đã tạo: Th4 16, 2026 lúc 04:22 AM
-- Phiên bản máy phục vụ: 10.4.32-MariaDB
-- Phiên bản PHP: 8.0.30

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Cơ sở dữ liệu: `gianhang`
--

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `admin`
--

CREATE TABLE `admin` (
  `idAdmin` int(11) NOT NULL,
  `idTaiKhoan` int(11) NOT NULL,
  `hoTen` varchar(150) DEFAULT NULL,
  `ghiChu` varchar(255) DEFAULT NULL,
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `admin`
--

INSERT INTO `admin` (`idAdmin`, `idTaiKhoan`, `hoTen`, `ghiChu`, `ngayTao`) VALUES
(1, 1, 'Quan tri he thong', 'Tai khoan admin mac dinh', '2026-03-26 22:17:19');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `chitiethoadon`
--

CREATE TABLE `chitiethoadon` (
  `id` int(11) NOT NULL,
  `idHoaDon` int(11) NOT NULL,
  `idMonAn` int(11) NOT NULL,
  `soLuong` int(11) NOT NULL DEFAULT 1,
  `donGia` decimal(12,2) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `chitiethoadon`
--

INSERT INTO `chitiethoadon` (`id`, `idHoaDon`, `idMonAn`, `soLuong`, `donGia`) VALUES
(1, 1, 1, 1, 20000.00),
(2, 1, 3, 1, 30000.00),
(3, 2, 4, 1, 35000.00),
(4, 2, 5, 2, 15000.00);

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `chu_quan_ly`
--

CREATE TABLE `chu_quan_ly` (
  `idChuQuanLy` int(11) NOT NULL,
  `idTaiKhoan` int(11) NOT NULL,
  `hoTen` varchar(150) DEFAULT NULL,
  `sdt` varchar(20) DEFAULT NULL,
  `diaChi` varchar(255) DEFAULT NULL,
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `chu_quan_ly`
--

INSERT INTO `chu_quan_ly` (`idChuQuanLy`, `idTaiKhoan`, `hoTen`, `sdt`, `diaChi`, `ngayTao`) VALUES
(1, 3, 'Nguyen Van A', '0909000009', 'Quan 4', '2026-04-08 23:22:50');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `gianhang`
--

CREATE TABLE `gianhang` (
  `idGianHang` int(11) NOT NULL,
  `idChuQuanLy` int(11) DEFAULT NULL,
  `ten` varchar(150) NOT NULL,
  `diaChi` varchar(255) DEFAULT NULL,
  `lat` decimal(10,7) DEFAULT NULL,
  `lon` decimal(10,7) DEFAULT NULL,
  `vongBo` decimal(8,2) NOT NULL DEFAULT 10.00,
  `tinhTrang` enum('dang_hoat_dong','tam_ngung','dong_cua') NOT NULL DEFAULT 'dang_hoat_dong',
  `phiHangThang` decimal(12,2) NOT NULL DEFAULT 0.00,
  `ngayDangKy` datetime NOT NULL DEFAULT current_timestamp(),
  `thoiGianCapNhat` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `gianhang`
--

INSERT INTO `gianhang` (`idGianHang`, `idChuQuanLy`, `ten`, `diaChi`, `lat`, `lon`, `vongBo`, `tinhTrang`, `phiHangThang`, `ngayDangKy`, `thoiGianCapNhat`) VALUES
(1, 1, 'Banh trang nuong Co Ba', 'Khu A - Pho am thuc Vinh Khanh', 10.7626220, 106.6601720, 10.00, 'dang_hoat_dong', 500000.00, '2026-03-26 22:17:19', '2026-04-16 00:10:17'),
(2, 1, 'Tra sua May', 'Khu B - Pho am thuc Vinh Khanh', 10.7631000, 106.6609000, 10.00, 'dang_hoat_dong', 701000.00, '2026-03-26 22:17:19', '2026-04-09 20:40:33'),
(3, 1, 'Xien que 888', 'Khu C - Pho am thuc Vinh Khanh', 10.7634000, 106.6611000, 10.00, 'dang_hoat_dong', 650000.00, '2026-03-26 22:17:19', '2026-04-15 21:02:14'),
(4, 1, '321', 'Khu C - Phố ẩm thực Vĩnh Khánh', 0.0000010, 0.0000010, 10.00, 'dang_hoat_dong', 0.00, '2026-04-16 00:17:21', '2026-04-16 00:17:21');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `gianhangngonngu`
--

CREATE TABLE `gianhangngonngu` (
  `id` int(11) NOT NULL,
  `idGianHang` int(11) NOT NULL,
  `idNgonNgu` int(11) NOT NULL,
  `ten` varchar(150) NOT NULL,
  `audioURL` varchar(255) DEFAULT NULL,
  `moTa` text DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `gianhangngonngu`
--

INSERT INTO `gianhangngonngu` (`id`, `idGianHang`, `idNgonNgu`, `ten`, `audioURL`, `moTa`) VALUES
(1, 1, 1, 'Banh trang nuong Co Ba', 'audio/gianhang_1_vi.mp3', 'Banh trang nuong gion, thom, an kem trung, hanh la va sot dac trung.'),
(2, 1, 2, 'Co Ba Grilled Rice Paper', 'audio/gianhang_1_en.mp3', 'A booth specializing in crispy and flavorful traditional grilled rice paper.'),
(3, 2, 1, 'Tra sua May', 'audio/gianhang_2_vi.mp3', 'Tra sua vi nhe, de uong, phu hop cho du khach di bo tham quan pho am thuc.'),
(4, 2, 2, 'May Milk Tea', 'audio/gianhang_2_en.mp3', 'A milk tea booth serving familiar drinks with a light and refreshing taste.'),
(5, 3, 1, 'Xien que 888', 'audio/gianhang_3_vi.mp3', 'Gian hang xien que voi nhieu lua chon an vat nong hoi va de thuong.'),
(6, 3, 2, 'Skewer 88', 'audio/gianhang_3_en.mp3', 'A street-food booth serving assorted skewers that are easy to grab and enjoy.'),
(8, 1, 4, 'Banh trang nuong Co Ba', 'audio/gianhang_1_ja.mp3', 'haha'),
(9, 4, 1, '321', NULL, '2');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `goidichvu`
--

CREATE TABLE `goidichvu` (
  `idGoi` int(11) NOT NULL,
  `ten` varchar(150) NOT NULL,
  `moTa` text DEFAULT NULL,
  `gia` decimal(12,2) NOT NULL DEFAULT 0.00,
  `thoiHanNgay` int(11) NOT NULL DEFAULT 1,
  `trangThai` enum('hoat_dong','tam_ngung','ngung_ap_dung') NOT NULL DEFAULT 'hoat_dong',
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `goidichvu`
--

INSERT INTO `goidichvu` (`idGoi`, `ten`, `moTa`, `gia`, `thoiHanNgay`, `trangThai`, `ngayTao`) VALUES
(1, 'Goi tham quan ngay', 'Truy cap app trong 1 ngay cho du khach.', 15000.00, 1, 'hoat_dong', '2026-04-14 22:52:29'),
(2, 'Goi tham quan tuan', 'Truy cap app trong 7 ngay cho du khach.', 70000.00, 7, 'hoat_dong', '2026-04-14 22:52:29'),
(3, 'Goi tieu chuan gian hang', 'Goi duy tri gian hang theo chu ky thang.', 500000.00, 30, 'tam_ngung', '2026-04-14 22:52:29'),
(4, 'Tokyo Takoyaki', '1', 1000.00, 31, 'ngung_ap_dung', '2026-04-15 23:02:40'),
(6, '1', NULL, 1000.00, 30, 'hoat_dong', '2026-04-15 23:26:25');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `hinhanhgianhang`
--

CREATE TABLE `hinhanhgianhang` (
  `idHinhAnh` int(11) NOT NULL,
  `idGianHang` int(11) NOT NULL,
  `duongDan` varchar(255) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `hinhanhgianhang`
--

INSERT INTO `hinhanhgianhang` (`idHinhAnh`, `idGianHang`, `duongDan`) VALUES
(130, 1, 'chucchich.jpg'),
(131, 2, 'mypham.jpg'),
(132, 3, 'tet.jpg');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `hinhanhmonan`
--

CREATE TABLE `hinhanhmonan` (
  `idHinhAnh` int(11) NOT NULL,
  `idMonAn` int(11) NOT NULL,
  `duongDan` varchar(255) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `hinhanhmonan`
--

INSERT INTO `hinhanhmonan` (`idHinhAnh`, `idMonAn`, `duongDan`) VALUES
(1, 1, 'bt_trung.jpg'),
(2, 2, 'bt_phomai.jpg'),
(3, 3, 'ts_truyenthong.jpg'),
(4, 4, 'tra_dao_cam_sa.jpg'),
(5, 5, 'xien_bo_vien.jpg'),
(6, 6, 'xien_ca_vien.jpg');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `hoadon`
--

CREATE TABLE `hoadon` (
  `idHoaDon` int(11) NOT NULL,
  `idKhachHang` int(11) DEFAULT NULL,
  `idPhienVaoApp` bigint(20) DEFAULT NULL,
  `idGoi` int(11) DEFAULT NULL,
  `email` varchar(100) DEFAULT NULL,
  `tongTien` decimal(12,2) NOT NULL DEFAULT 0.00,
  `thoiGianTao` datetime NOT NULL DEFAULT current_timestamp(),
  `tinhTrang` enum('moi_tao','da_thanh_toan','da_huy','het_han') NOT NULL DEFAULT 'moi_tao',
  `ghiChu` varchar(255) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `hoadon`
--

INSERT INTO `hoadon` (`idHoaDon`, `idKhachHang`, `idPhienVaoApp`, `idGoi`, `email`, `tongTien`, `thoiGianTao`, `tinhTrang`, `ghiChu`) VALUES
(1, NULL, NULL, NULL, 'kh1@gmail.com', 50000.00, '2026-03-26 22:17:19', 'moi_tao', 'It cay'),
(2, NULL, NULL, NULL, 'kh2@gmail.com', 65000.00, '2026-03-26 22:17:19', 'da_thanh_toan', 'Khong da'),
(3, NULL, 1, 1, 'kh1@gmail.com', 15000.00, '2026-04-14 22:52:29', 'da_thanh_toan', 'Mua goi tham quan ngay');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `hoadongianhang`
--

CREATE TABLE `hoadongianhang` (
  `idHoaDonGianHang` int(11) NOT NULL,
  `idGianHang` int(11) NOT NULL,
  `tongTien` decimal(12,2) NOT NULL DEFAULT 0.00,
  `ngayHetHan` datetime DEFAULT NULL,
  `trangThai` enum('chua_thanh_toan','da_thanh_toan','qua_han','da_huy') NOT NULL DEFAULT 'chua_thanh_toan',
  `ghiChu` varchar(255) DEFAULT NULL,
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `hoadongianhang`
--

INSERT INTO `hoadongianhang` (`idHoaDonGianHang`, `idGianHang`, `tongTien`, `ngayHetHan`, `trangThai`, `ghiChu`, `ngayTao`) VALUES
(1, 1, 500000.00, '2026-05-14 22:52:30', 'chua_thanh_toan', 'Phi duy tri thang hien tai', '2026-04-14 22:52:30'),
(2, 2, 701000.00, '2026-05-14 22:52:30', 'chua_thanh_toan', 'Phi duy tri thang hien tai', '2026-04-14 22:52:30'),
(3, 3, 650000.00, '2026-05-14 22:52:30', 'chua_thanh_toan', 'Phi duy tri thang hien tai', '2026-04-14 22:52:30');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `khachhang`
--

CREATE TABLE `khachhang` (
  `idKhachHang` int(11) NOT NULL,
  `idTaiKhoan` int(11) NOT NULL,
  `sdt` varchar(20) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `monan`
--

CREATE TABLE `monan` (
  `idMonAn` int(11) NOT NULL,
  `idGianHang` int(11) NOT NULL,
  `ten` varchar(150) NOT NULL,
  `donGia` decimal(12,2) NOT NULL,
  `thoiGianCapNhat` datetime DEFAULT NULL,
  `tinhTrang` enum('con_ban','het_mon','ngung_ban') NOT NULL DEFAULT 'con_ban'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `monan`
--

INSERT INTO `monan` (`idMonAn`, `idGianHang`, `ten`, `donGia`, `thoiGianCapNhat`, `tinhTrang`) VALUES
(1, 1, 'Banh trang nuong trung', 20000.00, '2026-04-14 22:52:29', 'con_ban'),
(2, 1, 'Banh trang nuong pho mai', 25000.00, '2026-04-14 22:52:29', 'con_ban'),
(3, 2, 'Tra sua truyen thong', 30000.00, '2026-04-14 22:52:29', 'con_ban'),
(4, 2, 'Tra dao cam sa', 35000.00, '2026-04-14 22:52:29', 'con_ban'),
(5, 3, 'Xien bo vien', 15000.00, '2026-04-14 22:52:29', 'con_ban'),
(6, 3, 'Xien ca vien', 12000.00, '2026-04-14 22:52:29', 'con_ban');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `monanngonngu`
--

CREATE TABLE `monanngonngu` (
  `id` int(11) NOT NULL,
  `idMonAn` int(11) NOT NULL,
  `idNgonNgu` int(11) NOT NULL,
  `ten` varchar(150) NOT NULL,
  `moTa` text DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `monanngonngu`
--

INSERT INTO `monanngonngu` (`id`, `idMonAn`, `idNgonNgu`, `ten`, `moTa`) VALUES
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

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `ngonngu`
--

CREATE TABLE `ngonngu` (
  `idNgonNgu` int(11) NOT NULL,
  `maNgonNgu` varchar(10) NOT NULL,
  `ten` varchar(50) NOT NULL,
  `trangThai` enum('hoat_dong','an') NOT NULL DEFAULT 'hoat_dong'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `ngonngu`
--

INSERT INTO `ngonngu` (`idNgonNgu`, `maNgonNgu`, `ten`, `trangThai`) VALUES
(1, 'vi', 'Tieng Viet', 'hoat_dong'),
(2, 'en', 'English', 'hoat_dong'),
(3, 'ko', 'Korean', 'hoat_dong'),
(4, 'ja', 'Japanese', 'hoat_dong');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `phien_vao_app`
--

CREATE TABLE `phien_vao_app` (
  `id` bigint(20) NOT NULL,
  `idThietBi` int(11) DEFAULT NULL,
  `maThietBi` varchar(255) NOT NULL,
  `idGoi` int(11) DEFAULT NULL,
  `qrRaw` text DEFAULT NULL,
  `accessToken` varchar(128) NOT NULL,
  `batDauLuc` datetime NOT NULL DEFAULT current_timestamp(),
  `hetHanLuc` datetime NOT NULL,
  `trangThai` enum('hieu_luc','het_han','huy') NOT NULL DEFAULT 'hieu_luc'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `phien_vao_app`
--

INSERT INTO `phien_vao_app` (`id`, `idThietBi`, `maThietBi`, `idGoi`, `qrRaw`, `accessToken`, `batDauLuc`, `hetHanLuc`, `trangThai`) VALUES
(1, 1, 'DEVICE-DEMO-001', 1, 'DEMO-QR-001', 'DEMOACCESS0000000000000000000000000000000000000000000000000001', '2026-04-14 22:52:29', '2026-04-14 23:22:29', 'het_han'),
(2, 1, 'DEVICE-DEMO-001', NULL, 'DEMO-QR-001', 'A3C3DF0D74C6772FDDFED3B5C2A6E8446C0CA86F59693B905FE41BF0C2BBAE41', '2026-04-14 16:15:25', '2026-04-14 16:45:25', 'het_han'),
(3, 1, 'DEVICE-DEMO-001', NULL, 'DEMO-QR-001', 'BA5B27482BA4C3C990947ABDCBCA919B6176275F22AD359C266472B3E0C16497', '2026-04-14 16:16:54', '2026-04-14 16:46:54', 'het_han'),
(4, 1, 'DEVICE-DEMO-001', 3, 'DEMO-QR-001', '5B0CEB0BD9A9A19FBA369392B92034CBF4845652A47AFC3D2CA5050C320A644E', '2026-04-14 16:27:25', '2026-05-14 16:27:25', 'hieu_luc');

--
-- Bẫy `phien_vao_app`
--
DELIMITER $$
CREATE TRIGGER `trg_phien_vao_app_before_insert` BEFORE INSERT ON `phien_vao_app` FOR EACH ROW BEGIN
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
END
$$
DELIMITER ;
DELIMITER $$
CREATE TRIGGER `trg_phien_vao_app_before_update` BEFORE UPDATE ON `phien_vao_app` FOR EACH ROW BEGIN
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
END
$$
DELIMITER ;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `taikhoan`
--

CREATE TABLE `taikhoan` (
  `idTaiKhoan` int(11) NOT NULL,
  `email` varchar(100) NOT NULL,
  `matKhau` varchar(255) NOT NULL,
  `username` varchar(50) NOT NULL,
  `loaiTaiKhoan` enum('khach_hang','chu_quan_ly','admin') NOT NULL DEFAULT 'khach_hang',
  `tinhTrang` enum('hoat_dong','khoa') NOT NULL DEFAULT 'hoat_dong',
  `tinhTrangDangKy` enum('cho_duyet','da_duyet','tu_choi') NOT NULL DEFAULT 'da_duyet',
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `taikhoan`
--

INSERT INTO `taikhoan` (`idTaiKhoan`, `email`, `matKhau`, `username`, `loaiTaiKhoan`, `tinhTrang`, `tinhTrangDangKy`, `ngayTao`) VALUES
(1, '1', '1', '1', 'admin', 'hoat_dong', 'da_duyet', '2026-03-26 22:17:19'),
(3, 'chu1@demo.com', '123456', 'chuquanly01', 'chu_quan_ly', 'hoat_dong', 'da_duyet', '2026-04-08 23:22:49');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `thietbi`
--

CREATE TABLE `thietbi` (
  `idThietBi` int(11) NOT NULL,
  `maThietBi` varchar(255) NOT NULL,
  `maKichHoat` varchar(100) DEFAULT NULL,
  `idTaiKhoan` int(11) DEFAULT NULL,
  `daKichHoat` tinyint(1) NOT NULL DEFAULT 0,
  `thoiGianKichHoat` datetime DEFAULT NULL,
  `ngayTao` datetime NOT NULL DEFAULT current_timestamp(),
  `lanCuoiHoatDong` datetime DEFAULT NULL,
  `trangThai` enum('cho_kich_hoat','hoat_dong','khoa') NOT NULL DEFAULT 'cho_kich_hoat'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `thietbi`
--

INSERT INTO `thietbi` (`idThietBi`, `maThietBi`, `maKichHoat`, `idTaiKhoan`, `daKichHoat`, `thoiGianKichHoat`, `ngayTao`, `lanCuoiHoatDong`, `trangThai`) VALUES
(1, 'DEVICE-DEMO-001', 'ACT-001', NULL, 1, '2026-04-14 22:52:29', '2026-04-14 22:52:29', '2026-04-14 23:27:25', 'hoat_dong'),
(2, 'DEVICE-DEMO-002', 'ACT-002', NULL, 0, NULL, '2026-04-14 22:52:29', NULL, 'cho_kich_hoat');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `yeucaugianhang`
--

CREATE TABLE `yeucaugianhang` (
  `idYeuCau` int(11) NOT NULL,
  `idChuQuanLy` int(11) NOT NULL,
  `tenDeNghi` varchar(150) NOT NULL,
  `diaChiDeNghi` varchar(255) DEFAULT NULL,
  `ghiChuGui` text DEFAULT NULL,
  `trangThai` enum('cho_duyet','da_duyet','tu_choi') NOT NULL DEFAULT 'cho_duyet',
  `idGianHang` int(11) DEFAULT NULL,
  `ngayGui` datetime NOT NULL DEFAULT current_timestamp(),
  `ngayXuLy` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `yeucaugianhang`
--

INSERT INTO `yeucaugianhang` (`idYeuCau`, `idChuQuanLy`, `tenDeNghi`, `diaChiDeNghi`, `ghiChuGui`, `trangThai`, `idGianHang`, `ngayGui`, `ngayXuLy`) VALUES
(1, 1, 'Bun bo dem', 'Khu E - Pho am thuc Vinh Khanh', 'Toi muon dang ky them gian hang ban vao buoi toi.', 'cho_duyet', NULL, '2026-04-16 00:16:10', NULL),
(2, 1, '321', 'Khu C - Pho am thuc Vinh Khanh', 'Xin mo them mot gian hang nho gan khu trung tam.', 'da_duyet', 4, '2026-04-16 00:17:01', '2026-04-16 00:17:21');

--
-- Chỉ mục cho các bảng đã đổ
--

--
-- Chỉ mục cho bảng `admin`
--
ALTER TABLE `admin`
  ADD PRIMARY KEY (`idAdmin`),
  ADD UNIQUE KEY `uq_admin_idTaiKhoan` (`idTaiKhoan`);

--
-- Chỉ mục cho bảng `chitiethoadon`
--
ALTER TABLE `chitiethoadon`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_chitiethoadon_idHoaDon` (`idHoaDon`),
  ADD KEY `idx_chitiethoadon_idMonAn` (`idMonAn`);

--
-- Chỉ mục cho bảng `chu_quan_ly`
--
ALTER TABLE `chu_quan_ly`
  ADD PRIMARY KEY (`idChuQuanLy`),
  ADD UNIQUE KEY `uq_chuquanly_idTaiKhoan` (`idTaiKhoan`);

--
-- Chỉ mục cho bảng `gianhang`
--
ALTER TABLE `gianhang`
  ADD PRIMARY KEY (`idGianHang`),
  ADD KEY `idx_gianhang_idChuQuanLy` (`idChuQuanLy`),
  ADD KEY `idx_gianhang_toado` (`lat`,`lon`);

--
-- Chỉ mục cho bảng `gianhangngonngu`
--
ALTER TABLE `gianhangngonngu`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uq_gianHang_ngonNgu` (`idGianHang`,`idNgonNgu`),
  ADD KEY `idx_gianhangngonngu_idNgonNgu` (`idNgonNgu`);

--
-- Chỉ mục cho bảng `goidichvu`
--
ALTER TABLE `goidichvu`
  ADD PRIMARY KEY (`idGoi`);

--
-- Chỉ mục cho bảng `hinhanhgianhang`
--
ALTER TABLE `hinhanhgianhang`
  ADD PRIMARY KEY (`idHinhAnh`),
  ADD KEY `idx_hinhanhgianhang_idGianHang` (`idGianHang`);

--
-- Chỉ mục cho bảng `hinhanhmonan`
--
ALTER TABLE `hinhanhmonan`
  ADD PRIMARY KEY (`idHinhAnh`),
  ADD KEY `idx_hinhanhmonan_idMonAn` (`idMonAn`);

--
-- Chỉ mục cho bảng `hoadon`
--
ALTER TABLE `hoadon`
  ADD PRIMARY KEY (`idHoaDon`),
  ADD KEY `idx_hoadon_idKhachHang` (`idKhachHang`),
  ADD KEY `idx_hoadon_idPhienVaoApp` (`idPhienVaoApp`),
  ADD KEY `idx_hoadon_idGoi` (`idGoi`);

--
-- Chỉ mục cho bảng `hoadongianhang`
--
ALTER TABLE `hoadongianhang`
  ADD PRIMARY KEY (`idHoaDonGianHang`),
  ADD KEY `idx_hoadongianhang_idGianHang` (`idGianHang`);

--
-- Chỉ mục cho bảng `khachhang`
--
ALTER TABLE `khachhang`
  ADD PRIMARY KEY (`idKhachHang`),
  ADD UNIQUE KEY `uq_khachhang_idTaiKhoan` (`idTaiKhoan`);

--
-- Chỉ mục cho bảng `monan`
--
ALTER TABLE `monan`
  ADD PRIMARY KEY (`idMonAn`),
  ADD KEY `idx_monan_idGianHang` (`idGianHang`);

--
-- Chỉ mục cho bảng `monanngonngu`
--
ALTER TABLE `monanngonngu`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uq_monAn_ngonNgu` (`idMonAn`,`idNgonNgu`),
  ADD KEY `idx_monanngonngu_idNgonNgu` (`idNgonNgu`);

--
-- Chỉ mục cho bảng `ngonngu`
--
ALTER TABLE `ngonngu`
  ADD PRIMARY KEY (`idNgonNgu`),
  ADD UNIQUE KEY `uq_ngonngu_maNgonNgu` (`maNgonNgu`);

--
-- Chỉ mục cho bảng `phien_vao_app`
--
ALTER TABLE `phien_vao_app`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uq_accessToken` (`accessToken`),
  ADD KEY `idx_phien_vao_app_maThietBi` (`maThietBi`),
  ADD KEY `idx_phien_vao_app_idThietBi` (`idThietBi`),
  ADD KEY `idx_phien_vao_app_idGoi` (`idGoi`);

--
-- Chỉ mục cho bảng `taikhoan`
--
ALTER TABLE `taikhoan`
  ADD PRIMARY KEY (`idTaiKhoan`),
  ADD UNIQUE KEY `uq_taikhoan_email` (`email`),
  ADD UNIQUE KEY `uq_taikhoan_username` (`username`);

--
-- Chỉ mục cho bảng `thietbi`
--
ALTER TABLE `thietbi`
  ADD PRIMARY KEY (`idThietBi`),
  ADD UNIQUE KEY `uq_thietbi_maThietBi` (`maThietBi`),
  ADD KEY `idx_thietbi_idTaiKhoan` (`idTaiKhoan`);

--
-- Chỉ mục cho bảng `yeucaugianhang`
--
ALTER TABLE `yeucaugianhang`
  ADD PRIMARY KEY (`idYeuCau`),
  ADD KEY `idx_yeucaugianhang_owner` (`idChuQuanLy`),
  ADD KEY `idx_yeucaugianhang_status` (`trangThai`),
  ADD KEY `idx_yeucaugianhang_store` (`idGianHang`);

--
-- AUTO_INCREMENT cho các bảng đã đổ
--

--
-- AUTO_INCREMENT cho bảng `admin`
--
ALTER TABLE `admin`
  MODIFY `idAdmin` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=2;

--
-- AUTO_INCREMENT cho bảng `chitiethoadon`
--
ALTER TABLE `chitiethoadon`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=5;

--
-- AUTO_INCREMENT cho bảng `chu_quan_ly`
--
ALTER TABLE `chu_quan_ly`
  MODIFY `idChuQuanLy` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=2;

--
-- AUTO_INCREMENT cho bảng `gianhang`
--
ALTER TABLE `gianhang`
  MODIFY `idGianHang` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=5;

--
-- AUTO_INCREMENT cho bảng `gianhangngonngu`
--
ALTER TABLE `gianhangngonngu`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=10;

--
-- AUTO_INCREMENT cho bảng `goidichvu`
--
ALTER TABLE `goidichvu`
  MODIFY `idGoi` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=8;

--
-- AUTO_INCREMENT cho bảng `hinhanhgianhang`
--
ALTER TABLE `hinhanhgianhang`
  MODIFY `idHinhAnh` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=133;

--
-- AUTO_INCREMENT cho bảng `hinhanhmonan`
--
ALTER TABLE `hinhanhmonan`
  MODIFY `idHinhAnh` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=7;

--
-- AUTO_INCREMENT cho bảng `hoadon`
--
ALTER TABLE `hoadon`
  MODIFY `idHoaDon` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=4;

--
-- AUTO_INCREMENT cho bảng `hoadongianhang`
--
ALTER TABLE `hoadongianhang`
  MODIFY `idHoaDonGianHang` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=4;

--
-- AUTO_INCREMENT cho bảng `khachhang`
--
ALTER TABLE `khachhang`
  MODIFY `idKhachHang` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=3;

--
-- AUTO_INCREMENT cho bảng `monan`
--
ALTER TABLE `monan`
  MODIFY `idMonAn` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=7;

--
-- AUTO_INCREMENT cho bảng `monanngonngu`
--
ALTER TABLE `monanngonngu`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=13;

--
-- AUTO_INCREMENT cho bảng `ngonngu`
--
ALTER TABLE `ngonngu`
  MODIFY `idNgonNgu` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=5;

--
-- AUTO_INCREMENT cho bảng `phien_vao_app`
--
ALTER TABLE `phien_vao_app`
  MODIFY `id` bigint(20) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=5;

--
-- AUTO_INCREMENT cho bảng `taikhoan`
--
ALTER TABLE `taikhoan`
  MODIFY `idTaiKhoan` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=5;

--
-- AUTO_INCREMENT cho bảng `thietbi`
--
ALTER TABLE `thietbi`
  MODIFY `idThietBi` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=3;

--
-- AUTO_INCREMENT cho bảng `yeucaugianhang`
--
ALTER TABLE `yeucaugianhang`
  MODIFY `idYeuCau` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=3;

--
-- Các ràng buộc cho các bảng đã đổ
--

--
-- Các ràng buộc cho bảng `admin`
--
ALTER TABLE `admin`
  ADD CONSTRAINT `fk_admin_taikhoan` FOREIGN KEY (`idTaiKhoan`) REFERENCES `taikhoan` (`idTaiKhoan`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `chitiethoadon`
--
ALTER TABLE `chitiethoadon`
  ADD CONSTRAINT `fk_chiTietHoaDon_hoaDon` FOREIGN KEY (`idHoaDon`) REFERENCES `hoadon` (`idHoaDon`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_chiTietHoaDon_monAn` FOREIGN KEY (`idMonAn`) REFERENCES `monan` (`idMonAn`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `chu_quan_ly`
--
ALTER TABLE `chu_quan_ly`
  ADD CONSTRAINT `fk_chuquanly_taikhoan` FOREIGN KEY (`idTaiKhoan`) REFERENCES `taikhoan` (`idTaiKhoan`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `gianhang`
--
ALTER TABLE `gianhang`
  ADD CONSTRAINT `fk_gianhang_chuquanly` FOREIGN KEY (`idChuQuanLy`) REFERENCES `chu_quan_ly` (`idChuQuanLy`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `gianhangngonngu`
--
ALTER TABLE `gianhangngonngu`
  ADD CONSTRAINT `fk_gianHangNgonNgu_gianHang` FOREIGN KEY (`idGianHang`) REFERENCES `gianhang` (`idGianHang`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_gianHangNgonNgu_ngonNgu` FOREIGN KEY (`idNgonNgu`) REFERENCES `ngonngu` (`idNgonNgu`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `hinhanhgianhang`
--
ALTER TABLE `hinhanhgianhang`
  ADD CONSTRAINT `fk_hinhAnhGianHang_gianHang` FOREIGN KEY (`idGianHang`) REFERENCES `gianhang` (`idGianHang`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `hinhanhmonan`
--
ALTER TABLE `hinhanhmonan`
  ADD CONSTRAINT `fk_hinhAnhMonAn_monAn` FOREIGN KEY (`idMonAn`) REFERENCES `monan` (`idMonAn`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `hoadon`
--
ALTER TABLE `hoadon`
  ADD CONSTRAINT `fk_hoaDon_khachHang` FOREIGN KEY (`idKhachHang`) REFERENCES `khachhang` (`idKhachHang`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_hoadon_goidichvu` FOREIGN KEY (`idGoi`) REFERENCES `goidichvu` (`idGoi`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_hoadon_phienvaoapp` FOREIGN KEY (`idPhienVaoApp`) REFERENCES `phien_vao_app` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `hoadongianhang`
--
ALTER TABLE `hoadongianhang`
  ADD CONSTRAINT `fk_hoadongianhang_gianhang` FOREIGN KEY (`idGianHang`) REFERENCES `gianhang` (`idGianHang`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `khachhang`
--
ALTER TABLE `khachhang`
  ADD CONSTRAINT `fk_khachHang_taiKhoan` FOREIGN KEY (`idTaiKhoan`) REFERENCES `taikhoan` (`idTaiKhoan`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `monan`
--
ALTER TABLE `monan`
  ADD CONSTRAINT `fk_monAn_gianHang` FOREIGN KEY (`idGianHang`) REFERENCES `gianhang` (`idGianHang`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `monanngonngu`
--
ALTER TABLE `monanngonngu`
  ADD CONSTRAINT `fk_monAnNgonNgu_monAn` FOREIGN KEY (`idMonAn`) REFERENCES `monan` (`idMonAn`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_monAnNgonNgu_ngonNgu` FOREIGN KEY (`idNgonNgu`) REFERENCES `ngonngu` (`idNgonNgu`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `phien_vao_app`
--
ALTER TABLE `phien_vao_app`
  ADD CONSTRAINT `fk_phien_vao_app_goidichvu` FOREIGN KEY (`idGoi`) REFERENCES `goidichvu` (`idGoi`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_phien_vao_app_thietbi_id` FOREIGN KEY (`idThietBi`) REFERENCES `thietbi` (`idThietBi`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_phien_vao_app_thietbi_ma` FOREIGN KEY (`maThietBi`) REFERENCES `thietbi` (`maThietBi`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `thietbi`
--
ALTER TABLE `thietbi`
  ADD CONSTRAINT `fk_thietbi_taikhoan` FOREIGN KEY (`idTaiKhoan`) REFERENCES `taikhoan` (`idTaiKhoan`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `yeucaugianhang`
--
ALTER TABLE `yeucaugianhang`
  ADD CONSTRAINT `fk_yeucaugianhang_owner` FOREIGN KEY (`idChuQuanLy`) REFERENCES `chu_quan_ly` (`idChuQuanLy`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_yeucaugianhang_store` FOREIGN KEY (`idGianHang`) REFERENCES `gianhang` (`idGianHang`) ON DELETE SET NULL ON UPDATE CASCADE;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
