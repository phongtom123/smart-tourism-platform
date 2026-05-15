INSERT INTO gianhang (idGianHang, idChuQuanLy, ten, diaChi, lat, lon, vongBo, tinhTrang, phiHangThang)
VALUES 
(10, 1, 'Test Overlap A', 'Test Location A', 10.7630000, 106.6605000, 10.00, 'dang_hoat_dong', 500000.00),
(11, 1, 'Test Overlap B', 'Test Location B', 10.7630002, 106.6605005, 10.00, 'dang_hoat_dong', 500000.00);

INSERT INTO gianhangngonngu (id, idGianHang, idNgonNgu, ten, audioURL, moTa)
VALUES 
(100, 10, 1, 'Test Overlap A', 'audio/test_a_vi.mp3', 'Gian hang test A'),
(101, 11, 1, 'Test Overlap B', 'audio/test_b_vi.mp3', 'Gian hang test B');
