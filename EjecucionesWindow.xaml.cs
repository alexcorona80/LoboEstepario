using System;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;

namespace LoboEstepario
{
    public partial class EjecucionesWindow : Window
    {
        private static readonly HttpClient ApiClient = CreateApiClient();
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public EjecucionesWindow()
        {
            InitializeComponent();
            Loaded += async (sender, args) => await LoadAsync();
        }

        private static HttpClient CreateApiClient()
        {
            var configuredBaseUrl = ConfigurationManager.AppSettings["ApiBaseUrl"] ?? "http://localhost:5080/";
            return new HttpClient { BaseAddress = new Uri(configuredBaseUrl) };
        }

        private async Task LoadAsync()
        {
            RefreshButton.IsEnabled = false;
            StatusText.Text = "Cargando la actividad…";

            try
            {
                var businessTask = ApiClient.GetAsync("api/business/stats");
                var emailTask = ApiClient.GetAsync("api/crm/stats");
                await Task.WhenAll(businessTask, emailTask);

                var businessResponse = businessTask.Result;
                businessResponse.EnsureSuccessStatusCode();
                var business = json.Deserialize<BusinessStatsDto>(await businessResponse.Content.ReadAsStringAsync());

                var emailResponse = emailTask.Result;
                emailResponse.EnsureSuccessStatusCode();
                var email = json.Deserialize<EmailStatsDto>(await emailResponse.Content.ReadAsStringAsync());

                if (business != null)
                {
                    LastScrapeValue.Text = FormatDate(business.LastScrapedAtUtc);
                    BusinessTotalValue.Text = business.TotalCount.ToString("N0", CultureInfo.InvariantCulture);
                    BusinessContactValue.Text = string.Format(CultureInfo.InvariantCulture, "{0:N0} con datos de contacto", business.WithContactCount);
                    ActiveSourcesValue.Text = business.ActiveSourceCount.ToString(CultureInfo.InvariantCulture);
                }
                if (email != null)
                {
                    LastEmailValue.Text = FormatDate(email.LastSentAtUtc);
                    EmailSentValue.Text = email.SentCount.ToString("N0", CultureInfo.InvariantCulture);
                    EmailFailedValue.Text = string.Format(CultureInfo.InvariantCulture, "{0:N0} fallidos", email.FailedCount);
                    BusinessesWithEmailValue.Text = email.BusinessesWithEmailCount.ToString("N0", CultureInfo.InvariantCulture);
                }

                StatusText.Text = "Resumen de la actividad actualizado.";
            }
            catch (Exception ex)
            {
                LastScrapeValue.Text = "—";
                BusinessTotalValue.Text = "—";
                BusinessContactValue.Text = "—";
                ActiveSourcesValue.Text = "—";
                LastEmailValue.Text = "—";
                EmailSentValue.Text = "—";
                EmailFailedValue.Text = "—";
                BusinessesWithEmailValue.Text = "—";
                StatusText.Text = "No fue posible cargar la actividad: " + ex.Message;
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        private static string FormatDate(string utc)
        {
            DateTime utcTime;
            if (!DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out utcTime))
                return "nunca";
            var local = utcTime.ToLocalTime();
            return local.Date == DateTime.Now.Date
                ? string.Format(CultureInfo.InvariantCulture, "Hoy · {0:HH:mm}", local)
                : string.Format(CultureInfo.InvariantCulture, "{0:dd/MM} · {0:HH:mm}", local);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
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