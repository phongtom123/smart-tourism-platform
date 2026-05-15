-- Migration: thêm tính năng tour định sẵn
-- Mỗi tour gồm danh sách gian hàng theo thứ tự cố định.
-- Du khách có thể chọn tour, app sẽ dẫn đường + override priority audio
-- sao cho khi đến đúng stop kế tiếp thì stop đó luôn thắng audio.

CREATE TABLE IF NOT EXISTS tour (
  idTour INT NOT NULL AUTO_INCREMENT,
  ten VARCHAR(255) NOT NULL,
  moTa TEXT NULL,
  idNgonNgu INT NULL,
  doDaiPhutDeXuat INT NULL,
  anhBia VARCHAR(500) NULL,
  danhMuc VARCHAR(100) NULL,
  tinhTrang ENUM('hoat_dong', 'an') NOT NULL DEFAULT 'hoat_dong',
  ngayTao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  ngayCapNhat DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (idTour),
  KEY idx_tour_tinhtrang (tinhTrang),
  KEY idx_tour_ngonngu (idNgonNgu)
);

CREATE TABLE IF NOT EXISTS tour_diem (
  idTourDiem INT NOT NULL AUTO_INCREMENT,
  idTour INT NOT NULL,
  idGianHang INT NOT NULL,
  thuTu INT NOT NULL,
  audioIntroUrl VARCHAR(500) NULL,        -- audio dẫn dắt riêng cho stop này; null = dùng audio mặc định của gian hàng
  thoiGianDeXuatPhut INT NULL,            -- thời gian gợi ý dừng tại stop
  ghiChu TEXT NULL,
  PRIMARY KEY (idTourDiem),
  UNIQUE KEY uq_tour_thutu (idTour, thuTu),
  UNIQUE KEY uq_tour_gianhang (idTour, idGianHang),
  KEY idx_tour_diem_tour (idTour),
  CONSTRAINT fk_tour_diem_tour
    FOREIGN KEY (idTour) REFERENCES tour (idTour)
    ON DELETE CASCADE
    ON UPDATE CASCADE,
  CONSTRAINT fk_tour_diem_gianhang
    FOREIGN KEY (idGianHang) REFERENCES gianhang (idGianHang)
    ON DELETE CASCADE
    ON UPDATE CASCADE
);

CREATE TABLE IF NOT EXISTS tour_tien_do (
  idTourTienDo INT NOT NULL AUTO_INCREMENT,
  idTour INT NOT NULL,
  maThietBi VARCHAR(100) NOT NULL,
  stepHienTai INT NOT NULL DEFAULT 0,     -- thuTu của stop kế tiếp; 0 = chưa bắt đầu
  startedAt DATETIME NULL,
  completedAt DATETIME NULL,
  ngayCapNhat DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (idTourTienDo),
  UNIQUE KEY uq_tien_do_tour_thietbi (idTour, maThietBi),
  KEY idx_tien_do_completed (completedAt),
  CONSTRAINT fk_tien_do_tour
    FOREIGN KEY (idTour) REFERENCES tour (idTour)
    ON DELETE CASCADE
    ON UPDATE CASCADE
);
