using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LoboEstepario.Pages
{
    public partial class HomePage : UserControl, IPage
    {
        private readonly StatTile negocios, contacto, fuentes, enviados, fallidos, empresasCorreo;
        private int generation;

        public HomePage()
        {
            InitializeComponent();
            negocios = AddTile("Negocios guardados", "GoldBrush");
            contacto = AddTile("Con datos de contacto", "TealBrush");
            fuentes = AddTile("Fuentes activas", "VioletBrush");
            enviados = AddTile("Correos enviados", "EmberBrush");
            fallidos = AddTile("Correos fallidos", null);
            empresasCorreo = AddTile("Empresas con correo", "TealBrush");
            fuentes.Foot.Text = "geografías configuradas para prospectar";
            contacto.Foot.Text = "teléfonos, correos o sitios web registrados";
            fallidos.Foot.Text = "registrados en el historial del CRM";
            empresasCorreo.Foot.Text = "base potencial del módulo de correo";
        }

        private StatTile AddTile(string label, string accent)
        {
            var tile = StatTile.Create(this, label, accent, null);
            
            StatGrid.Children.Add(tile.Root);
            return tile;
        }

        public Task OnNavigatedToAsync(object parameter)
        {
            return LoadAsync();
        }

        private void ShowLoading(string title, string detail)
        {
            LoadingTitle.Text = title;
            LoadingDetail.Text = detail;
            LoadingPanel.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowError(string detail)
        {
            ErrorDetail.Text = detail;
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        private async Task LoadAsync()
        {
            var mine = ++generation;
            ShowLoading("Cargando indicadores…", "Consultando los servicios de la API.");

            // Igual que la web: reintenta hasta ~90 s mientras Render despierta.
            var deadline = DateTime.UtcNow.AddSeconds(90);
            bool ok;
            while (true)
            {
                ok = await ApiClientFactory.PingAsync(TimeSpan.FromSeconds(10));
                if (mine != generation) return;
                if (ok || DateTime.UtcNow >= deadline) break;
                ShowLoading(ApiClientFactory.WakingMessage, "Puede tardar hasta ~90 s. Reintentando cada 5 s.");
                await Task.Delay(TimeSpan.FromSeconds(5));
                if (mine != generation) return;
            }

            if (!ok)
            {
                ShowError("No hubo respuesta de " + ApiClientFactory.BaseUrl + ". Verifica tu conexión a internet o que el servicio esté disponible.");
                return;
            }

            ShowLoading("Cargando indicadores…", "Consultando los servicios de la API.");
            BusinessStatsDto business;
            EmailStatsDto email;
            try
            {
                var bTask = ApiClientFactory.Client.GetStringAsync("api/business/stats");
                var eTask = ApiClientFactory.Client.GetStringAsync("api/crm/stats");
                await Task.WhenAll(bTask, eTask);
                business = Json.Deserialize<BusinessStatsDto>(bTask.Result) ?? new BusinessStatsDto();
                email = Json.Deserialize<EmailStatsDto>(eTask.Result) ?? new EmailStatsDto();
            }
            catch (Exception ex)
            {
                if (mine != generation) return;
                ShowError("La API respondió pero no fue posible leer los indicadores: " + ex.Message);
                return;
            }
            if (mine != generation) return;

            negocios.SetValue(Num(business.TotalCount));
            negocios.SetFoot("último scrape: ", FormatDate(business.LastScrapedAtUtc), null);
            contacto.SetValue(Num(business.WithContactCount));
            fuentes.SetValue(Num(business.ActiveSourceCount));
            enviados.SetValue(Num(email.SentCount));
            enviados.SetFoot("último envío: ", FormatDate(email.LastSentAtUtc), null);
            fallidos.SetValue(Num(email.FailedCount));
            empresasCorreo.SetValue(Num(email.BusinessesWithEmailCount));

            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Visible;
        }

        private static string Num(long value)
        {
            return value.ToString("N0", CultureInfo.CurrentCulture);
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
        private void Go_Config(object sender, RoutedEventArgs e) { MainWindow.Current.Navigate(Routes.Configuracion); }
        private void Go_Fuentes(object sender, RoutedEventArgs e) { MainWindow.Current.Navigate(Routes.Fuentes); }
    }
}
