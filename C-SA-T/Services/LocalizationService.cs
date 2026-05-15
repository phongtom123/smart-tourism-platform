using System.Globalization;

namespace MauiApp1.Services;

public class LocalizationService
{
    public const string LanguagePreferenceKey = "ui_language";
    public const string LanguageModePreferenceKey = "ui_language_mode";
    public const string SystemLanguageMode = "system";
    public const string ManualLanguageMode = "manual";

    private string _currentLanguage = "vi";

    public string CurrentLanguage => _currentLanguage;

    public event Action? LanguageChanged;

    public void SetLanguage(string code)
    {
        var c = NormalizeLanguageCode(code);
        if (_currentLanguage == c) return;
        _currentLanguage = c;
        ApplyCulture(c);
        LanguageChanged?.Invoke();
    }

    public LocalizationService()
    {
        ApplyCulture(_currentLanguage);
    }

    public static string ResolveStartupLanguage()
    {
        if (!Preferences.ContainsKey(LanguageModePreferenceKey) &&
            Preferences.ContainsKey(LanguagePreferenceKey))
        {
            var legacyLanguage = NormalizeLanguageCode(Preferences.Get(LanguagePreferenceKey, "vi"));
            Preferences.Set(LanguageModePreferenceKey, ManualLanguageMode);
            Preferences.Set(LanguagePreferenceKey, legacyLanguage);
            return legacyLanguage;
        }

        var mode = Preferences.Get(LanguageModePreferenceKey, SystemLanguageMode);
        if (mode.Equals(ManualLanguageMode, StringComparison.OrdinalIgnoreCase) &&
            Preferences.ContainsKey(LanguagePreferenceKey))
        {
            return NormalizeLanguageCode(Preferences.Get(LanguagePreferenceKey, "vi"));
        }

        var detected = DetectSystemLanguage();
        Preferences.Set(LanguageModePreferenceKey, SystemLanguageMode);
        Preferences.Set(LanguagePreferenceKey, detected);
        return detected;
    }

    public static string DetectSystemLanguage()
    {
        var candidates = new[]
        {
            CultureInfo.CurrentUICulture,
            CultureInfo.CurrentCulture,
            CultureInfo.InstalledUICulture
        };

        foreach (var culture in candidates)
        {
            var language = NormalizeLanguageCode(culture.TwoLetterISOLanguageName);
            if (IsSupportedLanguage(language))
                return language;
        }

        return "vi";
    }

    public static string NormalizeLanguageCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "vi";

