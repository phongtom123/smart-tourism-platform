<?php
$auth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($auth['idTaiKhoan']) ? (int) $auth['idTaiKhoan'] : 0;
$accountRole = isset($auth['loaiTaiKhoan']) ? (string) $auth['loaiTaiKhoan'] : '';

function tour_redirect($url)
{
    $url = (string) $url;
    if (!headers_sent()) {
        header('Location: ' . $url);
        exit;
    }

    $escapedUrl = htmlspecialchars($url, ENT_QUOTES, 'UTF-8');
    echo '<script>window.location.href=' . json_encode($url, JSON_UNESCAPED_SLASHES | JSON_HEX_TAG | JSON_HEX_AMP | JSON_HEX_APOS | JSON_HEX_QUOT) . ';</script>';
    echo '<noscript><meta http-equiv="refresh" content="0;url=' . $escapedUrl . '"><a href="' . $escapedUrl . '">Tiep tuc</a></noscript>';
    exit;
}

// Admin-only guard.
if ($accountRole !== 'admin') {
    tour_redirect(admin_url('index1st.php?usecase=store'));
}

$action = isset($_GET['action']) ? trim((string) $_GET['action']) : '';
$selectedId = isset($_GET['id']) ? (int) $_GET['id'] : 0;
$flashMessage = isset($_GET['message']) ? (string) $_GET['message'] : '';
$flashError = isset($_GET['error']) ? (string) $_GET['error'] : '';

function tour_page_url($action = '', $id = 0, $message = '', $error = '')
{
    $params = array('usecase' => 'tour');
    if ($action !== '') $params['action'] = $action;
    if ($id > 0) $params['id'] = $id;
    if ($message !== '') $params['message'] = $message;
    if ($error !== '') $params['error'] = $error;
    return admin_url('index1st.php?' . http_build_query($params));
}

