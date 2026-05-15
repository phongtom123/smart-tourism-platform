<?php
$auth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$idTaiKhoan = isset($auth['idTaiKhoan']) ? (int) $auth['idTaiKhoan'] : 0;
$loaiTaiKhoan = isset($auth['loaiTaiKhoan']) ? (string) $auth['loaiTaiKhoan'] : 'admin';
$idGianHang = isset($_GET['idGianHang']) ? (int) $_GET['idGianHang'] : 0;
$selectedFoodId = isset($_GET['foodId']) ? (int) $_GET['foodId'] : 0;
$isCreateFoodMode = isset($_GET['mode']) && $_GET['mode'] === 'create';
$pageMessage = null;
$pageError = '';

function menu_page_role_prefix($role)
{
    return $role === 'chu_quan_ly' ? 'Owner' : 'Admin';
}

function menu_page_store_detail_path($role, $idGianHang)
{
    return menu_page_role_prefix($role) . '/stores/' . rawurlencode((string) $idGianHang);
}

function menu_page_foods_path($role, $idGianHang)
{
    return menu_page_role_prefix($role) . '/stores/' . rawurlencode((string) $idGianHang) . '/foods';
}

function menu_page_food_collection_path($role)
{
    return menu_page_role_prefix($role) . '/foods';
}

function menu_page_food_detail_path($role, $idMonAn)
{
    return menu_page_role_prefix($role) . '/foods/' . rawurlencode((string) $idMonAn);
}

function menu_page_food_image_url($role, $idTaiKhoan, $idMonAn)
{
    $endpoint = menu_page_role_prefix($role) . '/foods/';
    return backend_api_url($endpoint . rawurlencode((string) $idMonAn) . '/image') . '?idTaiKhoan=' . rawurlencode((string) $idTaiKhoan);
}

function menu_page_proxy_image_url($path)
{
    $path = trim((string) $path);
    if ($path === '') {
        return '';
    }

    return admin_url('api/image-proxy.php') . '?path=' . rawurlencode($path);
}

function menu_page_call_file_upload($url, $fieldName, $fileInfo, &$error, &$httpCode = 0)
{
    $error = '';
    $httpCode = 0;

    if (!function_exists('curl_init') || !class_exists('CURLFile')) {
        $error = 'May chu PHP chua bat cURL/CURLFile de tai anh mon an.';
        return null;
    }

    if (!is_array($fileInfo) || empty($fileInfo['tmp_name']) || !is_file($fileInfo['tmp_name'])) {
        $error = 'Không tìm thấy file tạm để tải lên.';
        return null;
    }

    $mimeType = !empty($fileInfo['type']) ? (string) $fileInfo['type'] : 'application/octet-stream';
    $fileName = !empty($fileInfo['name']) ? (string) $fileInfo['name'] : 'food-image';

    $payload = array(
        $fieldName => new CURLFile($fileInfo['tmp_name'], $mimeType, $fileName),
    );

    $ch = curl_init($url);
    if ($ch === false) {
        $error = 'Khong khoi tao duoc ket noi tai anh mon an.';
        return null;
    }

    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_POST, true);
    curl_setopt($ch, CURLOPT_HTTPHEADER, array('Accept: application/json'));
    curl_setopt($ch, CURLOPT_POSTFIELDS, $payload);
    curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
    curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, false);
    curl_setopt($ch, CURLOPT_CONNECTTIMEOUT, 10);
    curl_setopt($ch, CURLOPT_TIMEOUT, 30);

    $body = curl_exec($ch);
    if ($body === false) {
        $error = curl_error($ch) !== '' ? curl_error($ch) : 'Không thể tải ảnh món ăn lên backend.';
        curl_close($ch);
        return null;
    }

    $httpCode = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);

    $decoded = $body !== '' ? json_decode($body, true) : array();
    if ($httpCode >= 400) {
        if (is_array($decoded) && !empty($decoded['message'])) {
            $error = (string) $decoded['message'];
        } else {
            $error = 'API trả về HTTP ' . $httpCode . '.';
        }
        return null;
    }

    if ($decoded === null && trim((string) $body) !== '') {
        $error = 'Phản hồi tải ảnh món ăn không hợp lệ.';
        return null;
    }

    return is_array($decoded) ? $decoded : array();
}

