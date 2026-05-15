(function () {
    var shell = document.getElementById('authShell');
    var toRegister = document.querySelector('[data-to-register]');
    var toLogin = document.querySelector('[data-to-login]');
    var loginForm = document.getElementById('loginForm');
    var registerForm = document.getElementById('registerForm');
    var loginMsg = document.getElementById('loginMsg');
    var registerMsg = document.getElementById('registerMsg');
    var registerError = document.getElementById('registerError');
    var loginError = document.getElementById('loginError');
    var loginSubmitBtn = document.getElementById('loginSubmitBtn');
    var registerSubmitBtn = registerForm ? registerForm.querySelector('button[type="submit"]') : null;
    var registerFullName = document.getElementById('registerFullName');
    var registerUsername = document.getElementById('registerUsername');
    var registerEmail = document.getElementById('registerEmail');
    var passwordToggles = document.querySelectorAll('[data-toggle-password]');
    var registerPassword = document.getElementById('registerPassword');
    var registerConfirmPassword = document.getElementById('registerConfirmPassword');
    var confirmPasswordWrap = document.getElementById('confirmPasswordWrap');
    var confirmPasswordError = document.getElementById('confirmPasswordError');
    var confirmTouched = false;

    function showRegister() {
        clearLoginError();
        clearLoginSuccess();
        shell.classList.add('register-mode');
    }

    function showLogin() {
        clearRegisterError();
        clearRegisterSuccess();
        shell.classList.remove('register-mode');
    }

    function showLoginError(message) {
        if (!loginError) return;
        loginError.textContent = message;
        loginError.classList.add('show');
    }

    function clearLoginError() {
        if (!loginError) return;
        loginError.textContent = '';
        loginError.classList.remove('show');
    }

    function showLoginSuccess(message) {
        if (!loginMsg) return;
        loginMsg.textContent = message;
        loginMsg.classList.add('show');
    }

    function clearLoginSuccess() {
        if (!loginMsg) return;
        loginMsg.textContent = '';
        loginMsg.classList.remove('show');
    }

    function showRegisterError(message) {
        if (!registerError) return;
        clearRegisterSuccess();
        registerError.textContent = message;
        registerError.classList.add('show');
    }

    function clearRegisterError() {
        if (!registerError) return;
        registerError.textContent = '';
        registerError.classList.remove('show');
    }

    function showRegisterSuccess(message) {
        if (!registerMsg) return;
        clearRegisterError();
        registerMsg.textContent = message;
        registerMsg.classList.add('show');
    }

    function clearRegisterSuccess() {
        if (!registerMsg) return;
        registerMsg.textContent = '';
        registerMsg.classList.remove('show');
    }

    if (toRegister) {
        toRegister.addEventListener('click', showRegister);
    }

    if (toLogin) {
        toLogin.addEventListener('click', showLogin);
    }

    if (loginForm) {
        loginForm.addEventListener('submit', async function (e) {
            e.preventDefault();

            clearLoginError();
            clearLoginSuccess();

            var accountInput = document.getElementById('loginAccount');
            var passwordInput = document.getElementById('loginPassword');
            var rememberLogin = document.getElementById('rememberLogin');

            var account = accountInput ? accountInput.value.trim() : '';
            var matKhau = passwordInput ? passwordInput.value : '';

            if (!account || !matKhau) {
                showLoginError('Vui lòng nhập username/email và mật khẩu.');
                return;
            }

            var payload = {
                username: '',
                email: '',
                matKhau: matKhau
            };

            if (account.includes('@')) {
                payload.email = account;
            } else {
                payload.username = account;
            }

            try {
                if (loginSubmitBtn) {
                    loginSubmitBtn.disabled = true;
                    loginSubmitBtn.textContent = 'Đang đăng nhập...';
                }

                var response = await fetch('api/auth-login-proxy.php', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json'
                    },
                    body: JSON.stringify(payload)
                });

                var responseText = await response.text();
                var data = {};

                try {
                    data = responseText ? JSON.parse(responseText) : {};
                } catch (parseError) {
                    throw new Error('Phản hồi API không đúng định dạng JSON.');
                }

                if (!response.ok || !data.success) {
                    throw new Error(data.message || 'Đăng nhập thất bại.');
                }

                var authData = {
                    idTaiKhoan: data.idTaiKhoan,
                    username: data.username,
                    email: data.email,
                    loaiTaiKhoan: data.loaiTaiKhoan,
                    hoTen: data.hoTen,
                    idAdmin: data.idAdmin,
                    idChuQuanLy: data.idChuQuanLy,
                    isLoggedIn: true,
                    loginAt: new Date().toISOString()
                };

                if (rememberLogin && rememberLogin.checked) {
                    localStorage.setItem('admin_auth', JSON.stringify(authData));
                } else {
                    sessionStorage.setItem('admin_auth', JSON.stringify(authData));
                }

                showLoginSuccess(data.message || 'Đăng nhập thành công.');

                setTimeout(function () {
                    var target = data.redirectUrl || 'index1st.php?usecase=dashboard';
                    window.location.href = target;
                }, 700);
            } catch (error) {
                showLoginError(error.message || 'Không thể kết nối tới máy chủ.');
            } finally {
                if (loginSubmitBtn) {
                    loginSubmitBtn.disabled = false;
                    loginSubmitBtn.textContent = 'Đăng nhập';
                }
            }
        });
    }

    if (registerForm) {
        registerForm.addEventListener('submit', async function (e) {
            e.preventDefault();

            clearRegisterError();
            clearRegisterSuccess();

            if (!validateConfirmPassword(true)) {
                return;
            }

            var hoTen = registerFullName ? registerFullName.value.trim() : '';
            var username = registerUsername ? registerUsername.value.trim() : '';
            var email = registerEmail ? registerEmail.value.trim() : '';
            var matKhau = registerPassword ? registerPassword.value : '';

            if (!hoTen || !username || !email || !matKhau) {
                showRegisterError('Vui lòng nhập đầy đủ họ tên, tên đăng nhập, email và mật khẩu.');
                return;
            }

            if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
                showRegisterError('Email không đúng định dạng.');
                return;
            }

            if (matKhau.length < 8) {
                showRegisterError('Mật khẩu phải có ít nhất 8 ký tự.');
                return;
            }

            try {
                if (registerSubmitBtn) {
                    registerSubmitBtn.disabled = true;
                    registerSubmitBtn.textContent = 'Đang đăng ký...';
                }

                var response = await fetch('api/auth-register-proxy.php', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json'
                    },
                    body: JSON.stringify({
                        hoTen: hoTen,
                        username: username,
                        email: email,
                        matKhau: matKhau
                    })
                });

                var responseText = await response.text();
                var data = {};

                try {
                    data = responseText ? JSON.parse(responseText) : {};
                } catch (parseError) {
                    throw new Error('Phản hồi API không đúng định dạng JSON.');
                }

                if (!response.ok || !data.success) {
                    throw new Error(data.message || 'Đăng ký thất bại.');
                }

                showRegisterSuccess(data.message || 'Đăng ký thành công. Bạn có thể đăng nhập ngay bây giờ.');
                registerForm.reset();
                confirmTouched = false;
                validateConfirmPassword(false);
                setTimeout(showLogin, 900);
            } catch (error) {
                showRegisterError(error.message || 'Không thể kết nối tới máy chủ.');
            } finally {
                if (registerSubmitBtn) {
                    registerSubmitBtn.disabled = false;
                    registerSubmitBtn.textContent = 'Đăng ký tài khoản';
                }
            }
        });
    }

    function validateConfirmPassword(forceShow) {
        if (!registerPassword || !registerConfirmPassword || !confirmPasswordWrap || !confirmPasswordError) {
            return true;
        }

        var needShow = forceShow || confirmTouched;
        var hasConfirmValue = registerConfirmPassword.value.length > 0;
        var isMatched = registerPassword.value === registerConfirmPassword.value;
        var isValid = hasConfirmValue && isMatched;

        if (needShow && !isValid) {
            confirmPasswordWrap.classList.add('error');
            confirmPasswordError.classList.add('show');
            clearRegisterSuccess();
            return false;
        }

        confirmPasswordWrap.classList.remove('error');
        confirmPasswordError.classList.remove('show');
        return true;
    }

    if (registerConfirmPassword) {
        registerConfirmPassword.addEventListener('blur', function () {
            confirmTouched = true;
            validateConfirmPassword(true);
        });

        registerConfirmPassword.addEventListener('input', function () {
            if (confirmTouched) {
                validateConfirmPassword(false);
            }
        });
    }

    if (registerPassword) {
        registerPassword.addEventListener('input', function () {
            if (confirmTouched && registerConfirmPassword && registerConfirmPassword.value.length > 0) {
                validateConfirmPassword(false);
            }
        });
    }

    if (passwordToggles.length) {
        passwordToggles.forEach(function (toggleButton) {
            toggleButton.addEventListener('click', function () {
                var targetId = toggleButton.getAttribute('data-toggle-password');
                var targetInput = document.getElementById(targetId);
                var icon = toggleButton.querySelector('i');

                if (!targetInput || !icon) {
                    return;
                }

                var isHidden = targetInput.type === 'password';
                targetInput.type = isHidden ? 'text' : 'password';
                toggleButton.setAttribute('aria-pressed', isHidden ? 'true' : 'false');
                toggleButton.setAttribute('aria-label', isHidden ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');

                icon.classList.remove('fa-eye', 'fa-eye-slash');
                icon.classList.add(isHidden ? 'fa-eye-slash' : 'fa-eye');
            });
        });
    }
})();
