<?php
$auth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($auth['idTaiKhoan']) ? (int) $auth['idTaiKhoan'] : 0;
$loaiTaiKhoan = isset($auth['loaiTaiKhoan']) ? (string) $auth['loaiTaiKhoan'] : 'admin';
$isOwner = $loaiTaiKhoan === 'chu_quan_ly';

if (!function_exists('poi_map_google_api_key')) {
    function poi_map_google_api_key()
    {
        $browserKeyPath = dirname(__DIR__) . '/Secret/google-maps-browser-key.txt';
        if (is_file($browserKeyPath)) {
            $browserKey = trim((string) file_get_contents($browserKeyPath));
            if ($browserKey !== '') {
                return $browserKey;
            }
        }

        $envKeys = array('CSA_GOOGLE_MAPS_BROWSER_KEY', 'GOOGLE_MAPS_BROWSER_KEY');
        foreach ($envKeys as $envKey) {
            $value = getenv($envKey);
            if (is_string($value) && trim($value) !== '') {
                return trim($value);
            }
        }

        return '';
    }
}

if (!function_exists('poi_map_google_api_key_source')) {
    function poi_map_google_api_key_source()
    {
        $browserKeyPath = dirname(__DIR__) . '/Secret/google-maps-browser-key.txt';
        if (is_file($browserKeyPath) && trim((string) file_get_contents($browserKeyPath)) !== '') {
            return 'file';
        }

        $browserEnvKeys = array('CSA_GOOGLE_MAPS_BROWSER_KEY', 'GOOGLE_MAPS_BROWSER_KEY');
        foreach ($browserEnvKeys as $envKey) {
            $value = getenv($envKey);
            if (is_string($value) && trim($value) !== '') {
                return 'env';
            }
        }

        return 'missing';
    }
}

if (!function_exists('poi_map_google_map_id')) {
    function poi_map_google_map_id()
    {
        $envKeys = array('CSA_GOOGLE_MAPS_MAP_ID', 'GOOGLE_MAPS_MAP_ID');
        foreach ($envKeys as $envKey) {
            $value = getenv($envKey);
            if (is_string($value) && trim($value) !== '') {
                return trim($value);
            }
        }

        return 'DEMO_MAP_ID';
    }
}

if (!function_exists('poi_map_image_url')) {
    function poi_map_image_url($path)
    {
        $path = trim((string) $path);
        if ($path === '') {
            return '';
        }

        return admin_url('api/image-proxy.php') . '?path=' . rawurlencode($path);
    }
}

if (!function_exists('poi_map_status_meta')) {
    function poi_map_status_meta($status)
    {
        $status = strtolower(trim((string) $status));
        switch ($status) {
            case 'dang_hoat_dong':
            case 'hoat_dong':
                return array('label' => 'Đang hoạt động', 'class' => 'active');
            case 'tam_ngung':
            case 'tam_dung':
                return array('label' => 'Tạm ngưng', 'class' => 'paused');
            case 'dong_cua':
                return array('label' => 'Đóng cửa', 'class' => 'closed');
            default:
                return array('label' => 'Không rõ', 'class' => 'unknown');
        }
    }
}

if (!function_exists('poi_map_money')) {
    function poi_map_money($value)
    {
        return number_format((float) $value, 0, ',', '.') . 'đ';
    }
}

