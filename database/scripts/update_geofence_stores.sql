-- Update Test Overlap B to be exactly 5 meters away from A
-- A: 10.7630000, 106.6605000
-- B: 10.7630004, 106.6605002 (approximately 5m away using Haversine)
UPDATE gianhang SET lat = 10.7630004, lon = 106.6605002 WHERE idGianHang = 11;
SELECT 'Updated Test Overlap B coordinates' AS result;