function menu_page_redirect_url($idGianHang, $selectedFoodId = 0, $flash = '', $createMode = false)
{
    $params = array(
        'usecase' => 'menu',
        'idGianHang' => (int) $idGianHang,
    );

    if ($selectedFoodId > 0) {
        $params['foodId'] = (int) $selectedFoodId;
    }
    if ($flash !== '') {
        $params['flash'] = $flash;
    }
    if ($createMode) {
        $params['mode'] = 'create';
    }

    return admin_url('index1st.php?' . http_build_query($params));
}

function menu_page_redirect($url)
{
    $url = (string) $url;

    if (!headers_sent()) {
        header('Location: ' . $url);
        exit;
    }

    $safeUrl = htmlspecialchars($url, ENT_QUOTES, 'UTF-8');
    $jsonUrl = json_encode($url, JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_AMP | JSON_HEX_QUOT);
    if ($jsonUrl === false) {
        $jsonUrl = '""';
    }

    echo '<script>window.location.href=' . $jsonUrl . ';</script>';
    echo '<noscript><meta http-equiv="refresh" content="0;url=' . $safeUrl . '"></noscript>';
    exit;
}

function menu_page_status_options()
{
    return array(
        'con_ban' => 'Còn bán',
        'het_mon' => 'Hết món',
        'ngung_ban' => 'Ngừng bán',
    );
}

function menu_page_status_meta($status)
{
    $status = strtolower(trim((string) $status));
    $map = array(
        'con_ban' => array('label' => 'Còn bán', 'class' => 'available'),
        'het_mon' => array('label' => 'Hết món', 'class' => 'out'),
        'ngung_ban' => array('label' => 'Ngừng bán', 'class' => 'paused'),
    );

    return isset($map[$status]) ? $map[$status] : $map['con_ban'];
}

function menu_page_format_money($value)
{
    return number_format((float) $value, 0, ',', '.') . ' đ';
}

$flash = isset($_GET['flash']) ? (string) $_GET['flash'] : '';
if ($flash === 'created') {
    $pageMessage = array('type' => 'success', 'text' => 'Đã thêm món ăn mới cho gian hàng.');
} elseif ($flash === 'updated') {
    $pageMessage = array('type' => 'success', 'text' => 'Đã cập nhật món ăn thành công.');
}
if (isset($_GET['image']) && $_GET['image'] === 'failed') {
    $pageMessage = array('type' => 'error', 'text' => 'Món ăn đã được lưu nhưng ảnh chưa cập nhật được. Bạn có thể thử tải ảnh lại.');
}