if (!function_exists('poi_map_fetch_pois')) {
    function poi_map_fetch_pois($isOwner, $idTaiKhoan, &$error)
    {
        $error = '';
        $apiHttpCode = 0;
        $items = $idTaiKhoan > 0
            ? admin_api_call('GET', 'Admin/poi-map', null, $error, $apiHttpCode, array(
                'idTaiKhoan' => $idTaiKhoan,
                'ownerOnly' => $isOwner ? 'true' : 'false',
            ))
            : null;

        if (!is_array($items)) {
            if ($error === '') {
                $error = 'Không thể tải danh sách POI: backend API chưa sẵn sàng.';
            }
            return array();
        }

        $pois = array();
        foreach ($items as $row) {
            $statusMeta = poi_map_status_meta($row['tinhTrang'] ?? '');
            $ownerName = '';
            if (!empty($row['tenChuQuanLy'])) {
                $ownerName = (string) $row['tenChuQuanLy'];
            } elseif (!empty($row['usernameChuQuanLy'])) {
                $ownerName = (string) $row['usernameChuQuanLy'];
            } elseif (!empty($row['emailChuQuanLy'])) {
                $ownerName = (string) $row['emailChuQuanLy'];
            }

            $radius = isset($row['vongBo']) && $row['vongBo'] !== null ? (float) $row['vongBo'] : 10.0;
            if ($radius <= 0) {
                $radius = 10.0;
            }

            $fee = isset($row['phiHangThang']) ? (float) $row['phiHangThang'] : 0.0;
            $pois[] = array(
                'id' => isset($row['idGianHang']) ? (int) $row['idGianHang'] : 0,
                'name' => isset($row['ten']) ? (string) $row['ten'] : 'Gian hàng',
                'address' => !empty($row['diaChi']) ? (string) $row['diaChi'] : 'Chưa cập nhật địa chỉ',
                'lat' => isset($row['lat']) ? (float) $row['lat'] : 0.0,
                'lng' => isset($row['lon']) ? (float) $row['lon'] : 0.0,
                'radiusMeters' => $radius,
                'views' => isset($row['luotTruyCap']) ? (int) $row['luotTruyCap'] : 0,
                'status' => isset($row['tinhTrang']) ? (string) $row['tinhTrang'] : '',
                'statusLabel' => $statusMeta['label'],
                'statusClass' => $statusMeta['class'],
                'monthlyFee' => $fee,
                'monthlyFeeLabel' => poi_map_money($fee),
                'ownerName' => $ownerName !== '' ? $ownerName : 'Chưa gán chủ quản lý',
                'imageUrl' => !empty($row['hinhAnh']) ? poi_map_image_url((string) $row['hinhAnh']) : '',
                'dailyVisits' => isset($row['dailyVisits']) && is_array($row['dailyVisits']) ? $row['dailyVisits'] : array(),
            );
        }

        return $pois;
    }
}

$poiError = '';
$pois = poi_map_fetch_pois($isOwner, $idTaiKhoan, $poiError);
$googleMapsApiKey = poi_map_google_api_key();
$googleMapsApiKeySource = poi_map_google_api_key_source();
$googleMapsMapId = poi_map_google_map_id();

$activeCount = 0;
$pausedCount = 0;
$closedCount = 0;
$totalFee = 0.0;
$centerLat = 10.762622;
$centerLng = 106.660172;

if (count($pois) > 0) {
    $sumLat = 0.0;
    $sumLng = 0.0;
    foreach ($pois as $poi) {
        $sumLat += (float) $poi['lat'];
        $sumLng += (float) $poi['lng'];
        $totalFee += (float) $poi['monthlyFee'];

        if ($poi['statusClass'] === 'active') {
            $activeCount++;
        } elseif ($poi['statusClass'] === 'paused') {
            $pausedCount++;
        } elseif ($poi['statusClass'] === 'closed') {
            $closedCount++;
        }
    }

    $centerLat = $sumLat / count($pois);
    $centerLng = $sumLng / count($pois);
}

