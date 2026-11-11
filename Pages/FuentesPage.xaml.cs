using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LoboEstepario.Pages
{
    public partial class FuentesPage : UserControl, IPage
    {
        private int generation;

        public FuentesPage()
        {
            InitializeComponent();
            ApiClientFactory.WakingChanged += OnWakingChanged;
            Unloaded += (s, e) => ApiClientFactory.WakingChanged -= OnWakingChanged;
            Loaded += (s, e) => { ApiClientFactory.WakingChanged -= OnWakingChanged; ApiClientFactory.WakingChanged += OnWakingChanged; };
        }

        private void OnWakingChanged(bool waking)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (LoadingPanel.Visibility != Visibility.Visible) return;
                LoadingTitle.Text = waking ? ApiClientFactory.WakingMessage : "Comprobando conexiones…";
                LoadingDetail.Text = waking ? "Render puede tardar hasta ~90 s en arrancar." : "Consultando la API.";
            }));
        }

        public Task OnNavigatedToAsync(object parameter)
        {
            return CheckAsync();
        }

        private async Task CheckAsync()
        {
            var mine = ++generation;
            ContentPanel.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;
            OnWakingChanged(ApiClientFactory.IsWaking);

            await Task.WhenAll(CheckOvertureAsync(), CheckMongoAsync());
            if (mine != generation) return;

            LoadingPanel.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Visible;
        }

        private void SetChip(Border chip, TextBlock text, string label, string style)
        {
            text.Text = label;
            chip.Style = (Style)FindResource(style);
        }

        private void AddChip(string text, string style)
        {
            OvertureChips.Children.Add(new Border
            {
                Style = (Style)FindResource(style),
                Margin = new Thickness(0, 0, 8, 6),
                Child = new TextBlock { Text = text }
            });
        }

        private async Task CheckOvertureAsync()
        {
            OvertureChips.Children.Clear();
            DatasetPathText.Visibility = Visibility.Collapsed;
            OvertureErrorText.Visibility = Visibility.Collapsed;
            try
            {
                using (var response = await ApiClientFactory.Client.GetAsync("api/health/overture"))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    var payload = TryParse(body);
                    var status = Get(payload, "status");

                    if (response.IsSuccessStatusCode && string.Equals(status, "reachable", StringComparison.OrdinalIgnoreCase))
                    {
                        SetChip(OvertureChip, OvertureChipText, "alcanzable", "ChipGreen");
                        var release = Get(payload, "releaseVersion");
                        var theme = Get(payload, "theme");
                        var type = Get(payload, "type");
                        if (!string.IsNullOrWhiteSpace(release)) AddChip("release " + release, "ChipViolet");
                        if (!string.IsNullOrWhiteSpace(theme)) AddChip("theme " + theme, "Chip");
                        if (!string.IsNullOrWhiteSpace(type)) AddChip("tipo " + type, "Chip");
                        var path = Get(payload, "datasetPath");
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            DatasetPathText.Text = path;
                            DatasetPathText.Visibility = Visibility.Visible;
                        }
                        return;
                    }

                    if (response.IsSuccessStatusCode && string.Equals(status, "empty", StringComparison.OrdinalIgnoreCase))
                    {
                        SetChip(OvertureChip, OvertureChipText, "vacío", "ChipGold");
                        OvertureErrorText.Text = "El dataset se alcanzó pero no devolvió lugares.";
                        OvertureErrorText.Visibility = Visibility.Visible;
                        return;
                    }

                    SetChip(OvertureChip, OvertureChipText, string.IsNullOrWhiteSpace(status) ? "error" : status, "ChipRed");
                    var detail = Get(payload, "detail") ?? Get(payload, "datasetPath") ?? body;
                    OvertureErrorText.Text = "No se pudo alcanzar el dataset: " + AiService.Truncate(detail);
                    OvertureErrorText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                SetChip(OvertureChip, OvertureChipText, "sin conexión", "ChipRed");
                OvertureErrorText.Text = "No fue posible consultar la API: " + ex.Message;
                OvertureErrorText.Visibility = Visibility.Visible;
            }
        }

        private async Task CheckMongoAsync()
        {
            // La web solo muestra el chip fijo "persistencia"; en escritorio se conserva la comprobación real vía api/business/stats.
            try
            {
                var stats = Json.Deserialize<BusinessStatsDto>(await ApiClientFactory.Client.GetStringAsync("api/business/stats"));
                SetChip(MongoChip, MongoChipText, "persistencia", "ChipTeal");
                MongoStatsText.Foreground = (Brush)FindResource("FaintBrush");
                MongoStatsText.Text = stats == null
                    ? "Conectado, pero la base no devolvió estadísticas."
                    : string.Format(CultureInfo.CurrentCulture, "Conectado · {0:N0} negocios guardados · {1:N0} con datos de contacto.", stats.TotalCount, stats.WithContactCount);
            }
            catch (Exception ex)
            {
                SetChip(MongoChip, MongoChipText, "sin conexión", "ChipRed");
                MongoStatsText.Foreground = (Brush)FindResource("EmberBrush");
                MongoStatsText.Text = "No fue posible leer BusinessList: " + ex.Message;
            }
        }

        private static IDictionary<string, object> TryParse(string body)
        {
            try { return Json.DeserializeObject(body) as IDictionary<string, object>; }
            catch (Exception) { return null; }
        }

        private static string Get(IDictionary<string, object> payload, string key)
        {
            if (payload == null) return null;
            foreach (var pair in payload)
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) && pair.Value != null)
                    return Convert.ToString(pair.Value, CultureInfo.InvariantCulture);
            return null;
        }

        private async void Recheck_Click(object sender, RoutedEventArgs e) { await CheckAsync(); }
        private void Go_Inicio(object sender, RoutedEventArgs e) { MainWindow.Current.Navigate(Routes.Inicio); }
    }
}