$storeSummary = null;
$foods = array();
$selectedFood = null;
$formData = array(
    'idMonAn' => 0,
    'ten' => '',
    'donGia' => '',
    'tinhTrang' => 'con_ban',
    'hinhAnh' => '',
);

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['food_form_submit'])) {
    $formData = array(
        'idMonAn' => isset($_POST['idMonAn']) ? (int) $_POST['idMonAn'] : 0,
        'ten' => trim((string) ($_POST['ten'] ?? '')),
        'donGia' => trim((string) ($_POST['donGia'] ?? '')),
        'tinhTrang' => trim((string) ($_POST['tinhTrang'] ?? 'con_ban')),
        'hinhAnh' => trim((string) ($_POST['currentHinhAnh'] ?? '')),
    );
    $uploadedImage = isset($_FILES['foodImage']) && is_array($_FILES['foodImage']) ? $_FILES['foodImage'] : null;
    $hasUploadedImage = $uploadedImage !== null && isset($uploadedImage['error']) && (int) $uploadedImage['error'] === UPLOAD_ERR_OK;

    if ($idGianHang <= 0) {
        $pageMessage = array('type' => 'error', 'text' => 'Không xác định được gian hàng để quản lý món ăn.');
    } elseif ($formData['ten'] === '') {
        $pageMessage = array('type' => 'error', 'text' => 'Tên món ăn không được để trống.');
    } elseif ($formData['donGia'] === '' || !is_numeric($formData['donGia']) || (float) $formData['donGia'] < 0) {
        $pageMessage = array('type' => 'error', 'text' => 'Đơn giá món ăn không hợp lệ.');
    } elseif (!isset(menu_page_status_options()[$formData['tinhTrang']])) {
        $pageMessage = array('type' => 'error', 'text' => 'Trạng thái món ăn không hợp lệ.');
    } else {
        $payload = array(
            'idGianHang' => $idGianHang,
            'ten' => $formData['ten'],
            'donGia' => (float) $formData['donGia'],
            'tinhTrang' => $formData['tinhTrang'],
        );

        if ($formData['idMonAn'] > 0) {
            $apiError = '';
            $apiHttpCode = 0;
            $updateResult = admin_api_call(
                'PUT',
                menu_page_food_detail_path($loaiTaiKhoan, $formData['idMonAn']),
                $payload,
                $apiError,
                $apiHttpCode,
                array('idTaiKhoan' => $idTaiKhoan)
            );

            if ($updateResult === null) {
                $pageMessage = array('type' => 'error', 'text' => 'Cập nhật món ăn thất bại: ' . ($apiError !== '' ? $apiError : 'backend API chưa sẵn sàng.'));
            } else {
                $redirectFoodId = isset($updateResult['idMonAn']) ? (int) $updateResult['idMonAn'] : $formData['idMonAn'];
                $imageFailed = false;
                if ($hasUploadedImage) {
                    $imageError = '';
                    $imageHttpCode = 0;
                    $imageResult = menu_page_call_file_upload(menu_page_food_image_url($loaiTaiKhoan, $idTaiKhoan, $redirectFoodId), 'image', $uploadedImage, $imageError, $imageHttpCode);
                    if ($imageResult === null) {
                        $imageFailed = true;
                    }
                }
                menu_page_redirect(menu_page_redirect_url($idGianHang, $redirectFoodId, 'updated') . ($imageFailed ? '&image=failed' : ''));
            }
        } else {
            $apiError = '';
            $apiHttpCode = 0;
            $createResult = admin_api_call(
                'POST',
                menu_page_food_collection_path($loaiTaiKhoan),
                $payload,
                $apiError,
                $apiHttpCode,
                array('idTaiKhoan' => $idTaiKhoan)
            );

            if ($createResult === null) {
                $pageMessage = array('type' => 'error', 'text' => 'Thêm món ăn thất bại: ' . ($apiError !== '' ? $apiError : 'backend API chưa sẵn sàng.'));
            } else {
                $newFoodId = isset($createResult['idMonAn']) ? (int) $createResult['idMonAn'] : 0;
                $imageFailed = false;
                if ($hasUploadedImage && $newFoodId > 0) {
                    $imageError = '';
                    $imageHttpCode = 0;
                    $imageResult = menu_page_call_file_upload(menu_page_food_image_url($loaiTaiKhoan, $idTaiKhoan, $newFoodId), 'image', $uploadedImage, $imageError, $imageHttpCode);
                    if ($imageResult === null) {
                        $imageFailed = true;
                    }
                }
                menu_page_redirect(menu_page_redirect_url($idGianHang, $newFoodId, 'created') . ($imageFailed ? '&image=failed' : ''));
            }
        }
    }
}