// === Handle POST actions ===
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $postAction = isset($_POST['action']) ? trim((string) $_POST['action']) : '';

    if ($postAction === 'save') {
        $idTour = isset($_POST['idTour']) ? (int) $_POST['idTour'] : 0;
        $stopsRaw = isset($_POST['stops_json']) ? (string) $_POST['stops_json'] : '[]';
        $stops = json_decode($stopsRaw, true);
        if (!is_array($stops)) $stops = array();
        $normalizedStops = array();
        $seenStops = array();
        foreach ($stops as $s) {
            if (!is_array($s)) continue;
            $idGianHang = (int) ($s['idGianHang'] ?? 0);
            if ($idGianHang <= 0 || isset($seenStops[$idGianHang])) continue;
            $seenStops[$idGianHang] = true;
            $normalizedStops[] = $s;
        }

        $payload = array(
            'ten' => trim((string) ($_POST['ten'] ?? '')),
            'moTa' => trim((string) ($_POST['moTa'] ?? '')),
            'idNgonNgu' => isset($_POST['idNgonNgu']) && $_POST['idNgonNgu'] !== '' ? (int) $_POST['idNgonNgu'] : null,
            'doDaiPhutDeXuat' => isset($_POST['doDai']) && $_POST['doDai'] !== '' ? (int) $_POST['doDai'] : null,
            'anhBia' => trim((string) ($_POST['anhBia'] ?? '')) ?: null,
            'danhMuc' => trim((string) ($_POST['danhMuc'] ?? '')) ?: null,
            'tinhTrang' => trim((string) ($_POST['tinhTrang'] ?? 'hoat_dong')),
            'danhSachStop' => array_map(function ($s, $idx) {
                return array(
                    'idGianHang' => (int) ($s['idGianHang'] ?? 0),
                    'thuTu' => $idx + 1,
                    'audioIntroUrl' => isset($s['audioIntroUrl']) && $s['audioIntroUrl'] !== '' ? $s['audioIntroUrl'] : null,
                    'thoiGianDeXuatPhut' => isset($s['thoiGianDeXuatPhut']) && $s['thoiGianDeXuatPhut'] !== '' ? (int) $s['thoiGianDeXuatPhut'] : null,
                    'ghiChu' => isset($s['ghiChu']) && $s['ghiChu'] !== '' ? $s['ghiChu'] : null,
                );
            }, $normalizedStops, array_keys($normalizedStops)),
        );

        if ($payload['ten'] === '') {
            tour_redirect(tour_page_url($idTour > 0 ? 'edit' : 'new', $idTour, '', 'Vui lòng nhập tên tour.'));
            exit;
        }

        $apiError = '';
        $apiHttpCode = 0;
        $path = $idTour > 0 ? 'admin/tour/' . $idTour : 'admin/tour';
        $method = $idTour > 0 ? 'PUT' : 'POST';
        $result = admin_api_call(
            $method,
            $path,
            $payload,
            $apiError,
            $apiHttpCode,
            array('idTaiKhoan' => $idTaiKhoan)
        );

        if ($result === null || empty($result['success'])) {
            $msg = $apiError !== '' ? $apiError : (is_array($result) && !empty($result['message']) ? $result['message'] : 'Lỗi lưu tour.');
            tour_redirect(tour_page_url($idTour > 0 ? 'edit' : 'new', $idTour, '', $msg));
            exit;
        }

        tour_redirect(tour_page_url('', 0, $idTour > 0 ? 'Cập nhật tour thành công.' : 'Tạo tour thành công.'));
        exit;
    }

    if ($postAction === 'delete') {
        $idTour = isset($_POST['idTour']) ? (int) $_POST['idTour'] : 0;
        if ($idTour <= 0) {
            tour_redirect(tour_page_url('', 0, '', 'Thiếu id tour.'));
            exit;
        }
        $apiError = '';
        $apiHttpCode = 0;
        $result = admin_api_call(
            'DELETE',
            'admin/tour/' . $idTour,
            null,
            $apiError,
            $apiHttpCode,
            array('idTaiKhoan' => $idTaiKhoan)
        );
        if ($result === null || empty($result['success'])) {
            $msg = $apiError !== '' ? $apiError : 'Không xóa được tour.';
            tour_redirect(tour_page_url('', 0, '', $msg));
            exit;
        }
        tour_redirect(tour_page_url('', 0, 'Xóa tour thành công.'));
        exit;
    }
}

// === Fetch data ===
$tourError = $flashError;
$tourMessage = $flashMessage;
$apiHttpCode = 0;

