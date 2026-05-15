using MauiApp1.Models;
using MauiApp1.Services;
using System.Collections.ObjectModel;

namespace MauiApp1.Views
{
    public class MonAnPage : ContentPage
    {
        private readonly MonAnService _monAnService;
        private readonly ObservableCollection<MonAn> _items = new();

        private readonly Entry _entryChiNhanh;
        private readonly CollectionView _collectionView;

        public MonAnPage(MonAnService monAnService)
        {
            _monAnService = monAnService;

            Title = "MÃ³n Äƒn theo gian hÃ ng";

            _entryChiNhanh = new Entry
            {
                Placeholder = "Nháº­p ID gian hÃ ng"
            };

            var btnLoad = new Button
            {
                Text = "Táº£i mÃ³n Äƒn"
            };
            btnLoad.Clicked += OnLoadClicked;

            _collectionView = new CollectionView
            {
                ItemsSource = _items,
                ItemTemplate = new DataTemplate(() =>
                {
                    var ten = new Label { FontAttributes = FontAttributes.Bold };
                    ten.SetBinding(Label.TextProperty, "TenMon");

                    var info = new Label();
                    info.SetBinding(Label.TextProperty, "ThongTinMon");

                    var gia = new Label();
                    gia.SetBinding(Label.TextProperty, new Binding("DonGia", stringFormat: "GiÃ¡: {0:N0}"));

                    return new VerticalStackLayout
                    {
                        Padding = 12,
                        Spacing = 4,
                        Children =
                        {
                            ten,
                            info,
                            gia
                        }
                    };
                })
            };

            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    _entryChiNhanh,
                    btnLoad,
                    _collectionView
                }
            };
        }

        private async void OnLoadClicked(object? sender, EventArgs e)
        {
            _items.Clear();

            if (!int.TryParse(_entryChiNhanh.Text, out int idChiNhanh))
            {
                await DisplayAlertAsync("Lá»—i", "ID gian hÃ ng khÃ´ng há»£p lá»‡", "OK");
                return;
            }

            var data = await _monAnService.GetByChiNhanhAsync(idChiNhanh);

            foreach (var item in data)
                _items.Add(item);
        }
    }
}

