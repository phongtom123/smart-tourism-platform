<?php
$sidebarActive = isset($sidebarActive) ? $sidebarActive : '';
$sidebarAuth = isset($_SESSION['admin_auth']) && is_array($_SESSION['admin_auth']) ? $_SESSION['admin_auth'] : array();
$sidebarDisplayName = !empty($sidebarAuth['hoTen']) ? (string) $sidebarAuth['hoTen'] : (!empty($sidebarAuth['username']) ? (string) $sidebarAuth['username'] : 'Admin');
$sidebarIsOwner = !empty($sidebarAuth['loaiTaiKhoan']) && $sidebarAuth['loaiTaiKhoan'] === 'chu_quan_ly';
$sidebarRole = $sidebarIsOwner ? 'Chủ quản lý' : 'System Admin';
$sidebarInitial = function_exists('mb_substr')
    ? mb_strtoupper(mb_substr($sidebarDisplayName, 0, 1, 'UTF-8'), 'UTF-8')
    : strtoupper(substr($sidebarDisplayName, 0, 1));

if (!function_exists('sidebar_active_class')) {
    function sidebar_active_class($key, $active)
    {
        return $key === $active ? ' active' : '';
    }
}
?>
<div>
  <div class="brand">
    <div class="brand-icon">
      <i class="fa-solid fa-table-cells-large"></i>
    </div>
    <div class="brand-text">
      <h2>Hệ thống Admin</h2>
      <p>ADMINISTRATOR</p>
    </div>
  </div>

  <nav class="sidebar-nav">
    <?php if (!$sidebarIsOwner) { ?>
    <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=dashboard'), ENT_QUOTES, 'UTF-8'); ?>" class="nav-item<?php echo sidebar_active_class('dashboard', $sidebarActive); ?>" data-sidebar-item="dashboard">
      <i class="fa-solid fa-chart-column"></i>
      <span>Tổng quan</span>
    </a>
    <?php } ?>

    <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=store'), ENT_QUOTES, 'UTF-8'); ?>" class="nav-item<?php echo sidebar_active_class('store', $sidebarActive); ?>" data-sidebar-item="store">
      <i class="fa-solid fa-store"></i>
      <span>Gian hàng</span>
    </a>

    <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=poi-map'), ENT_QUOTES, 'UTF-8'); ?>" class="nav-item<?php echo sidebar_active_class('poi-map', $sidebarActive); ?>" data-sidebar-item="poi-map">
      <i class="fa-solid fa-map-location-dot"></i>
      <span>POI Map</span>
    </a>

    <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=request'), ENT_QUOTES, 'UTF-8'); ?>" class="nav-item<?php echo sidebar_active_class('request', $sidebarActive); ?>" data-sidebar-item="request">
      <i class="fa-solid fa-inbox"></i>
      <span>Yêu cầu</span>
    </a>

    <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=invoice'), ENT_QUOTES, 'UTF-8'); ?>" class="nav-item<?php echo sidebar_active_class('invoice', $sidebarActive); ?>" data-sidebar-item="invoice">
      <i class="fa-solid fa-file-invoice-dollar"></i>
      <span>Hóa đơn</span>
    </a>

    <?php if (!$sidebarIsOwner) { ?>
    <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=account'), ENT_QUOTES, 'UTF-8'); ?>" class="nav-item<?php echo sidebar_active_class('account', $sidebarActive); ?>" data-sidebar-item="account">
      <i class="fa-solid fa-users"></i>
      <span>Tài khoản</span>
    </a>

    <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=service'), ENT_QUOTES, 'UTF-8'); ?>" class="nav-item<?php echo sidebar_active_class('service', $sidebarActive); ?>" data-sidebar-item="service">
      <i class="fa-solid fa-layer-group"></i>
      <span>Dịch vụ</span>
    </a>

    <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=device'), ENT_QUOTES, 'UTF-8'); ?>" class="nav-item<?php echo sidebar_active_class('device', $sidebarActive); ?>" data-sidebar-item="device">
      <i class="fa-solid fa-mobile-screen-button"></i>
      <span>Thiết bị</span>
    </a>

    <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=tour'), ENT_QUOTES, 'UTF-8'); ?>" class="nav-item<?php echo sidebar_active_class('tour', $sidebarActive); ?>" data-sidebar-item="tour">
      <i class="fa-solid fa-route"></i>
      <span>Tour</span>
    </a>
    <?php } ?>
  </nav>
</div>

<div>
  <div class="sidebar-divider"></div>
  <!-- <a href="<?php echo htmlspecialchars(admin_url('index1st.php?usecase=account'), ENT_QUOTES, 'UTF-8'); ?>" class="nav-item settings-link<?php echo sidebar_active_class('settings', $sidebarActive); ?>" data-sidebar-item="settings">
    <i class="fa-solid fa-gear"></i>
    <span>Cài đặt</span>
  </a> -->
  <div class="profile-card">
    <div class="profile-left">
      <div class="profile-avatar"><?php echo htmlspecialchars($sidebarInitial !== '' ? $sidebarInitial : 'A', ENT_QUOTES, 'UTF-8'); ?></div>
      <div class="profile-info">
        <h4><?php echo htmlspecialchars($sidebarDisplayName, ENT_QUOTES, 'UTF-8'); ?></h4>
        <p><?php echo htmlspecialchars($sidebarRole, ENT_QUOTES, 'UTF-8'); ?></p>
      </div>
    </div>
    <form class="logout-form" method="post" action="<?php echo htmlspecialchars(admin_url('logout.php'), ENT_QUOTES, 'UTF-8'); ?>">
      <button class="logout-btn" type="submit" aria-label="Đăng xuất">
        <i class="fa-solid fa-right-from-bracket"></i>
      </button>
    </form>
  </div>
</div>