if ($action === 'edit' || $action === 'new') {
    $editingTour = null;
    $editingStops = array();
    if ($action === 'edit' && $selectedId > 0) {
        $detail = admin_api_call('GET', 'admin/tour/' . $selectedId, null, $tourError, $apiHttpCode, array('idTaiKhoan' => $idTaiKhoan));
        if (is_array($detail) && isset($detail['tour'])) {
            $editingTour = $detail['tour'];
            $editingStops = isset($detail['danhSachStop']) && is_array($detail['danhSachStop']) ? $detail['danhSachStop'] : array();
        } else if ($tourError === '') {
            $tourError = 'Không tìm thấy tour.';
        }
    }
    $allStores = admin_api_call('GET', 'Admin/stores', null, $storesError, $apiHttpCode, array('idTaiKhoan' => $idTaiKhoan));
    if (!is_array($allStores)) $allStores = array();
} else {
    $tours = admin_api_call('GET', 'admin/tour', null, $tourError, $apiHttpCode, array('idTaiKhoan' => $idTaiKhoan));
    if (!is_array($tours)) $tours = array();

    $statTotal = count($tours);
    $statActive = 0; $statHidden = 0; $statTotalStops = 0;
    foreach ($tours as $t) {
        if (($t['tinhTrang'] ?? '') === 'hoat_dong') $statActive++;
        else if (($t['tinhTrang'] ?? '') === 'an') $statHidden++;
        $statTotalStops += (int) ($t['soStop'] ?? 0);
    }
    $statAvg = $statTotal > 0 ? round($statTotalStops / $statTotal, 1) : 0;

    // Fetch full detail (incl. stops with lat/lon) for each tour to render map.
    // N+1 queries — chap nhan duoc voi <50 tours typical.
    $tourPalette = array('#1ab2d3','#f59e0b','#10b981','#8b5cf6','#ef4444','#06b6d4','#84cc16','#ec4899','#f97316','#3b82f6');
    $toursWithStops = array();
    $allLats = array(); $allLngs = array();
    foreach ($tours as $idx => $t) {
        $idTour = (int) ($t['idTour'] ?? 0);
        if ($idTour <= 0) continue;
        $detailErr = '';
        $detail = admin_api_call('GET', 'admin/tour/' . $idTour, null, $detailErr, $apiHttpCode, array('idTaiKhoan' => $idTaiKhoan));
        $stops = array();
        if (is_array($detail) && isset($detail['danhSachStop']) && is_array($detail['danhSachStop'])) {
            foreach ($detail['danhSachStop'] as $s) {
                if (isset($s['lat']) && isset($s['lon']) && $s['lat'] !== null && $s['lon'] !== null) {
                    $lat = (float) $s['lat']; $lng = (float) $s['lon'];
                    $stops[] = array(
                        'idGianHang' => (int) ($s['idGianHang'] ?? 0),
                        'thuTu' => (int) ($s['thuTu'] ?? 0),
                        'ten' => (string) ($s['tenGianHang'] ?? ''),
                        'lat' => $lat,
                        'lng' => $lng,
                        'hinhAnh' => (string) ($s['hinhAnh'] ?? ''),
                        'isAvailable' => !empty($s['isAvailable']),
                        'gianHangTinhTrang' => (string) ($s['gianHangTinhTrang'] ?? ''),
                    );
                    $allLats[] = $lat; $allLngs[] = $lng;
                }
            }
        }
        $pausedCount = 0;
        foreach ($stops as $st) { if (empty($st['isAvailable'])) $pausedCount++; }
        $toursWithStops[] = array(
            'idTour' => $idTour,
            'ten' => (string) ($t['ten'] ?? ''),
            'moTa' => (string) ($t['moTa'] ?? ''),
            'danhMuc' => (string) ($t['danhMuc'] ?? ''),
            'doDaiPhutDeXuat' => isset($t['doDaiPhutDeXuat']) ? $t['doDaiPhutDeXuat'] : null,
            'tinhTrang' => (string) ($t['tinhTrang'] ?? 'hoat_dong'),
            'soStop' => (int) ($t['soStop'] ?? 0),
            'color' => $tourPalette[$idx % count($tourPalette)],
            'stops' => $stops,
            'pausedStopCount' => $pausedCount,
        );
    }

    // Center map: average of all stops, fallback HCM
    $mapCenterLat = count($allLats) > 0 ? array_sum($allLats) / count($allLats) : 10.762622;
    $mapCenterLng = count($allLngs) > 0 ? array_sum($allLngs) / count($allLngs) : 106.660172;

    $googleMapsApiKey = function_exists('poi_map_google_api_key') ? poi_map_google_api_key() : '';
    $googleMapsMapId = function_exists('poi_map_google_map_id') ? poi_map_google_map_id() : 'DEMO_MAP_ID';
    if ($googleMapsApiKey === '') {
        // Try inline fallback: load same helper from poi_map.php scope
        $browserKeyPath = dirname(__DIR__) . '/Secret/google-maps-browser-key.txt';
        if (is_file($browserKeyPath)) {
            $googleMapsApiKey = trim((string) file_get_contents($browserKeyPath));
        }
        foreach (array('CSA_GOOGLE_MAPS_BROWSER_KEY','GOOGLE_MAPS_BROWSER_KEY') as $k) {
            if ($googleMapsApiKey !== '') break;
            $v = getenv($k); if (is_string($v) && trim($v) !== '') $googleMapsApiKey = trim($v);
        }
    }
}
?>

