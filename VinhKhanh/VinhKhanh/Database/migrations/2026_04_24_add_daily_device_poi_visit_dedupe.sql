-- Migration: chặn cộng lặp lượt nghe POI theo thiết bị trong cùng một ngày
-- Cùng một maThietBi nghe lại cùng một gian hàng trong ngày sẽ không cộng thêm.
-- Sang ngày mới, thiết bị đó có thể được cộng lại bình thường.

CREATE TABLE IF NOT EXISTS luot_truy_cap_thiet_bi_ngay (
  idLuotTruyCapThietBiNgay INT NOT NULL AUTO_INCREMENT,
  idGianHang INT NOT NULL,
  maThietBi VARCHAR(100) NOT NULL,
  ngay DATE NOT NULL,
  ngayTao DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (idLuotTruyCapThietBiNgay),
  UNIQUE KEY uq_lttbtn_gianhang_thietbi_ngay (idGianHang, maThietBi, ngay),
  KEY idx_lttbtn_ngay (ngay),
  KEY idx_lttbtn_mathietbi_ngay (maThietBi, ngay),
  CONSTRAINT fk_lttbtn_gianhang
    FOREIGN KEY (idGianHang) REFERENCES gianhang (idGianHang)
    ON DELETE CASCADE
    ON UPDATE CASCADE
);