$mapPayload = json_encode($pois, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP | JSON_HEX_APOS | JSON_HEX_QUOT);
$mapConfig = json_encode(array(
    'center' => array('lat' => $centerLat, 'lng' => $centerLng),
    'mapId' => $googleMapsMapId,
    'hasApiKey' => $googleMapsApiKey !== '',
    'apiKeySource' => $googleMapsApiKeySource,
    'apiKeyPrefix' => $googleMapsApiKey !== '' ? substr($googleMapsApiKey, 0, 10) . '...' : '',
    'visitsApiUrl' => admin_url('api/poi_visits.php'),
), JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP | JSON_HEX_APOS | JSON_HEX_QUOT);
?>
<main class="main-content">
  <section class="poi-map-page">
    <div class="poi-map-header">
      <div>
        <p class="poi-map-kicker">POI Map 3D</p>
        <h2>Bản đồ POI gian hàng</h2>
        <p>Theo dõi vị trí, vùng geofence và trạng thái của các cửa hàng trên bản đồ nghiêng 3D.</p>
      </div>
      <a class="poi-map-action" href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=store'), ENT_QUOTES, 'UTF-8'); ?>">
        <i class="fa-solid fa-store"></i>
        <span>Danh sách gian hàng</span>
      </a>
    </div>

    <?php if ($poiError !== '') { ?>
    <div class="poi-map-alert error">
      <i class="fa-solid fa-triangle-exclamation"></i>
      <span><?php echo htmlspecialchars($poiError, ENT_QUOTES, 'UTF-8'); ?></span>
    </div>
    <?php } elseif ($googleMapsApiKey === '') { ?>
    <div class="poi-map-alert warning">
      <i class="fa-solid fa-key"></i>
      <span>Chưa cấu hình Google Maps browser key. Đặt key web vào CS_admin/Secret/google-maps-browser-key.txt để hiển thị bản đồ.</span>
    </div>
    <?php } elseif ($googleMapsApiKeySource !== 'file') { ?>
    <div class="poi-map-alert warning">
      <i class="fa-solid fa-globe"></i>
      <span>Đang dùng Google Maps key từ biến môi trường. Nếu Google Maps vẫn không hiện, hãy cho phép key này dùng Maps JavaScript API với HTTP referrer localhost trong Google Cloud.</span>
    </div>
    <?php } ?>

    <div class="poi-map-stats">
      <div class="poi-stat">
        <span>Tổng POI</span>
        <strong><?php echo count($pois); ?></strong>
      </div>
      <div class="poi-stat active">
        <span>Đang hoạt động</span>
        <strong><?php echo $activeCount; ?></strong>
      </div>
      <div class="poi-stat paused">
        <span>Tạm ngưng</span>
        <strong><?php echo $pausedCount; ?></strong>
      </div>
      <div class="poi-stat">
        <span>Phí tháng</span>
        <strong><?php echo htmlspecialchars(poi_map_money($totalFee), ENT_QUOTES, 'UTF-8'); ?></strong>
      </div>
    </div>

    <div class="poi-map-layout">
      <section class="poi-map-stage">
        <div class="poi-map-toolbar">
          <label class="poi-search">
            <i class="fa-solid fa-magnifying-glass"></i>
            <input id="poiMapSearch" type="search" placeholder="Tìm POI, địa chỉ, chủ quản lý" autocomplete="off" />
          </label>
          <div class="poi-map-filter" aria-label="Lọc POI">
            <button type="button" class="active" data-poi-status="all">Tất cả</button>
            <button type="button" data-poi-status="active">Hoạt động</button>
            <button type="button" data-poi-status="paused">Tạm ngưng</button>
            <button type="button" data-poi-status="closed">Đóng cửa</button>
          </div>
        </div>

        <div id="poiGoogleMap" class="poi-google-map" aria-label="Bản đồ POI 3D">
          <div class="poi-map-loading">
            <i class="fa-solid fa-map-location-dot"></i>
            <span>Đang tải Google Maps 3D...</span>
          </div>
        </div>

        <div class="poi-camera-panel" aria-label="Điều khiển camera 3D">
          <label>
            <span>Tilt <output id="poiTiltValue">62</output></span>
            <input id="poiTiltSlider" type="range" min="0" max="68" step="1" value="62" />
          </label>
          <label>
            <span>Rotate <output id="poiHeadingValue">336</output></span>
            <input id="poiHeadingSlider" type="range" min="0" max="359" step="1" value="336" />
          </label>
        </div>

        <div class="poi-map-controls">
          <button type="button" id="poiFitBounds" title="Căn vừa tất cả POI" aria-label="Căn vừa tất cả POI">
            <i class="fa-solid fa-compress"></i>
          </button>
          <button type="button" id="poiToggleHeatmap" title="Bật/tắt Heatmap" aria-label="Bật/tắt Heatmap">
            <i class="fa-solid fa-fire"></i>
          </button>
          <button type="button" id="poiToggleTilt" title="Bật/tắt nghiêng 3D" aria-label="Bật/tắt nghiêng 3D">
            <i class="fa-solid fa-cube"></i>
          </button>
          <button type="button" id="poiRotateLeft" title="Xoay trái" aria-label="Xoay trái">
            <i class="fa-solid fa-rotate-left"></i>
          </button>
          <button type="button" id="poiRotateRight" title="Xoay phải" aria-label="Xoay phải">
            <i class="fa-solid fa-rotate-right"></i>
          </button>
        </div>
      </section>

      <aside class="poi-map-panel">
        <div class="poi-panel-head">
          <div>
            <h3>POI cửa hàng</h3>
            <p><span id="poiVisibleCount"><?php echo count($pois); ?></span> điểm có tọa độ hợp lệ</p>
          </div>
          <span class="poi-live-pill">3D</span>
        </div>

        <div id="poiList" class="poi-list"></div>

        <div class="poi-empty" id="poiEmptyState">
          <i class="fa-regular fa-map"></i>
          <strong>Không có POI phù hợp</strong>
          <span>Thử bộ lọc khác hoặc cập nhật tọa độ gian hàng.</span>
        </div>
      </aside>
    </div>
  </section>
</main>