<div class="tour-page">

  <?php if ($tourMessage !== ''): ?>
  <div class="tour-flash success">
    <i class="fa-solid fa-circle-check"></i>
    <span><?php echo htmlspecialchars($tourMessage, ENT_QUOTES, 'UTF-8'); ?></span>
  </div>
  <?php endif; ?>
  <?php if ($tourError !== ''): ?>
  <div class="tour-flash error">
    <i class="fa-solid fa-circle-exclamation"></i>
    <span><?php echo htmlspecialchars($tourError, ENT_QUOTES, 'UTF-8'); ?></span>
  </div>
  <?php endif; ?>

  <?php if ($action === 'edit' || $action === 'new'): ?>
    <!-- ============ FORM ============ -->
    <div class="tour-page-head">
      <div>
        <h2><?php echo $action === 'new' ? 'Tạo tour mới' : 'Chỉnh sửa tour'; ?></h2>
        <p>Cấu hình hành trình tham quan: thông tin chung và danh sách gian hàng theo thứ tự. Kéo-thả để sắp xếp lại các điểm dừng.</p>
      </div>
      <a href="<?php echo htmlspecialchars(tour_page_url(), ENT_QUOTES, 'UTF-8'); ?>" class="tour-btn-ghost">
        <i class="fa-solid fa-arrow-left"></i> Quay lại
      </a>
    </div>

    <form method="post" id="tourForm" action="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=tour'), ENT_QUOTES, 'UTF-8'); ?>">
      <input type="hidden" name="action" value="save" />
      <input type="hidden" name="idTour" value="<?php echo (int) ($editingTour['idTour'] ?? 0); ?>" />
      <input type="hidden" name="stops_json" id="stopsJson" value="" />

      <div class="tour-form-layout">
        <!-- Left: tour info -->
        <div class="tour-form-card">
          <h3><i class="fa-solid fa-route"></i> Thông tin tour</h3>

          <div class="tour-field">
            <label>Tên tour <span class="req">*</span></label>
            <input type="text" name="ten" required maxlength="255"
                   placeholder="Vd: Hành trình ẩm thực Á Đông"
                   value="<?php echo htmlspecialchars((string) ($editingTour['ten'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
          </div>

          <div class="tour-field">
            <label>Mô tả</label>
            <textarea name="moTa" placeholder="Giới thiệu ngắn gọn về tour..."><?php echo htmlspecialchars((string) ($editingTour['moTa'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></textarea>
          </div>

          <div class="tour-row">
            <div class="tour-field">
              <label>Trạng thái</label>
              <select name="tinhTrang">
                <option value="hoat_dong" <?php echo (($editingTour['tinhTrang'] ?? 'hoat_dong') === 'hoat_dong') ? 'selected' : ''; ?>>Hoạt động</option>
                <option value="an" <?php echo (($editingTour['tinhTrang'] ?? '') === 'an') ? 'selected' : ''; ?>>Ẩn</option>
              </select>
            </div>
            <div class="tour-field">
              <label>Danh mục</label>
              <input type="text" name="danhMuc" placeholder="Vd: ẩm thực, lịch sử, văn hóa..."
                     value="<?php echo htmlspecialchars((string) ($editingTour['danhMuc'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
            </div>
          </div>

          <div class="tour-row">
            <div class="tour-field">
              <label>Độ dài gợi ý (phút)</label>
              <input type="number" name="doDai" min="0"
                     placeholder="60"
                     value="<?php echo htmlspecialchars((string) ($editingTour['doDaiPhutDeXuat'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
            </div>
            <div class="tour-field">
              <label>Ngôn ngữ (id)</label>
              <input type="number" name="idNgonNgu" min="1"
                     placeholder="1 = Tiếng Việt"
                     value="<?php echo htmlspecialchars((string) ($editingTour['idNgonNgu'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
            </div>
          </div>

          <div class="tour-field">
            <label>Ảnh bìa (URL)</label>
            <input type="text" name="anhBia" placeholder="https://..."
                   value="<?php echo htmlspecialchars((string) ($editingTour['anhBia'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
            <p class="hint">Để trống nếu chưa có ảnh.</p>
          </div>

          <div class="tour-form-footer">
            <a href="<?php echo htmlspecialchars(tour_page_url(), ENT_QUOTES, 'UTF-8'); ?>" class="tour-btn-ghost">Hủy</a>
            <button type="submit" class="tour-btn-primary">
              <i class="fa-solid fa-floppy-disk"></i>
              <?php echo $action === 'new' ? 'Tạo tour' : 'Lưu thay đổi'; ?>
            </button>
          </div>
        </div>

        <!-- Right: stops manager -->
        <div class="tour-stops-card">
          <h3>
            <i class="fa-solid fa-location-dot"></i> Điểm dừng
            <span class="tour-stops-counter" id="stopCounter">0</span>
          </h3>

          <div class="tour-stops-pane">
            <h4>
              <i class="fa-solid fa-grip-vertical"></i>
              Trong tour <span style="font-weight:400; color:#94a3b8;">(kéo để sắp xếp)</span>
            </h4>
            <div id="selectedStops" class="tour-stop-list is-droppable"></div>
          </div>

          <div class="tour-stops-pane">
            <h4><i class="fa-solid fa-store"></i> Gian hàng có thể thêm</h4>
            <div class="tour-stops-search">
              <i class="fa-solid fa-magnifying-glass"></i>
              <input type="text" id="storeFilter" placeholder="Lọc theo tên..." />
            </div>
            <div id="availableStores" class="tour-stop-list"></div>
          </div>
        </div>
      </div>
    </form>

    <script src="https://cdnjs.cloudflare.com/ajax/libs/Sortable/1.15.2/Sortable.min.js"></script>
    <script>
    (function() {
      const allStores = <?php echo json_encode($allStores ?: array(), JSON_UNESCAPED_UNICODE); ?>;
      const initialStops = <?php echo json_encode($editingStops ?: array(), JSON_UNESCAPED_UNICODE); ?>;

      const selectedEl = document.getElementById('selectedStops');
      const availableEl = document.getElementById('availableStores');
      const filterInput = document.getElementById('storeFilter');
      const stopsJsonInput = document.getElementById('stopsJson');
      const counterEl = document.getElementById('stopCounter');

      const selectedSeed = new Set();
      let selected = initialStops
        .map(s => ({
          idGianHang: parseInt(s.idGianHang || 0, 10),
          ten: s.tenGianHang || ('GH#' + (s.idGianHang || '')),
          audioIntroUrl: s.audioIntroUrl || '',
          thoiGianDeXuatPhut: s.thoiGianDeXuatPhut || '',
          ghiChu: s.ghiChu || '',
        }))
        .filter(s => {
          if (!s.idGianHang || selectedSeed.has(s.idGianHang)) return false;
          selectedSeed.add(s.idGianHang);
          return true;
        });

      function escapeHtml(s) {
        return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
      }

      function syncStopsJson() {
        stopsJsonInput.value = JSON.stringify(selected);
      }

      function renderSelected() {
        counterEl.textContent = selected.length;
        syncStopsJson();
        if (selected.length === 0) {
          selectedEl.innerHTML = '<div class="tour-stop-empty">Chưa có điểm dừng nào. Chọn từ danh sách bên dưới.</div>';
          return;
        }
        selectedEl.innerHTML = '';
        selected.forEach((s, idx) => {
          const div = document.createElement('div');
          div.className = 'tour-stop-item draggable';
          div.dataset.id = s.idGianHang;
          div.innerHTML = `
            <span class="tour-stop-order">${idx + 1}</span>
            <span class="tour-stop-handle"><i class="fa-solid fa-grip-vertical"></i></span>
            <span class="tour-stop-name">${escapeHtml(s.ten)}<small>ID #${s.idGianHang}</small></span>
            <button type="button" class="tour-stop-action remove" data-id="${s.idGianHang}" title="Bỏ khỏi tour">
              <i class="fa-solid fa-xmark"></i>
            </button>
          `;
          selectedEl.appendChild(div);
        });
        selectedEl.querySelectorAll('.remove').forEach(el => {
          el.addEventListener('click', e => {
            e.stopPropagation();
            const id = parseInt(e.currentTarget.dataset.id);
            selected = selected.filter(x => x.idGianHang !== id);
            renderSelected();
            renderAvailable();
          });
        });
      }

      function renderAvailable() {
        const filter = filterInput.value.toLowerCase().trim();
        availableEl.innerHTML = '';
        const selectedIds = new Set(selected.map(s => s.idGianHang));
        const candidates = allStores
          .map(s => ({ id: parseInt(s.idGianHang || s.id || 0, 10), ten: s.ten || '' }))
          .filter(s => s.id && !selectedIds.has(s.id))
          .filter(s => !filter || s.ten.toLowerCase().indexOf(filter) !== -1);

        if (candidates.length === 0) {
          availableEl.innerHTML = '<div class="tour-stop-empty">' +
            (filter ? 'Không tìm thấy gian hàng phù hợp.' : 'Tất cả gian hàng đã được thêm.') +
            '</div>';
          return;
        }

        candidates.forEach(c => {
          const div = document.createElement('div');
          div.className = 'tour-stop-item add-candidate';
          div.dataset.id = c.id;
          div.innerHTML = `
            <span class="tour-stop-name">${escapeHtml(c.ten)}<small>ID #${c.id}</small></span>
            <button type="button" class="tour-stop-action add" title="Thêm vào tour">
              <i class="fa-solid fa-plus"></i>
            </button>
          `;
          div.addEventListener('click', () => {
            selected.push({ idGianHang: c.id, ten: c.ten, audioIntroUrl: '', thoiGianDeXuatPhut: '', ghiChu: '' });
            renderSelected();
            renderAvailable();
          });
          availableEl.appendChild(div);
        });
      }

      if (window.Sortable && typeof window.Sortable.create === 'function') {
        Sortable.create(selectedEl, {
          animation: 180,
          ghostClass: 'is-dragging',
          onEnd: () => {
            const newOrder = Array.from(selectedEl.children)
              .map(el => parseInt(el.dataset.id || 0, 10))
              .filter(id => id > 0);
            selected = newOrder.map(id => selected.find(s => s.idGianHang === id)).filter(Boolean);
            renderSelected();
          }
        });
      }

      filterInput.addEventListener('input', renderAvailable);

      document.getElementById('tourForm').addEventListener('submit', () => {
        syncStopsJson();
      });

      renderSelected();
      renderAvailable();
    })();
    </script>

  <?php else: ?>
    <!-- ============ LIST ============ -->
    <div class="tour-page-head">
      <div>
        <h2>Quản lý tour</h2>
        <p>Tạo các hành trình tham quan có sẵn cho du khách. Mỗi tour gồm danh sách gian hàng theo thứ tự, app sẽ dẫn đường và phát audio đúng theo trình tự đó.</p>
      </div>
      <a href="<?php echo htmlspecialchars(tour_page_url('new'), ENT_QUOTES, 'UTF-8'); ?>" class="tour-btn-primary">
        <i class="fa-solid fa-plus"></i> Tạo tour mới
      </a>
    </div>

    <div class="tour-stats">
      <div class="tour-stat-card total">
        <div class="tour-stat-icon"><i class="fa-solid fa-layer-group"></i></div>
        <div>
          <div class="tour-stat-value"><?php echo $statTotal; ?></div>
          <div class="tour-stat-label">Tổng số tour</div>
        </div>
      </div>
      <div class="tour-stat-card active">
        <div class="tour-stat-icon"><i class="fa-solid fa-circle-check"></i></div>
        <div>
          <div class="tour-stat-value"><?php echo $statActive; ?></div>
          <div class="tour-stat-label">Đang hoạt động</div>
        </div>
      </div>
      <div class="tour-stat-card hidden">
        <div class="tour-stat-icon"><i class="fa-solid fa-eye-slash"></i></div>
        <div>
          <div class="tour-stat-value"><?php echo $statHidden; ?></div>
          <div class="tour-stat-label">Đang ẩn</div>
        </div>
      </div>
      <div class="tour-stat-card avg">
        <div class="tour-stat-icon"><i class="fa-solid fa-location-dot"></i></div>
        <div>
          <div class="tour-stat-value"><?php echo $statAvg; ?></div>
          <div class="tour-stat-label">TB điểm dừng / tour</div>
        </div>
      </div>
    </div>

    <?php if ($statTotal === 0): ?>
      <div class="tour-table-card">
        <div class="tour-empty">
          <div class="icon"><i class="fa-solid fa-route"></i></div>
          <h3>Chưa có tour nào</h3>
          <p>Bắt đầu bằng cách tạo tour đầu tiên để dẫn dắt du khách qua các gian hàng theo thứ tự bạn chọn.</p>
          <a href="<?php echo htmlspecialchars(tour_page_url('new'), ENT_QUOTES, 'UTF-8'); ?>" class="tour-btn-primary">
            <i class="fa-solid fa-plus"></i> Tạo tour đầu tiên
          </a>
        </div>
      </div>
    <?php else: ?>
      <div class="tour-map-layout">
        <!-- Map -->
        <section class="tour-map-stage">
          <?php if ($googleMapsApiKey === ''): ?>
            <div class="tour-map-no-key">
              <i class="fa-solid fa-key"></i>
              <strong>Chưa có Google Maps key</strong>
              <p>Đặt key vào <code>CS_admin/Secret/google-maps-browser-key.txt</code> để hiện bản đồ.</p>
            </div>
          <?php else: ?>
            <div id="tourGoogleMap" class="tour-google-map">
              <div class="tour-map-loading">
                <i class="fa-solid fa-map-location-dot"></i>
                <span>Đang tải bản đồ...</span>
              </div>
            </div>
            <div class="tour-map-controls">
              <button type="button" id="tourFitAll" title="Hiển thị toàn bộ">
                <i class="fa-solid fa-compress"></i>
              </button>
              <button type="button" id="tourClearSelection" title="Bỏ chọn tour">
                <i class="fa-solid fa-xmark"></i>
              </button>
            </div>
          <?php endif; ?>
        </section>

        <!-- Sidebar: tour cards -->
        <aside class="tour-info-panel">
          <div class="tour-info-head">
            <div>
              <h3>Danh sách tour</h3>
              <p><?php echo $statTotal; ?> tour · <?php echo $statTotalStops * 2; ?> điểm dừng</p>
            </div>
          </div>

          <div class="tour-info-list">
            <?php foreach ($toursWithStops as $tw): ?>
            <div class="tour-info-card" data-id-tour="<?php echo (int) $tw['idTour']; ?>">
              <div class="tour-info-card-head">
                <span class="tour-color-dot" style="background: <?php echo htmlspecialchars($tw['color'], ENT_QUOTES, 'UTF-8'); ?>;"></span>
                <div class="tour-info-name">
                  <strong><?php echo htmlspecialchars($tw['ten'], ENT_QUOTES, 'UTF-8'); ?></strong>
                  <?php if (!empty($tw['danhMuc'])): ?>
                    <small><?php echo htmlspecialchars($tw['danhMuc'], ENT_QUOTES, 'UTF-8'); ?></small>
                  <?php endif; ?>
                </div>
                <span class="tour-status <?php echo htmlspecialchars($tw['tinhTrang'], ENT_QUOTES, 'UTF-8'); ?>">
                  <?php echo $tw['tinhTrang'] === 'an' ? 'Ẩn' : 'Hoạt động'; ?>
                </span>
              </div>

              <?php if (!empty($tw['moTa'])): ?>
              <p class="tour-info-desc"><?php echo htmlspecialchars(mb_substr($tw['moTa'], 0, 100, 'UTF-8') . (mb_strlen($tw['moTa'], 'UTF-8') > 100 ? '…' : ''), ENT_QUOTES, 'UTF-8'); ?></p>
              <?php endif; ?>

              <div class="tour-info-meta">
                <span><i class="fa-solid fa-location-dot"></i> <?php echo $tw['soStop']; ?> điểm</span>
                <?php if ($tw['doDaiPhutDeXuat']): ?>
                <span><i class="fa-regular fa-clock"></i> <?php echo (int) $tw['doDaiPhutDeXuat']; ?> phút</span>
                <?php endif; ?>
              </div>

              <?php if (!empty($tw['pausedStopCount'])): ?>
              <div class="tour-info-warning">
                <i class="fa-solid fa-triangle-exclamation"></i>
                <span><?php echo (int) $tw['pausedStopCount']; ?> gian hàng đang tạm ngưng — du khách sẽ tự động bỏ qua khi đi tour.</span>
              </div>
              <?php endif; ?>

              <?php if (count($tw['stops']) > 0): ?>
              <ol class="tour-info-stops">
                <?php foreach ($tw['stops'] as $s): ?>
                <li class="<?php echo empty($s['isAvailable']) ? 'is-paused' : ''; ?>">
                  <?php echo htmlspecialchars($s['ten'], ENT_QUOTES, 'UTF-8'); ?>
                  <?php if (empty($s['isAvailable'])): ?>
                  <span class="tour-info-stop-badge" title="Gian hàng tạm ngưng — sẽ bị skip">Tạm ngưng</span>
                  <?php endif; ?>
                </li>
                <?php endforeach; ?>
              </ol>
              <?php else: ?>
              <p class="tour-info-empty-stops">Chưa có điểm dừng có tọa độ.</p>
              <?php endif; ?>

              <div class="tour-info-actions">
                <a href="<?php echo htmlspecialchars(tour_page_url('edit', (int) $tw['idTour']), ENT_QUOTES, 'UTF-8'); ?>" class="tour-btn-ghost"><i class="fa-solid fa-pen"></i> Sửa</a>
                <form method="post" action="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=tour'), ENT_QUOTES, 'UTF-8'); ?>" style="display:inline;" onsubmit="return confirm('Xóa tour này?');">
                  <input type="hidden" name="action" value="delete" />
                  <input type="hidden" name="idTour" value="<?php echo (int) $tw['idTour']; ?>" />
                  <button type="submit" class="tour-btn-danger"><i class="fa-solid fa-trash"></i> Xóa</button>
                </form>
              </div>
            </div>
            <?php endforeach; ?>
          </div>
        </aside>
      </div>
    <?php endif; ?>

  <?php endif; ?>
</div>

<?php if (($action !== 'edit' && $action !== 'new') && $statTotal > 0 && $googleMapsApiKey !== ''): ?>
<script>
window.TOUR_MAP_DATA = <?php echo json_encode($toursWithStops, JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP); ?>;
window.TOUR_MAP_CONFIG = {
  center: { lat: <?php echo $mapCenterLat; ?>, lng: <?php echo $mapCenterLng; ?> },
  mapId: <?php echo json_encode($googleMapsMapId); ?>,
  imageProxyUrl: <?php echo json_encode(admin_url('api/image-proxy.php'), JSON_UNESCAPED_SLASHES); ?>
};
</script>
<script src="<?php echo htmlspecialchars(admin_url('asset/admin/js/tour-map.js'), ENT_QUOTES, 'UTF-8'); ?>?v=<?php echo @filemtime(__DIR__ . '/../asset/admin/js/tour-map.js') ?: time(); ?>"></script>
<script async defer src="https://maps.googleapis.com/maps/api/js?key=<?php echo rawurlencode($googleMapsApiKey); ?>&v=weekly&libraries=marker&callback=initTourAdminMap&loading=async"></script>
<?php endif; ?>
