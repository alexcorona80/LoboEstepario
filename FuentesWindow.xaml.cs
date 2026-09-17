using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LoboEstepario
{
    public partial class FuentesWindow : Window
    {
        private static readonly HttpClient ApiClient = CreateApiClient();
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        private static readonly Brush GreenBrush = CreateBrush("#7BC96F");
        private static readonly Brush RedBrush = CreateBrush("#E2574C");
        private static readonly Brush GoldBrushUi = CreateBrush("#E2A63D");

        public FuentesWindow()
        {
            InitializeComponent();
            Loaded += async (sender, args) => await LoadAsync();
        }

        private static HttpClient CreateApiClient()
        {
            var configuredBaseUrl = ConfigurationManager.AppSettings["ApiBaseUrl"] ?? "http://localhost:5080/";
            return new HttpClient { BaseAddress = new Uri(configuredBaseUrl) };
        }

        private static Brush CreateBrush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        private async Task LoadAsync()
        {
            RecheckButton.IsEnabled = false;
            StatusText.Text = "Comprobando las conexiones…";
            ResetUi();

            await Task.WhenAll(CheckOvertureAsync(), CheckMongoAsync());

            RecheckButton.IsEnabled = true;
            StatusText.Text = "Estado de las fuentes actualizado.";
        }

        private void ResetUi()
        {
            SetChip(OvertureChip, OvertureChipText, "Comprobando…", null);
            OvertureMetadataText.Visibility = Visibility.Collapsed;
            DatasetPathText.Visibility = Visibility.Collapsed;
            OvertureErrorText.Visibility = Visibility.Collapsed;
            SetChip(MongoChip, MongoChipText, "Comprobando…", null);
            MongoStatsText.Text = "…";
            MongoErrorText.Visibility = Visibility.Collapsed;
        }

        private static void SetChip(Border chip, TextBlock chipText, string text, Brush brush)
        {
            chipText.Text = text;
            if (brush == null)
            {
                chipText.Foreground = (Brush)chip.TryFindResource("MutedBrush");
                chip.BorderBrush = (Brush)chip.TryFindResource("LineBrush");
                return;
            }
            chipText.Foreground = brush;
            chip.BorderBrush = brush;
        }

        private async Task CheckOvertureAsync()
        {
            try
            {
                var response = await ApiClient.GetAsync("api/health/overture");
                var body = await response.Content.ReadAsStringAsync();
                var payload = json.DeserializeObject(body) as IDictionary<string, object>;
                var status = payload != null && payload.ContainsKey("status") ? Convert.ToString(payload["status"]) : null;

                if (response.IsSuccessStatusCode && string.Equals(status, "reachable", StringComparison.OrdinalIgnoreCase))
                {
                    SetChip(OvertureChip, OvertureChipText, "alcanzable", GreenBrush);
                    var pieces = new System.Collections.Generic.List<string>();
                    if (payload != null && payload.ContainsKey("releaseVersion")) pieces.Add("release " + payload["releaseVersion"]);
                    if (payload != null && payload.ContainsKey("theme")) pieces.Add("theme " + payload["theme"]);
                    if (payload != null && payload.ContainsKey("type")) pieces.Add("tipo " + payload["type"]);
                    OvertureMetadataText.Text = string.Join("  ·  ", pieces);
                    OvertureMetadataText.Visibility = Visibility.Visible;
                    if (payload != null && payload.ContainsKey("datasetPath"))
                    {
                        DatasetPathText.Text = Convert.ToString(payload["datasetPath"]);
                        DatasetPathText.Visibility = Visibility.Visible;
                    }
                    return;
                }

                if (response.IsSuccessStatusCode && string.Equals(status, "empty", StringComparison.OrdinalIgnoreCase))
                {
                    SetChip(OvertureChip, OvertureChipText, "vacío", GoldBrushUi);
                    OvertureErrorText.Text = "El dataset se alcanzó pero no devolvió lugares.";
                    OvertureErrorText.Visibility = Visibility.Visible;
                    return;
                }

                SetChip(OvertureChip, OvertureChipText, "error", RedBrush);
                var detail = payload != null && payload.ContainsKey("detail") ? Convert.ToString(payload["detail"]) : body;
                OvertureErrorText.Text = "No se pudo alcanzar el dataset: " + AiService.Truncate(detail);
                OvertureErrorText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                SetChip(OvertureChip, OvertureChipText, "sin conexión", RedBrush);
                OvertureErrorText.Text = "No fue posible consultar la API: " + ex.Message;
                OvertureErrorText.Visibility = Visibility.Visible;
            }
        }

        private async Task CheckMongoAsync()
        {
            try
            {
                var response = await ApiClient.GetAsync("api/business/stats");
                response.EnsureSuccessStatusCode();
                var stats = json.Deserialize<BusinessStatsDto>(await response.Content.ReadAsStringAsync());
                if (stats == null)
                {
                    SetChip(MongoChip, MongoChipText, "conectado", GreenBrush);
                    MongoStatsText.Text = "La base de datos respondió pero no devolvió estadísticas.";
                    return;
                }
                SetChip(MongoChip, MongoChipText, "conectado", GreenBrush);
                MongoStatsText.Text = string.Format(CultureInfo.InvariantCulture, "{0:N0} negocios guardados · {1:N0} con datos de contacto.",
                    stats.TotalCount, stats.WithContactCount);
            }
            catch (Exception ex)
            {
                SetChip(MongoChip, MongoChipText, "sin conexión", RedBrush);
                MongoErrorText.Text = "No fue posible leer BusinessList: " + ex.Message;
                MongoErrorText.Visibility = Visibility.Visible;
            }
        }

        private async void Recheck_Click(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}