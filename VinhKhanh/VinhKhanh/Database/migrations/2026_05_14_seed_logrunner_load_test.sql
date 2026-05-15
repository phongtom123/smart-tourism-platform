-- Seed data for GeofenceLogRunner load testing.
-- Idempotent: re-running this script updates existing SIM rows instead of duplicating them.

DROP PROCEDURE IF EXISTS seed_geofence_logrunner_load_test;

DELIMITER //

CREATE PROCEDURE seed_geofence_logrunner_load_test()
BEGIN
    DECLARE i INT DEFAULT 1;
    DECLARE ownerId INT DEFAULT 1;
    DECLARE centerLat DECIMAL(10,7) DEFAULT 10.7625740;
    DECLARE centerLon DECIMAL(10,7) DEFAULT 106.6607184;
    DECLARE ringNo INT;
    DECLARE slotNo INT;
    DECLARE angleRad DOUBLE;
    DECLARE meters DOUBLE;
    DECLARE latValue DECIMAL(10,7);
    DECLARE lonValue DECIMAL(10,7);
    DECLARE storeName VARCHAR(150);
    DECLARE deviceCode VARCHAR(255);

    SELECT COALESCE(MIN(idChuQuanLy), 1)
    INTO ownerId
    FROM chu_quan_ly;

    WHILE i <= 25 DO
        SET ringNo = 1 + FLOOR((i - 1) / 10);
        SET slotNo = (i - 1) MOD 10;
        SET angleRad = (slotNo * 36 + ringNo * 11) * PI() / 180;
        SET meters = 65 + ringNo * 38;
        SET latValue = centerLat + (SIN(angleRad) * meters / 111320);
        SET lonValue = centerLon + (COS(angleRad) * meters / (111320 * COS(centerLat * PI() / 180)));
        SET storeName = CONCAT('SIM Gian Hang ', LPAD(i, 2, '0'));

        IF EXISTS (SELECT 1 FROM gianhang WHERE ten = storeName LIMIT 1) THEN
            UPDATE gianhang
            SET idChuQuanLy = ownerId,
                diaChi = CONCAT('Load test booth ', LPAD(i, 2, '0'), ', Quan 4'),
                lat = latValue,
                lon = lonValue,
                vongBo = 12 + ((i MOD 5) * 4),
                tinhTrang = 'dang_hoat_dong',
                phiHangThang = 100000 + ((i MOD 9) * 75000),
                thoiGianCapNhat = NOW()
            WHERE ten = storeName;
        ELSE
            INSERT INTO gianhang
                (idChuQuanLy, ten, diaChi, lat, lon, vongBo, luotTruyCap, tinhTrang, phiHangThang, ngayDangKy, thoiGianCapNhat)
            VALUES
                (ownerId, storeName, CONCAT('Load test booth ', LPAD(i, 2, '0'), ', Quan 4'), latValue, lonValue,
                 12 + ((i MOD 5) * 4), 0, 'dang_hoat_dong', 100000 + ((i MOD 9) * 75000), NOW(), NOW());
        END IF;

        SET i = i + 1;
    END WHILE;

    SET i = 1;

    WHILE i <= 50 DO
        SET deviceCode = CONCAT('DEV-', LPAD(i, 3, '0'));

        INSERT INTO thietbi
            (maThietBi, maKichHoat, idTaiKhoan, daKichHoat, thoiGianKichHoat, ngayTao, lanCuoiHoatDong,
             trangThai, loaiThietBi, platform, model, manufacturer, appVersion)
        VALUES
            (deviceCode, CONCAT('SIM-ACT-', LPAD(i, 3, '0')), NULL, 1, NOW(), NOW(), NOW(),
             'hoat_dong', 'app_client', 'Simulator', 'GeofenceLogRunner', 'Codex', 'load-test')
        ON DUPLICATE KEY UPDATE
            daKichHoat = VALUES(daKichHoat),
            thoiGianKichHoat = COALESCE(thoiGianKichHoat, VALUES(thoiGianKichHoat)),
            lanCuoiHoatDong = VALUES(lanCuoiHoatDong),
            trangThai = VALUES(trangThai),
            loaiThietBi = VALUES(loaiThietBi),
            platform = VALUES(platform),
            model = VALUES(model),
            manufacturer = VALUES(manufacturer),
            appVersion = VALUES(appVersion);

        SET i = i + 1;
    END WHILE;

    UPDATE gianhang
    SET tinhTrang = 'tam_ngung',
        thoiGianCapNhat = NOW()
    WHERE ten LIKE 'SIM Gian Hang %'
      AND CAST(SUBSTRING(ten, 14) AS UNSIGNED) > 25;

    UPDATE thietbi
    SET trangThai = 'khoa',
        lanCuoiHoatDong = NOW()
    WHERE maThietBi LIKE 'DEV-%'
      AND CAST(SUBSTRING(maThietBi, 5) AS UNSIGNED) > 50;
END//

DELIMITER ;

CALL seed_geofence_logrunner_load_test();

DROP PROCEDURE IF EXISTS seed_geofence_logrunner_load_test;