if ($idGianHang <= 0) {
    $pageError = 'Không xác định được gian hàng cần quản lý món ăn.';
} else {
    $detailError = '';
    $detailHttpCode = 0;
    $storeSummary = admin_api_call(
        'GET',
        menu_page_store_detail_path($loaiTaiKhoan, $idGianHang),
        null,
        $detailError,
        $detailHttpCode,
        array('idTaiKhoan' => $idTaiKhoan, 'lang' => 'vi')
    );

    if (!is_array($storeSummary)) {
        $pageError = $detailError !== '' ? $detailError : 'Không tải được thông tin gian hàng: backend API chưa sẵn sàng.';
    } else {
        $foodsError = '';
        $foodsHttpCode = 0;
        $foods = admin_api_call(
            'GET',
            menu_page_foods_path($loaiTaiKhoan, $idGianHang),
            null,
            $foodsError,
            $foodsHttpCode,
            array('idTaiKhoan' => $idTaiKhoan)
        );
        if (!is_array($foods)) {
            $foods = array();
        }

        foreach ($foods as $food) {
            if ((int) ($food['idMonAn'] ?? 0) === $selectedFoodId) {
                $selectedFood = $food;
                break;
            }
        }

        if (!$isCreateFoodMode && $selectedFood === null && count($foods) > 0) {
            $selectedFood = $foods[0];
        }

        if ($_SERVER['REQUEST_METHOD'] !== 'POST' || ($pageMessage !== null && $pageMessage['type'] === 'success')) {
            if ($isCreateFoodMode || $selectedFood === null) {
                $formData = array(
                    'idMonAn' => 0,
                    'ten' => '',
                    'donGia' => '',
                    'tinhTrang' => 'con_ban',
                    'hinhAnh' => '',
                );
            } else {
                $formData = array(
                    'idMonAn' => (int) ($selectedFood['idMonAn'] ?? 0),
                    'ten' => (string) ($selectedFood['ten'] ?? ''),
                    'donGia' => isset($selectedFood['donGia']) ? (string) $selectedFood['donGia'] : '',
                    'tinhTrang' => (string) ($selectedFood['tinhTrang'] ?? 'con_ban'),
                    'hinhAnh' => isset($selectedFood['hinhAnh']) ? (string) $selectedFood['hinhAnh'] : '',
                );
            }
        }
    }
}

$activeCount = 0;
foreach ($foods as $foodRow) {
    if (($foodRow['tinhTrang'] ?? '') === 'con_ban') {
        $activeCount++;
    }
}