        var c = code.Trim().ToLowerInvariant();
        return c switch
        {
            var value when value.StartsWith("vi") => "vi",
            var value when value.StartsWith("en") => "en",
            var value when value.StartsWith("ko") => "ko",
            var value when value.StartsWith("ja") => "ja",
            _ => "vi"
        };
    }

    private static bool IsSupportedLanguage(string code)
    {
        return code is "vi" or "en" or "ko" or "ja";
    }

    public string Get(string key)
    {
        if (_strings.TryGetValue(_currentLanguage, out var d) && d.TryGetValue(key, out var v)) return v;
        if (_strings.TryGetValue("vi", out var vi) && vi.TryGetValue(key, out var f)) return f;
        return key;
    }

    private static void ApplyCulture(string languageCode)
    {
        var cultureName = languageCode switch
        {
            "en" => "en-US",
            "ko" => "ko-KR",
            "ja" => "ja-JP",
            _ => "vi-VN"
        };

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> _strings = new()
    {
        ["vi"] = new()
        {
            ["tab_home"] = "Trang chủ",
            ["tab_explore"] = "Khám phá",
            ["tab_tour"] = "Tour",
            ["tour_refresh"] = "Làm mới",
            ["tour_page_title"] = "Tour",
            ["tour_page_subtitle"] = "Chọn hành trình và bắt đầu dẫn đường.",
            ["tour_empty"] = "Chưa có tour đang hoạt động.",
            ["tour_list_load_error"] = "Không tải được danh sách tour. {0}",
            ["tour_default_description"] = "Hành trình tham quan các gian hàng theo thứ tự.",
            ["tour_start_button"] = "Bắt đầu tour",
            ["tour_start_confirm_title"] = "Bắt đầu tour?",
            ["tour_start_confirm_message"] = "Bạn muốn thực hiện tour \"{0}\"?",
            ["tour_start_confirm_accept"] = "Bắt đầu",
            ["tour_start_confirm_cancel"] = "Hủy",
            ["tour_detail_load_error"] = "Không tải được chi tiết tour.",
            ["tour_no_available_stops"] = "Tour này chưa có điểm dừng đang hoạt động có tọa độ.",
            ["tour_meta_stops"] = "{0} điểm dừng",
            ["tour_meta_minutes"] = "{0} phút",
            ["tour_progress_running"] = "Đang đi tour",
            ["tour_progress_status"] = "Đang đi tour  {0}/{1}",
            ["tour_current_stop_fallback"] = "Điểm hiện tại",
            ["tour_next_stop_fallback"] = "Điểm tiếp theo",
            ["tour_completed"] = "Đã đến điểm cuối của tour",
            ["tour_start_fallback"] = "Bắt đầu",
            ["tour_stop_confirm_title"] = "Dừng tour?",
            ["tour_stop_confirm_message"] = "Bạn muốn dừng dẫn đường tour hiện tại?",
            ["tour_stop_confirm_accept"] = "Dừng tour",
            ["tour_stop_confirm_cancel"] = "Tiếp tục",
            ["map_title"] = "Khám phá",
            ["map_subtitle_default"] = "Ẩm thực, đồ uống và các địa điểm gần bạn",
            ["map_subtitle_half"] = "Chạm vào quán để xem chi tiết",
            ["map_subtitle_full"] = "Danh sách đầy đủ các địa điểm gợi ý",
            ["search_placeholder"] = "Tìm kiếm gian hàng",
            ["detail_close"] = "Đóng",
            ["detail_language"] = "Ngôn ngữ",
            ["detail_audio_title"] = "Thuyết minh audio",
            ["detail_food_images"] = "Một số hình ảnh món ăn",
            ["btn_play"] = "▶ Phát nè",
            ["btn_pause"] = "⏸ Tạm dừng",
            ["btn_pending"] = "■ Chờ phát",
            ["fallback_name"] = "Tên gian hàng",
            ["fallback_address"] = "Chưa có địa chỉ",
            ["fallback_description"] = "Chưa có mô tả.",
            ["fallback_audio_none"] = "Audio: chưa có",
            ["fallback_audio_ready"] = "Audio thuyết minh đã sẵn sàng",
            ["audio_playing"] = "Đang phát",
            ["audio_paused"] = "Đã tạm dừng",
            ["audio_pending"] = "Đang tải...",
            ["audio_banner_title"] = "Âm thanh gian hàng",
            ["my_location"] = "Vị trí của tôi",
            ["map_3d_requires_internet"] = "Ban do 3D can co internet.",
            ["map_3d_android_only"] = "Ban do 3D hien chi ho tro tren Android Google Maps.",
            ["map_switch_to_3d"] = "Chuyen sang ban do 3D",
            ["map_switch_to_2d"] = "Chuyen sang ban do 2D",
            ["map_refresh"] = "Lam moi ban do",
            ["login_title"] = "Đăng nhập",
            ["login_username_hint"] = "Tên đăng nhập hoặc email",
            ["login_password_hint"] = "Mật khẩu",
            ["login_btn"] = "Đăng nhập",
            ["register_btn"] = "Đăng ký",
            ["login_empty_fields"] = "Vui lòng nhập đầy đủ thông tin",
            ["login_loading"] = "Đang đăng nhập...",
            ["login_success"] = "Đăng nhập thành công",
            ["login_wrong_creds"] = "Sai tài khoản hoặc mật khẩu",
            ["login_api_error"] = "Lỗi kết nối API",
            ["register_todo"] = "Chưa làm chức năng đăng ký",
            ["alert_notice"] = "Thông báo",
            ["alert_error"] = "Lỗi",
            ["alert_ok"] = "OK",
            ["alert_no_audio"] = "Gian hàng này chưa có audio.",
            ["alert_audio_error"] = "Lỗi audio",
            ["hero_badge"] = "Bắt đầu khám phá",
            ["hero_street"] = "Phố Ẩm Thực",
            ["hero_follow"] = "Đang theo dõi {0} điểm",
            ["home_header_title"] = "Khu phố Vĩnh Khánh",
            ["home_header_location"] = "Quận 4, TP Hồ Chí Minh",
            ["home_hero_accent"] = "Vĩnh Khánh",
            ["home_nearby_prefix"] = "Gần bạn",
            ["home_restaurant_fallback"] = "Quán ăn",
            ["section_nearby"] = "Điểm nổi bật gần bạn",
            ["section_see_all"] = "Xem tất cả",
            ["no_data"] = "Chưa có dữ liệu địa điểm",
            ["search_title"] = "Tìm kiếm",
            ["search_results"] = "{0} kết quả cho \"{1}\"",
            ["search_results_empty"] = "Không thấy kết quả cho \"{0}\"",
            ["poi_load_error"] = "Không tải được dữ liệu POI từ DB.\n{0}",
            ["explore_view_detail"] = "Xem chi tiết",
            ["search_match_name"] = "Khớp tên quán",
            ["search_match_address"] = "Khớp địa chỉ",
            ["search_match_food"] = "Món hợp: {0}",
            ["search_no_results"] = "Không tìm thấy quán hoặc món phù hợp",
            ["search_try_other"] = "Thử từ khóa khác cho \"{0}\" hoặc tìm theo tên món, tên quán, địa chỉ.",
            ["alert_poi_not_found"] = "Không tìm thấy gian hàng tương ứng.",
            ["tab_settings"] = "Cài đặt",
            ["settings_title"] = "Cài đặt",
            ["settings_language_title"] = "Ngôn ngữ giao diện",
            ["settings_language_desc"] = "Chọn ngôn ngữ hiển thị cho toàn bộ ứng dụng",
        },
        ["en"] = new()
        {
            ["tab_home"] = "Home",
            ["tab_explore"] = "Explore",
            ["tab_tour"] = "Tour",
            ["tour_refresh"] = "Refresh",
            ["tour_page_title"] = "Tour",
            ["tour_page_subtitle"] = "Choose a route and start navigation.",
            ["tour_empty"] = "No active tours yet.",
            ["tour_list_load_error"] = "Could not load tours. {0}",
            ["tour_default_description"] = "Follow the stalls in the selected order.",
            ["tour_start_button"] = "Start tour",
            ["tour_start_confirm_title"] = "Start tour?",
            ["tour_start_confirm_message"] = "Do you want to start \"{0}\"?",
            ["tour_start_confirm_accept"] = "Start",
            ["tour_start_confirm_cancel"] = "Cancel",
            ["tour_detail_load_error"] = "Could not load tour details.",
            ["tour_no_available_stops"] = "This tour has no active stops with coordinates.",
            ["tour_meta_stops"] = "{0} stops",
            ["tour_meta_minutes"] = "{0} min",
            ["tour_progress_running"] = "Tour in progress",
            ["tour_progress_status"] = "Tour in progress  {0}/{1}",
            ["tour_current_stop_fallback"] = "Current stop",
            ["tour_next_stop_fallback"] = "Next stop",
            ["tour_completed"] = "You have reached the final stop",
            ["tour_start_fallback"] = "Start",
            ["tour_stop_confirm_title"] = "Stop tour?",
            ["tour_stop_confirm_message"] = "Do you want to stop the current tour navigation?",
            ["tour_stop_confirm_accept"] = "Stop tour",
            ["tour_stop_confirm_cancel"] = "Continue",
            ["map_title"] = "Explore",
            ["map_subtitle_default"] = "Food, drinks and places near you",
            ["map_subtitle_half"] = "Tap a stall to see details",
            ["map_subtitle_full"] = "Full list of suggested places",
            ["search_placeholder"] = "Search stalls",
            ["detail_close"] = "Close",
            ["detail_language"] = "Language",
            ["detail_audio_title"] = "Audio guide",
            ["detail_food_images"] = "Food photos",
            ["btn_play"] = "▶ Play",
            ["btn_pause"] = "⏸ Pause",
            ["btn_pending"] = "■ Loading",
            ["fallback_name"] = "Stall name",
            ["fallback_address"] = "No address",
            ["fallback_description"] = "No description.",
            ["fallback_audio_none"] = "Audio: unavailable",
            ["fallback_audio_ready"] = "Audio guide ready",
            ["audio_playing"] = "Now playing",
            ["audio_paused"] = "Paused",
            ["audio_pending"] = "Loading...",
            ["audio_banner_title"] = "Stall audio",
            ["my_location"] = "My location",
            ["map_3d_requires_internet"] = "3D map requires an internet connection.",
            ["map_3d_android_only"] = "3D map is currently available only on Android Google Maps.",
            ["map_switch_to_3d"] = "Switch to 3D map",
            ["map_switch_to_2d"] = "Switch to 2D map",
            ["map_refresh"] = "Refresh map",
            ["login_title"] = "Sign in",
            ["login_username_hint"] = "Username or email",
            ["login_password_hint"] = "Password",
            ["login_btn"] = "Sign in",
            ["register_btn"] = "Register",
            ["login_empty_fields"] = "Please fill in all fields",
            ["login_loading"] = "Signing in...",
            ["login_success"] = "Signed in successfully",
            ["login_wrong_creds"] = "Incorrect username or password",
            ["login_api_error"] = "API connection error",
            ["register_todo"] = "Registration not yet available",
            ["alert_notice"] = "Notice",
            ["alert_error"] = "Error",
            ["alert_ok"] = "OK",
            ["alert_no_audio"] = "This stall has no audio yet.",
            ["alert_audio_error"] = "Audio error",
            ["hero_badge"] = "Start exploring",
            ["hero_street"] = "Food Street",
            ["hero_follow"] = "Tracking {0} spots",
            ["home_header_title"] = "Vinh Khanh Quarter",
            ["home_header_location"] = "District 4, Ho Chi Minh City",
            ["home_hero_accent"] = "Vinh Khanh",
            ["home_nearby_prefix"] = "Near you",
            ["home_restaurant_fallback"] = "Food stall",
            ["section_nearby"] = "Highlights near you",
            ["section_see_all"] = "See all",
            ["no_data"] = "No location data yet",
            ["search_title"] = "Search",
            ["search_results"] = "{0} results for \"{1}\"",
            ["search_results_empty"] = "No results for \"{0}\"",
            ["poi_load_error"] = "Could not load POI data from the database.\n{0}",
            ["explore_view_detail"] = "View detail",
            ["search_match_name"] = "Name match",
            ["search_match_address"] = "Address match",
            ["search_match_food"] = "Dishes: {0}",
            ["search_no_results"] = "No stalls or dishes found",
            ["search_try_other"] = "Try another keyword for \"{0}\" or search by dish, name, or address.",
            ["alert_poi_not_found"] = "Stall not found.",
            ["tab_settings"] = "Settings",
            ["settings_title"] = "Settings",
            ["settings_language_title"] = "Interface language",
            ["settings_language_desc"] = "Choose the display language for the entire app",
        },
        ["ko"] = new()
        {
            ["tab_home"] = "홈",
            ["tab_explore"] = "탐색",
            ["tab_tour"] = "투어",
            ["tour_refresh"] = "새로고침",
            ["tour_page_title"] = "투어",
            ["tour_page_subtitle"] = "경로를 선택하고 안내를 시작하세요.",
            ["tour_empty"] = "진행 중인 투어가 없습니다.",
            ["tour_list_load_error"] = "투어 목록을 불러올 수 없습니다. {0}",
            ["tour_default_description"] = "선택한 순서대로 가게를 방문하는 경로입니다.",
            ["tour_start_button"] = "투어 시작",
            ["tour_start_confirm_title"] = "투어를 시작할까요?",
            ["tour_start_confirm_message"] = "\"{0}\" 투어를 시작하시겠습니까?",
            ["tour_start_confirm_accept"] = "시작",
            ["tour_start_confirm_cancel"] = "취소",
            ["tour_detail_load_error"] = "투어 상세 정보를 불러올 수 없습니다.",
            ["tour_no_available_stops"] = "이 투어에는 좌표가 있는 운영 중인 정류장이 없습니다.",
            ["tour_meta_stops"] = "{0}개 정류장",
            ["tour_meta_minutes"] = "{0}분",
            ["tour_progress_running"] = "투어 진행 중",
            ["tour_progress_status"] = "투어 진행 중  {0}/{1}",
            ["tour_current_stop_fallback"] = "현재 정류장",
            ["tour_next_stop_fallback"] = "다음 정류장",
            ["tour_completed"] = "마지막 정류장에 도착했습니다",
            ["tour_start_fallback"] = "시작",
            ["tour_stop_confirm_title"] = "투어를 중지할까요?",
            ["tour_stop_confirm_message"] = "현재 투어 안내를 중지하시겠습니까?",
            ["tour_stop_confirm_accept"] = "투어 중지",
            ["tour_stop_confirm_cancel"] = "계속",
            ["map_title"] = "탐색",
            ["map_subtitle_default"] = "주변 음식점 및 명소",
            ["map_subtitle_half"] = "가게를 탭하여 세부 정보 보기",
            ["map_subtitle_full"] = "추천 장소 전체 목록",
            ["search_placeholder"] = "가게 검색",
            ["detail_close"] = "닫기",
            ["detail_language"] = "언어",
            ["detail_audio_title"] = "오디오 가이드",
            ["detail_food_images"] = "음식 사진",
            ["btn_play"] = "▶ 재생",
            ["btn_pause"] = "⏸ 일시정지",
            ["btn_pending"] = "■ 로딩 중",
            ["fallback_name"] = "가게 이름",
            ["fallback_address"] = "주소 없음",
            ["fallback_description"] = "설명 없음.",
            ["fallback_audio_none"] = "오디오: 없음",
            ["fallback_audio_ready"] = "오디오 가이드 준비됨",
            ["audio_playing"] = "재생 중",
            ["audio_paused"] = "일시정지",
            ["audio_pending"] = "로딩 중...",
            ["audio_banner_title"] = "가게 오디오",
            ["my_location"] = "내 위치",
            ["map_3d_requires_internet"] = "3D map requires an internet connection.",
            ["map_3d_android_only"] = "3D map is currently available only on Android Google Maps.",
            ["map_switch_to_3d"] = "Switch to 3D map",
            ["map_switch_to_2d"] = "Switch to 2D map",
            ["map_refresh"] = "Refresh map",
            ["login_title"] = "로그인",
            ["login_username_hint"] = "사용자명 또는 이메일",
            ["login_password_hint"] = "비밀번호",
            ["login_btn"] = "로그인",
            ["register_btn"] = "회원가입",
            ["login_empty_fields"] = "모든 항목을 입력하세요",
            ["login_loading"] = "로그인 중...",
            ["login_success"] = "로그인 성공",
            ["login_wrong_creds"] = "아이디 또는 비밀번호가 잘못되었습니다",
            ["login_api_error"] = "API 연결 오류",
            ["register_todo"] = "회원가입 기능 준비 중",
            ["alert_notice"] = "알림",
            ["alert_error"] = "오류",
            ["alert_ok"] = "확인",
            ["alert_no_audio"] = "이 가게에는 오디오가 없습니다.",
            ["alert_audio_error"] = "오디오 오류",
            ["hero_badge"] = "탐색 시작",
            ["hero_street"] = "푸드 스트리트",
            ["hero_follow"] = "{0}곳 추적 중",
            ["home_header_title"] = "빈칸 거리",
            ["home_header_location"] = "호치민시 4군",
            ["home_hero_accent"] = "빈칸",
            ["home_nearby_prefix"] = "내 주변",
            ["home_restaurant_fallback"] = "음식점",
            ["section_nearby"] = "주변 인기 명소",
            ["section_see_all"] = "전체 보기",
            ["no_data"] = "위치 데이터 없음",
            ["search_title"] = "검색",
            ["search_results"] = "\"{1}\"에 대한 결과 {0}개",
            ["search_results_empty"] = "\"{0}\"에 대한 결과가 없습니다",
            ["poi_load_error"] = "DB에서 POI 데이터를 불러오지 못했습니다.\n{0}",
            ["explore_view_detail"] = "자세히 보기",
            ["search_match_name"] = "이름 일치",
            ["search_match_address"] = "주소 일치",
            ["search_match_food"] = "메뉴: {0}",
            ["search_no_results"] = "가게 또는 메뉴를 찾을 수 없음",
            ["search_try_other"] = "\"{0}\"에 대한 다른 키워드를 시도하거나 메뉴, 이름, 주소로 검색하세요.",
            ["alert_poi_not_found"] = "해당 가게를 찾을 수 없습니다.",
            ["tab_settings"] = "설정",
            ["settings_title"] = "설정",
            ["settings_language_title"] = "인터페이스 언어",
            ["settings_language_desc"] = "앱 전체에 표시할 언어를 선택하세요",
        },
        ["ja"] = new()
        {
            ["tab_home"] = "ホーム",
            ["tab_explore"] = "探索",
            ["tab_tour"] = "ツアー",
            ["tour_refresh"] = "更新",
            ["tour_page_title"] = "ツアー",
            ["tour_page_subtitle"] = "ルートを選んで案内を開始します。",
            ["tour_empty"] = "利用可能なツアーはまだありません。",
            ["tour_list_load_error"] = "ツアー一覧を読み込めませんでした。{0}",
            ["tour_default_description"] = "選択した順番で店舗を巡るルートです。",
            ["tour_start_button"] = "ツアー開始",
            ["tour_start_confirm_title"] = "ツアーを開始しますか？",
            ["tour_start_confirm_message"] = "「{0}」を開始しますか？",
            ["tour_start_confirm_accept"] = "開始",
            ["tour_start_confirm_cancel"] = "キャンセル",
            ["tour_detail_load_error"] = "ツアー詳細を読み込めませんでした。",
            ["tour_no_available_stops"] = "このツアーには座標のある営業中の停留所がありません。",
            ["tour_meta_stops"] = "{0}か所",
            ["tour_meta_minutes"] = "{0}分",
            ["tour_progress_running"] = "ツアー進行中",
            ["tour_progress_status"] = "ツアー進行中  {0}/{1}",
            ["tour_current_stop_fallback"] = "現在の停留所",
            ["tour_next_stop_fallback"] = "次の停留所",
            ["tour_completed"] = "最後の停留所に到着しました",
            ["tour_start_fallback"] = "開始",
            ["tour_stop_confirm_title"] = "ツアーを停止しますか？",
            ["tour_stop_confirm_message"] = "現在のツアー案内を停止しますか？",
            ["tour_stop_confirm_accept"] = "ツアー停止",
            ["tour_stop_confirm_cancel"] = "続ける",
            ["map_title"] = "探索",
            ["map_subtitle_default"] = "近くのグルメ・スポット",
            ["map_subtitle_half"] = "店をタップして詳細を見る",
            ["map_subtitle_full"] = "おすすめスポット一覧",
            ["search_placeholder"] = "店を検索",
            ["detail_close"] = "閉じる",
            ["detail_language"] = "言語",
            ["detail_audio_title"] = "音声ガイド",
            ["detail_food_images"] = "料理の写真",
            ["btn_play"] = "▶ 再生",
            ["btn_pause"] = "⏸ 一時停止",
            ["btn_pending"] = "■ 読込中",
            ["fallback_name"] = "店舗名",
            ["fallback_address"] = "住所なし",
            ["fallback_description"] = "説明なし。",
            ["fallback_audio_none"] = "音声: なし",
            ["fallback_audio_ready"] = "音声ガイド準備完了",
            ["audio_playing"] = "再生中",
            ["audio_paused"] = "一時停止",
            ["audio_pending"] = "読込中...",
            ["audio_banner_title"] = "店舗音声",
            ["my_location"] = "現在地",
            ["map_3d_requires_internet"] = "3D map requires an internet connection.",
            ["map_3d_android_only"] = "3D map is currently available only on Android Google Maps.",
            ["map_switch_to_3d"] = "Switch to 3D map",
            ["map_switch_to_2d"] = "Switch to 2D map",
            ["map_refresh"] = "Refresh map",
            ["login_title"] = "ログイン",
            ["login_username_hint"] = "ユーザー名またはメール",
            ["login_password_hint"] = "パスワード",
            ["login_btn"] = "ログイン",
            ["register_btn"] = "登録",
            ["login_empty_fields"] = "すべての項目を入力してください",
            ["login_loading"] = "ログイン中...",
            ["login_success"] = "ログイン成功",
            ["login_wrong_creds"] = "ユーザー名またはパスワードが違います",
            ["login_api_error"] = "API接続エラー",
            ["register_todo"] = "登録機能は未実装です",
            ["alert_notice"] = "お知らせ",
            ["alert_error"] = "エラー",
            ["alert_ok"] = "OK",
            ["alert_no_audio"] = "この店舗の音声はまだありません。",
            ["alert_audio_error"] = "音声エラー",
            ["hero_badge"] = "探索を始める",
            ["hero_street"] = "グルメストリート",
            ["hero_follow"] = "{0}スポットを追跡中",
            ["home_header_title"] = "ヴィンカイン通り",
            ["home_header_location"] = "ホーチミン市4区",
            ["home_hero_accent"] = "ヴィンカイン",
            ["home_nearby_prefix"] = "近く",
            ["home_restaurant_fallback"] = "飲食店",
            ["section_nearby"] = "近くのおすすめスポット",
            ["section_see_all"] = "すべて見る",
            ["no_data"] = "位置データなし",
            ["search_title"] = "検索",
            ["search_results"] = "\"{1}\" の検索結果 {0}件",
            ["search_results_empty"] = "\"{0}\" の結果が見つかりません",
            ["poi_load_error"] = "DBからPOIデータを読み込めませんでした。\n{0}",
            ["explore_view_detail"] = "詳細を見る",
            ["search_match_name"] = "名前一致",
            ["search_match_address"] = "住所一致",
            ["search_match_food"] = "料理: {0}",
            ["search_no_results"] = "店舗またはメニューが見つかりません",
            ["search_try_other"] = "\"{0}\" の別のキーワードを試すか、料理名・店名・住所で検索してください。",
            ["alert_poi_not_found"] = "対応する店舗が見つかりません。",
            ["tab_settings"] = "設定",
            ["settings_title"] = "設定",
            ["settings_language_title"] = "インターフェース言語",
            ["settings_language_desc"] = "アプリ全体の表示言語を選択してください",
        },
    };
}
