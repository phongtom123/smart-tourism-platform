<?php
require_once __DIR__ . '/connect.php';
session_start();

if (isset($_SESSION['admin_auth']) && !empty($_SESSION['admin_auth']['isLoggedIn'])) {
    $redirectUseCase = 'store';

    header('Location: ' . admin_url('index1st.php?usecase=' . $redirectUseCase));
    exit;
}

$initialMode = isset($_GET['mode']) && $_GET['mode'] === 'register' ? 'register' : 'login';
$authCssVersion = @filemtime(__DIR__ . '/asset/admin/css/auth.css') ?: time();
$authJsVersion = @filemtime(__DIR__ . '/asset/admin/js/auth.js') ?: time();
?>
<!DOCTYPE html>
<html lang="vi">

<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Đăng nhập / Đăng ký</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;500;600;700;800&display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.0/css/all.min.css" />
    <link rel="stylesheet" href="asset/admin/css/auth.css?v=<?php echo rawurlencode((string) $authCssVersion); ?>" />

</head>

<body>
    <div class="auth-page">
        <div id="authShell" class="auth-shell <?php echo $initialMode === 'register' ? 'register-mode' : ''; ?>">
            <section class="pane login-pane">
                <div class="brand-mini">
                    <span class="dot"><i class="fa-solid fa-shield-halved"></i></span>
                    Hệ thống quản lý
                </div>
                <h1>Đăng nhập</h1>
                <p class="sub">Chào mừng quay lại. Admin và Chủ quản lý đều có thể đăng nhập tại đây để vào đúng khu vực làm việc của mình.</p>

<form class="form" id="loginForm" action="#" method="post" novalidate>
                    <div class="field">
                        <label for="loginAccount">Username hoặc Email</label>
                        <div class="input-wrap">
                            <i class="fa-regular fa-user"></i>
                            <input id="loginAccount" name="account" type="text" placeholder="Nhập username hoặc email" required />
                        </div>
                        <div class="field-help">Hỗ trợ đăng nhập cho <strong>Admin</strong> và <strong>Chủ quản lý</strong>.</div>
                    </div>

    <div class="field">
        <label for="loginPassword">Mật khẩu</label>
        <div class="input-wrap">
            <i class="fa-solid fa-lock"></i>
            <input id="loginPassword" name="password" type="password" placeholder="••••••••" required />
            <button class="toggle-pass" type="button" data-toggle-password="loginPassword" aria-label="Hiện mật khẩu" aria-pressed="false">
                <i class="fa-regular fa-eye"></i>
            </button>
        </div>
    </div>

    <div class="row">
        <label><input type="checkbox" id="rememberLogin" /> Ghi nhớ đăng nhập</label>
        <a href="#">Quên mật khẩu?</a>
    </div>

    <button class="btn" type="submit" id="loginSubmitBtn">Đăng nhập</button>
    <div class="field-error" id="loginError"></div>
    <div class="ok-msg" id="loginMsg"></div>
</form>

                <p class="toggle-link">
                    Chưa có tài khoản?
                    <button type="button" data-to-register>Thì Đăng ký</button>
                </p>
            </section>

            <section class="pane register-pane">
                <div class="brand-mini">
                    <span class="dot"><i class="fa-solid fa-user-plus"></i></span>
                    Tạo tài khoản chủ quản lý
                </div>
                <h1>Đăng ký</h1>
                <p class="sub">Tạo tài khoản chủ quản lý để bắt đầu sử dụng hệ thống. Sau khi đăng ký thành công, bạn có thể đăng nhập ngay tại đây.</p>

                <form class="form" id="registerForm" action="#" method="post" novalidate>
                    <div class="field">
                        <label for="registerFullName">Họ và tên</label>
                        <div class="input-wrap">
                            <i class="fa-regular fa-user"></i>
                            <input id="registerFullName" name="full_name" type="text" placeholder="Nguyễn Văn A" required />
                        </div>
                    </div>

                    <div class="field">
                        <label for="registerUsername">Tên đăng nhập</label>
                        <div class="input-wrap">
                            <i class="fa-solid fa-at"></i>
                            <input id="registerUsername" name="username" type="text" placeholder="chuquanly01" required />
                        </div>
                    </div>

                    <div class="field">
                        <label for="registerEmail">Email</label>
                        <div class="input-wrap">
                            <i class="fa-regular fa-envelope"></i>
                            <input id="registerEmail" name="email" type="email" placeholder="name@email.com" required />
                        </div>
                    </div>

                    <div class="field">
                        <label for="registerPassword">Mật khẩu</label>
                        <div class="input-wrap">
                            <i class="fa-solid fa-lock"></i>
                            <input id="registerPassword" name="password" type="password" placeholder="Tối thiểu 8 ký tự" minlength="8" required />
                            <button class="toggle-pass" type="button" data-toggle-password="registerPassword" aria-label="Hiện mật khẩu" aria-pressed="false">
                                <i class="fa-regular fa-eye"></i>
                            </button>
                        </div>
                    </div>

                    <div class="field">
                        <label for="registerConfirmPassword">Xác nhận mật khẩu</label>
                        <div class="input-wrap" id="confirmPasswordWrap">
                            <i class="fa-solid fa-lock"></i>
                            <input id="registerConfirmPassword" name="confirm_password" type="password" placeholder="Nhập lại mật khẩu" minlength="8" required />
                            <button class="toggle-pass" type="button" data-toggle-password="registerConfirmPassword" aria-label="Hiện mật khẩu" aria-pressed="false">
                                <i class="fa-regular fa-eye"></i>
                            </button>
                        </div>
                        <p class="field-error" id="confirmPasswordError">Mật khẩu xác nhận không khớp.</p>
                    </div>

                    <button class="btn" type="submit">Đăng ký tài khoản</button>
                    <div class="field-error" id="registerError"></div>
                    <div class="ok-msg" id="registerMsg"></div>
                </form>

                <p class="toggle-link">
                    Đã có tài khoản?
                    <button type="button" data-to-login>Thì Đăng nhập</button>
                </p>
            </section>

            <aside class="welcome">
                <div>
                    <div class="pill">
                        <i class="fa-solid fa-sparkles"></i>
                        Đăng nhập theo vai trò
                    </div>
                    <h2>Admin và<br />Chủ quản lý<br />dùng chung một cổng</h2>
                    <p>Hệ thống sẽ tự nhận diện vai trò sau khi đăng nhập và chuyển bạn đến đúng trang quản lý tương ứng.</p>
                </div>
            </aside>
        </div>
    </div>

    <script src="asset/admin/js/auth.js?v=<?php echo rawurlencode((string) $authJsVersion); ?>"></script>
</body>

</html>