<script>
window.POI_ADMIN_MAP_DATA = <?php echo $mapPayload ?: '[]'; ?>;
window.POI_ADMIN_MAP_CONFIG = <?php echo $mapConfig ?: '{}'; ?>;
</script>
<script src="<?php echo htmlspecialchars(admin_url('asset/admin/js/poi-map.js'), ENT_QUOTES, 'UTF-8'); ?>?v=<?php echo filemtime(__DIR__ . '/../asset/admin/js/poi-map.js'); ?>"></script>
<?php if ($googleMapsApiKey !== '') { ?>
<script async defer src="https://maps.googleapis.com/maps/api/js?key=<?php echo rawurlencode($googleMapsApiKey); ?>&v=weekly&libraries=marker,visualization&callback=initPoiAdminMap&loading=async"></script>
<?php } else { ?>
<script>
if (window.initPoiAdminMap) {
  window.initPoiAdminMap();
}
</script>
<?php } ?>
<style>
.poi-calendar-heatmap {
  margin-top: 12px;
  overflow-x: auto;
  overflow-y: visible;
  padding: 12px;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.poi-calendar-heatmap-title {
  font-size: 13px;
  font-weight: 700;
  color: #1e293b;
}
.poi-calendar-heatmap-shell {
  position: relative;
  min-width: max-content;
}
.poi-calendar-heatmap-months {
  display: grid;
  grid-template-columns: repeat(53, 12px);
  column-gap: 4px;
  margin-left: 32px;
  margin-bottom: 6px;
  min-height: 14px;
}
.poi-calendar-month {
  font-size: 10px;
  line-height: 1;
  font-weight: 700;
  color: #475569;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  white-space: nowrap;
}
.poi-calendar-heatmap-body {
  display: flex;
  align-items: flex-start;
  gap: 8px;
}
.poi-calendar-heatmap-days {
  width: 24px;
  display: grid;
  grid-template-rows: repeat(7, 12px);
  row-gap: 4px;
}
.poi-calendar-heatmap-days span {
  font-size: 10px;
  line-height: 12px;
  color: #64748b;
}
.poi-calendar-heatmap-days span:not(.is-visible) {
  visibility: hidden;
}
.poi-calendar-heatmap-grid {
  display: grid;
  grid-template-columns: repeat(53, 12px);
  grid-auto-flow: column;
  grid-template-rows: repeat(7, 12px);
  gap: 4px;
  width: max-content;
}
.poi-calendar-day {
  width: 12px;
  height: 12px;
  border-radius: 3px;
  background-color: #ebedf0;
  border: 0;
  padding: 0;
  appearance: none;
  cursor: pointer;
  position: relative;
  transition: transform 0.12s ease, box-shadow 0.12s ease;
}
.poi-calendar-day:hover,
.poi-calendar-day:focus-visible {
  transform: scale(1.18);
  box-shadow: 0 0 0 1px rgba(15, 23, 42, 0.12);
  z-index: 2;
  outline: none;
}
.poi-calendar-day.is-empty {
  background: transparent;
  cursor: default;
  pointer-events: none;
}
.poi-calendar-day.is-legend {
  cursor: default;
}
.poi-calendar-day[data-level="1"] { background-color: #c6e48b; }
.poi-calendar-day[data-level="2"] { background-color: #7bc96f; }
.poi-calendar-day[data-level="3"] { background-color: #239a3b; }
.poi-calendar-day[data-level="4"] { background-color: #196127; }
.poi-calendar-heatmap-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-top: 10px;
}
.poi-calendar-heatmap-summary,
.poi-calendar-legend-label {
  font-size: 11px;
  color: #64748b;
}
.poi-calendar-heatmap-legend {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  white-space: nowrap;
}
.poi-calendar-tooltip {
  position: absolute;
  left: 0;
  top: 0;
  transform: translate(-50%, calc(-100% - 8px));
  padding: 6px 8px;
  border-radius: 8px;
  background: rgba(15, 23, 42, 0.96);
  color: #fff;
  font-size: 11px;
  line-height: 1.3;
  white-space: nowrap;
  pointer-events: none;
  opacity: 0;
  visibility: hidden;
  transition: opacity 0.12s ease;
  z-index: 10;
  box-shadow: 0 8px 24px rgba(15, 23, 42, 0.18);
}
.poi-calendar-tooltip::after {
  content: "";
  position: absolute;
  left: 50%;
  bottom: -4px;
  width: 8px;
  height: 8px;
  background: rgba(15, 23, 42, 0.96);
  transform: translateX(-50%) rotate(45deg);
}
.poi-calendar-tooltip.visible {
  opacity: 1;
  visibility: visible;
}
@media (max-width: 640px) {
  .poi-calendar-heatmap-footer {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
