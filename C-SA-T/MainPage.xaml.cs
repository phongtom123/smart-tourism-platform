namespace MauiApp1
{
    public partial class MainPage : ContentPage
    {
        private bool _daKiemTra = false;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_daKiemTra)
                return;

            _daKiemTra = true;

            try
            {
                lblStatus.Text = "Màn hình kiểm tra DB cũ đã bị vô hiệu hóa.";
                await DisplayAlertAsync("Thông báo", "App đã chuyển sang dùng backend API thay vì kết nối DB trực tiếp.", "OK");
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Có lỗi khi khởi tạo màn hình";
                await DisplayAlertAsync("Lỗi", ex.ToString(), "OK");
            }
        }
    }
}
