using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LoboEstepario.Pages
{
    public partial class EjecucionesPage : UserControl, IPage
    {
        private readonly StatTile ultimoScrape, negocios, ultimoEnvio, volumen;
        private int generation;

        public EjecucionesPage()
        {
            InitializeComponent();
            ultimoScrape = Add("Último scrape (prospección)", "GoldBrush", null);
            negocios = Add("Negocios resultado", "VioletBrush", " guardados");
            ultimoEnvio = Add("Último envío del CRM", "EmberBrush", null);
            volumen = Add("Volumen de envíos", "TealBrush", " enviados");
            ultimoScrape.Value.FontSize = 22; ultimoEnvio.Value.FontSize = 22;
            ultimoScrape.Foot.Text = "se rescraped para actualizar cada lugar";
            ultimoEnvio.Foot.Text = "historial en el módulo CRM";
            ApiClientFactory.WakingChanged += OnWakingChanged;
            Unloaded += (s, e) => ApiClientFactory.WakingChanged -= OnWakingChanged;
            Loaded += (s, e) => { ApiClientFactory.WakingChanged -= OnWakingChanged; ApiClientFactory.WakingChanged += OnWakingChanged; };
        }

        private StatTile Add(string label, string accent, string unit)
        {
            var tile = StatTile.Create(this, label, accent, unit);
            
            StatGrid.Children.Add(tile.Root);
            return tile;
        }

        private void OnWakingChanged(bool waking)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (LoadingPanel.Visibility != Visibility.Visible) return;
                LoadingTitle.Text = waking ? ApiClientFactory.WakingMessage : "Cargando actividad…";
                LoadingDetail.Text = waking ? "Render puede tardar hasta ~90 s en arrancar." : "Consultando las estadísticas guardadas.";
            }));
        }

        public Task OnNavigatedToAsync(object parameter)
        {
            return LoadAsync();
        }

        private async Task LoadAsync()
        {
            var mine = ++generation;
            LoadingPanel.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Collapsed;
            OnWakingChanged(ApiClientFactory.IsWaking);
            try
            {
                var bTask = ApiClientFactory.Client.GetStringAsync("api/business/stats");
                var eTask = ApiClientFactory.Client.GetStringAsync("api/crm/stats");
                await Task.WhenAll(bTask, eTask);
                if (mine != generation) return;
                var business = Json.Deserialize<BusinessStatsDto>(bTask.Result) ?? new BusinessStatsDto();
                var email = Json.Deserialize<EmailStatsDto>(eTask.Result) ?? new EmailStatsDto();

                ultimoScrape.SetValue(FormatDate(business.LastScrapedAtUtc));
                negocios.SetValue(business.TotalCount.ToString("N0", CultureInfo.CurrentCulture));
                negocios.SetFoot(null, business.WithContactCount.ToString("N0", CultureInfo.CurrentCulture), " con datos de contacto");
                ultimoEnvio.SetValue(FormatDate(email.LastSentAtUtc));
                volumen.SetValue(email.SentCount.ToString("N0", CultureInfo.CurrentCulture));
                volumen.SetFoot(null, email.FailedCount.ToString("N0", CultureInfo.CurrentCulture), " fallidos");

                LoadingPanel.Visibility = Visibility.Collapsed;
                ContentPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                if (mine != generation) return;
                ErrorDetail.Text = "No fue posible consultar la API: " + ex.Message;
                LoadingPanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
            }
        }

        private static string FormatDate(string utc)
        {
            DateTime parsed;
            if (!DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed)) return "nunca";
            return parsed.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        }

        private async void Retry_Click(object sender, RoutedEventArgs e) { await LoadAsync(); }
        private void Go_Negocios(object sender, RoutedEventArgs e) { MainWindow.Current.Navigate(Routes.Negocios); }
        private void Go_Crm(object sender, RoutedEventArgs e) { MainWindow.Current.Navigate(Routes.Crm); }
        private void Go_Social(object sender, RoutedEventArgs e) { MainWindow.Current.Navigate(Routes.Social); }
    }
}