$storeDisplayName = isset($storeSummary['ten']) ? (string) $storeSummary['ten'] : 'Gian hàng';
$storeFeeLabel = isset($storeSummary['phiHangThang']) ? menu_page_format_money($storeSummary['phiHangThang']) : 'Chưa có';
$formTitle = $formData['idMonAn'] > 0 ? 'Chỉnh sửa món ăn' : 'Thêm món ăn mới';
$saveButtonLabel = $formData['idMonAn'] > 0 ? 'Lưu món ăn' : 'Tạo món ăn';
$currentFoodImageUrl = isset($formData['hinhAnh']) && $formData['hinhAnh'] !== '' ? menu_page_proxy_image_url($formData['hinhAnh']) : '';
?>
<main class="main-content">
  <section class="menu-management-page">
    <div class="page-head">
      <div>
        <h2>Quản lý món ăn</h2>
        <p>Chủ gian hàng có thể cập nhật tên món, đơn giá và trạng thái bán cho từng món trong gian hàng này.</p>
      </div>

      <div class="page-head-actions">
        <a class="secondary-btn link-btn" href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=branchdetail2&idGianHang=' . (int) $idGianHang), ENT_QUOTES, 'UTF-8'); ?>">Quay lại gian hàng</a>
        <?php if ($pageError === '') { ?>
        <a class="primary-btn link-btn" href="<?php echo htmlspecialchars(menu_page_redirect_url($idGianHang, 0, '', true), ENT_QUOTES, 'UTF-8'); ?>">
          <i class="fa-solid fa-plus"></i>
          <span>Thêm món ăn mới</span>
        </a>
        <?php } ?>
      </div>
    </div>

    <?php if ($pageMessage !== null) { ?>
    <div class="store-edit-alert <?php echo htmlspecialchars($pageMessage['type'], ENT_QUOTES, 'UTF-8'); ?>">
      <?php echo htmlspecialchars($pageMessage['text'], ENT_QUOTES, 'UTF-8'); ?>
    </div>
    <?php } ?>

    <?php if ($pageError !== '') { ?>
    <div class="store-edit-alert error">
      <?php echo htmlspecialchars($pageError, ENT_QUOTES, 'UTF-8'); ?>
    </div>
    <?php } else { ?>
    <div class="stats-row">
      <div class="panel stat-card">
        <p class="stat-label">Gian hàng đang quản lý</p>
        <h3><?php echo htmlspecialchars($storeDisplayName, ENT_QUOTES, 'UTF-8'); ?></h3>
      </div>

      <div class="panel stat-card">
        <p class="stat-label">Tổng số món</p>
        <h3 class="accent"><?php echo (int) count($foods); ?></h3>
      </div>

      <div class="panel stat-card">
        <p class="stat-label">Đang bán</p>
        <h3 class="success-text"><?php echo (int) $activeCount; ?></h3>
      </div>
    </div>

    <div class="menu-layout">
      <div class="panel list-panel">
        <div class="card-head">
          <h3><i class="fa-solid fa-list"></i> Danh sách món ăn</h3>
          <span class="table-note">Phí gian hàng: <?php echo htmlspecialchars($storeFeeLabel, ENT_QUOTES, 'UTF-8'); ?></span>
        </div>

        <?php if (count($foods) === 0) { ?>
        <div class="empty-state">
          <h4>Chưa có món ăn nào</h4>
          <p>Bạn có thể thêm món đầu tiên cho gian hàng này ngay từ khung bên phải.</p>
        </div>
        <?php } else { ?>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Ảnh</th>
                <th>Món ăn</th>
                <th>Đơn giá</th>
                <th>Trạng thái</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <?php foreach ($foods as $food) { ?>
              <?php $statusMeta = menu_page_status_meta($food['tinhTrang'] ?? 'con_ban'); ?>
              <tr class="<?php echo ((int) ($food['idMonAn'] ?? 0) === (int) $formData['idMonAn'] && $formData['idMonAn'] > 0) ? 'selected-row' : ''; ?>">
                <td>
                  <?php $foodImageUrl = !empty($food['hinhAnh']) ? menu_page_proxy_image_url((string) $food['hinhAnh']) : ''; ?>
                  <div class="food-thumb<?php echo $foodImageUrl !== '' ? ' has-image' : ''; ?>"<?php echo $foodImageUrl !== '' ? ' style="background-image:url(\'' . htmlspecialchars($foodImageUrl, ENT_QUOTES, 'UTF-8') . '\')"' : ''; ?>>
                    <?php if ($foodImageUrl === '') { ?>
                    <span><?php echo htmlspecialchars((string) strtoupper(substr((string) ($food['ten'] ?? 'M'), 0, 1)), ENT_QUOTES, 'UTF-8'); ?></span>
                    <?php } ?>
                  </div>
                </td>
                <td>
                  <div class="food-name-cell">
                    <strong><?php echo htmlspecialchars((string) ($food['ten'] ?? 'Món ăn'), ENT_QUOTES, 'UTF-8'); ?></strong>
                    <span>#<?php echo (int) ($food['idMonAn'] ?? 0); ?></span>
                  </div>
                </td>
                <td class="money"><?php echo htmlspecialchars(menu_page_format_money($food['donGia'] ?? 0), ENT_QUOTES, 'UTF-8'); ?></td>
                <td>
                  <span class="status-badge <?php echo htmlspecialchars($statusMeta['class'], ENT_QUOTES, 'UTF-8'); ?>">
                    <span class="mini-dot"></span>
                    <?php echo htmlspecialchars($statusMeta['label'], ENT_QUOTES, 'UTF-8'); ?>
                  </span>
                </td>
                <td class="action-col">
                  <a class="edit-link" href="<?php echo htmlspecialchars(menu_page_redirect_url($idGianHang, (int) ($food['idMonAn'] ?? 0)), ENT_QUOTES, 'UTF-8'); ?>">Chỉnh sửa</a>
                </td>
              </tr>
              <?php } ?>
            </tbody>
          </table>
        </div>
        <?php } ?>
      </div>

      <div class="panel form-panel">
        <div class="card-head">
          <h3><i class="fa-solid fa-bowl-food"></i> <?php echo htmlspecialchars($formTitle, ENT_QUOTES, 'UTF-8'); ?></h3>
          <?php if ($formData['idMonAn'] > 0) { ?>
          <a class="edit-link" href="<?php echo htmlspecialchars(menu_page_redirect_url($idGianHang, 0, '', true), ENT_QUOTES, 'UTF-8'); ?>">Tạo món mới</a>
          <?php } ?>
        </div>

        <form class="menu-form" method="post" enctype="multipart/form-data" action="<?php echo htmlspecialchars(menu_page_redirect_url($idGianHang, $formData['idMonAn'], '', $formData['idMonAn'] <= 0), ENT_QUOTES, 'UTF-8'); ?>">
          <input type="hidden" name="food_form_submit" value="1" />
          <input type="hidden" name="idMonAn" value="<?php echo (int) $formData['idMonAn']; ?>" />
          <input type="hidden" name="currentHinhAnh" value="<?php echo htmlspecialchars((string) $formData['hinhAnh'], ENT_QUOTES, 'UTF-8'); ?>" />

          <div class="food-image-panel">
            <div id="foodImagePreview" class="food-image-preview<?php echo $currentFoodImageUrl !== '' ? ' has-image' : ''; ?>"<?php echo $currentFoodImageUrl !== '' ? ' style="background-image:url(\'' . htmlspecialchars($currentFoodImageUrl, ENT_QUOTES, 'UTF-8') . '\')"' : ''; ?>>
              <?php if ($currentFoodImageUrl === '') { ?>
              <span>Chưa có ảnh món ăn</span>
              <?php } ?>
            </div>

            <label class="form-field">
              <span>Hình ảnh món ăn</span>
              <input id="foodImageInput" type="file" name="foodImage" accept="image/*" />
              <small class="form-help">Bạn có thể thêm hoặc thay ảnh món ăn ngay khi lưu form này.</small>
            </label>
          </div>

          <label class="form-field">
            <span>Tên món ăn</span>
            <input type="text" name="ten" value="<?php echo htmlspecialchars($formData['ten'], ENT_QUOTES, 'UTF-8'); ?>" required />
          </label>

          <label class="form-field">
            <span>Đơn giá</span>
            <input type="number" min="0" step="1000" name="donGia" value="<?php echo htmlspecialchars($formData['donGia'], ENT_QUOTES, 'UTF-8'); ?>" required />
            <small class="form-help">Nhập giá bán theo đơn vị đồng. Chủ gian hàng được phép tự cập nhật giá món ăn.</small>
          </label>

          <label class="form-field">
            <span>Trạng thái</span>
            <select name="tinhTrang">
              <?php foreach (menu_page_status_options() as $statusValue => $statusLabel) { ?>
              <option value="<?php echo htmlspecialchars($statusValue, ENT_QUOTES, 'UTF-8'); ?>" <?php echo $formData['tinhTrang'] === $statusValue ? 'selected' : ''; ?>><?php echo htmlspecialchars($statusLabel, ENT_QUOTES, 'UTF-8'); ?></option>
              <?php } ?>
            </select>
          </label>

          <div class="form-actions">
            <button class="primary-btn" type="submit">
              <i class="fa-solid fa-floppy-disk"></i>
              <span><?php echo htmlspecialchars($saveButtonLabel, ENT_QUOTES, 'UTF-8'); ?></span>
            </button>
          </div>
        </form>

        <div class="menu-help">
          <h4>Gợi ý quản lý</h4>
          <p>Dùng trạng thái <strong>Còn bán</strong> khi món sẵn sàng phục vụ, chuyển sang <strong>Hết món</strong> nếu tạm hết trong ngày, hoặc <strong>Ngừng bán</strong> khi không muốn hiển thị nữa.</p>
        </div>
      </div>
    </div>
    <?php } ?>
  </section>
</main>
<script>
(function () {
  var input = document.getElementById('foodImageInput');
  var preview = document.getElementById('foodImagePreview');
  if (!input || !preview || !window.URL || !window.URL.createObjectURL) {
    return;
  }

  var initialBackground = preview.style.backgroundImage;
  var initialHadImage = preview.classList.contains('has-image');
  var objectUrl = '';

  input.addEventListener('change', function () {
    if (objectUrl) {
      URL.revokeObjectURL(objectUrl);
      objectUrl = '';
    }

    var file = input.files && input.files.length > 0 ? input.files[0] : null;
    if (!file || !file.type || file.type.indexOf('image/') !== 0) {
      preview.style.backgroundImage = initialBackground;
      preview.classList.toggle('has-image', initialHadImage);
      return;
    }

    objectUrl = URL.createObjectURL(file);
    preview.style.backgroundImage = 'url("' + objectUrl.replace(/"/g, '%22') + '")';
    preview.classList.add('has-image');
  });
})();
</script>
