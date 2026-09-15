using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace LoboEstepario
{
    public partial class CrmWindow : Window
    {
        private static readonly HttpClient ApiClient = CreateApiClient();
        private readonly JavaScriptSerializer json = CreateSerializer();
        private readonly ICollectionView recipientsView;
        private readonly string preselectedCompanyName;

        public ObservableCollection<RecipientRow> Recipients { get; } = new ObservableCollection<RecipientRow>();
        public ObservableCollection<HistoryRow> History { get; } = new ObservableCollection<HistoryRow>();

        public CrmWindow(string preselectedCompanyName = null)
        {
            InitializeComponent();
            this.preselectedCompanyName = preselectedCompanyName;
            DataContext = this;
            recipientsView = CollectionViewSource.GetDefaultView(Recipients);
            recipientsView.Filter = MatchesRecipientSearch;
            RecipientsGrid.ItemsSource = recipientsView;
            HistoryGrid.ItemsSource = History;

            Loaded += async (sender, args) =>
            {
                LoadSavedAccount();
                await LoadRecipientsAsync();
                await LoadHistoryAsync();
                await LoadStatsAsync();
            };
        }

        private static HttpClient CreateApiClient()
        {
            var configuredBaseUrl = ConfigurationManager.AppSettings["ApiBaseUrl"] ?? "http://localhost:5080/";
            return new HttpClient { BaseAddress = new Uri(configuredBaseUrl) };
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 100 };
            return serializer;
        }

        private void LoadSavedAccount()
        {
            var host = ConfigurationManager.AppSettings["CrmSmtpHost"];
            var port = ConfigurationManager.AppSettings["CrmSmtpPort"];
            var user = ConfigurationManager.AppSettings["CrmSmtpUser"];
            var pass = ConfigurationManager.AppSettings["CrmSmtpPassword"];
            var from = ConfigurationManager.AppSettings["CrmSmtpFromName"];
            var sslValue = ConfigurationManager.AppSettings["CrmSmtpUseSsl"];

            SmtpHostBox.Text = string.IsNullOrWhiteSpace(host) ? string.Empty : host;
            SmtpPortBox.Text = string.IsNullOrWhiteSpace(port) ? "587" : port;
            SmtpUserBox.Text = user ?? string.Empty;
            SmtpPasswordBox.Password = pass ?? string.Empty;
            FromNameBox.Text = from ?? string.Empty;
            UseSslBox.IsChecked = !string.Equals(sslValue, "False", StringComparison.OrdinalIgnoreCase);

            MatchProvider(host ?? string.Empty);
        }

        private void MatchProvider(string host)
        {
            var index = host.ToLowerInvariant().Contains("gmail") ? 0
                : host.ToLowerInvariant().Contains("office365") ? 1
                : host.ToLowerInvariant().Contains("yahoo") ? 2
                : 3;
            ProviderBox.SelectionChanged -= ProviderBox_SelectionChanged;
            ProviderBox.SelectedIndex = index;
            ProviderBox.SelectionChanged += ProviderBox_SelectionChanged;
        }

        private void ProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProviderBox.SelectedIndex == -1) return;
            switch (ProviderBox.SelectedIndex)
            {
                case 0:
                    SmtpHostBox.Text = "smtp.gmail.com";
                    SmtpPortBox.Text = "587";
                    UseSslBox.IsChecked = true;
                    break;
                case 1:
                    SmtpHostBox.Text = "smtp.office365.com";
                    SmtpPortBox.Text = "587";
                    UseSslBox.IsChecked = true;
                    break;
                case 2:
                    SmtpHostBox.Text = "smtp.mail.yahoo.com";
                    SmtpPortBox.Text = "587";
                    UseSslBox.IsChecked = true;
                    break;
                case 3:
                    break;
            }
        }

        private void SaveAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["CrmSmtpHost"].Value = SmtpHostBox.Text.Trim();
                config.AppSettings.Settings["CrmSmtpPort"].Value = string.IsNullOrWhiteSpace(SmtpPortBox.Text) ? "587" : SmtpPortBox.Text.Trim();
                config.AppSettings.Settings["CrmSmtpUser"].Value = SmtpUserBox.Text.Trim();
                config.AppSettings.Settings["CrmSmtpPassword"].Value = SmtpPasswordBox.Password;
                config.AppSettings.Settings["CrmSmtpFromName"].Value = FromNameBox.Text.Trim();
                config.AppSettings.Settings["CrmSmtpUseSsl"].Value = (UseSslBox.IsChecked ?? true).ToString();
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                StatusText.Text = "Cuenta de correo guardada.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible guardar la cuenta: " + ex.Message;
            }
        }

        private bool MatchesRecipientSearch(object item)
        {
            var row = item as RecipientRow;
            var query = RecipientSearchBox == null ? string.Empty : RecipientSearchBox.Text.Trim();
            return row != null && (string.IsNullOrWhiteSpace(query)
                || row.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || row.Email.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || row.Location.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void RecipientSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (recipientsView == null) return;
            recipientsView.Refresh();
            UpdateRecipientCounters();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in recipientsView.Cast<RecipientRow>())
            {
                row.Selected = true;
            }
            UpdateRecipientCounters();
        }

        private void UpdateRecipientCounters()
        {
            if (RecipientCount != null)
                RecipientCount.Text = string.Format("{0} con correo", Recipients.Count);
            var selected = Recipients.Count(row => row.Selected);
            if (SelectedCountLabel != null)
                SelectedCountLabel.Text = selected == 0 ? "ninguno seleccionado" : string.Format("{0} seleccionado{1}", selected, selected == 1 ? string.Empty : "s");
        }

        private async Task LoadRecipientsAsync()
        {
            try
            {
                StatusText.Text = "Cargando destinatarios desde la base de datos…";
                var response = await ApiClient.GetAsync("api/crm/recipients?limit=2000");
                response.EnsureSuccessStatusCode();
                var payload = json.Deserialize<BusinessRecipientDto[]>(await response.Content.ReadAsStringAsync()) ?? new BusinessRecipientDto[0];
                Recipients.Clear();
                foreach (var place in payload)
                {
                    var email = FirstEmail(place.Emails);
                    if (string.IsNullOrWhiteSpace(email)) continue;
                    Recipients.Add(new RecipientRow
                    {
                        Id = place.Id ?? string.Empty,
                        Name = place.Name ?? "Sin nombre",
                        Email = email,
                        Location = JoinLocation(place.Locality, place.Region, place.Country),
                        Selected = false,
                    });
                }
                if (!string.IsNullOrEmpty(preselectedCompanyName))
                {
                    foreach (var row in Recipients)
                    {
                        row.Selected = string.Equals(row.Name, preselectedCompanyName, StringComparison.OrdinalIgnoreCase);
                    }
                }
                recipientsView.Refresh();
                UpdateRecipientCounters();
                StatusText.Text = string.Format("{0} empresas con correo registrado.", Recipients.Count);
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible cargar los destinatarios: " + ex.Message;
            }
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                var response = await ApiClient.GetAsync("api/crm/history?limit=100");
                response.EnsureSuccessStatusCode();
                var payload = json.Deserialize<SentEmailDto[]>(await response.Content.ReadAsStringAsync()) ?? new SentEmailDto[0];
                History.Clear();
                foreach (var item in payload)
                {
                    History.Add(new HistoryRow
                    {
                        BusinessName = item.BusinessName ?? string.Empty,
                        ToEmail = item.ToEmail ?? string.Empty,
                        Subject = item.Subject ?? string.Empty,
                        SentAt = FormatHistoryDate(item.SentAtUtc),
                        Status = FormatStatus(item.Status),
                    });
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible cargar el historial: " + ex.Message;
            }
        }

        private async Task LoadStatsAsync()
        {
            try
            {
                var response = await ApiClient.GetAsync("api/crm/stats");
                response.EnsureSuccessStatusCode();
                var stats = json.Deserialize<EmailStatsDto>(await response.Content.ReadAsStringAsync());
                if (stats == null) return;
                if (StatsText != null)
                    StatsText.Text = string.Format("Enviados {0} · Fallidos {1} · Empresas con correo {2}",
                        stats.SentCount.ToString("N0"), stats.FailedCount.ToString("N0"), stats.BusinessesWithEmailCount.ToString("N0"));
            }
            catch (Exception ex)
            {
                if (StatusText != null) StatusText.Text = "No fue posible cargar las estadísticas: " + ex.Message;
            }
        }

        public static string FormatHistoryDate(string isoDate)
        {
            DateTime utc;
            if (!DateTime.TryParse(isoDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out utc))
                return string.Empty;
            return utc.ToLocalTime().ToString("dd/MM HH:mm");
        }

        public static string FormatStatus(int status) => status == 1 ? "FALLIDO" : "ENVIADO";

        private static string FirstEmail(string[] emails)
        {
            return (emails ?? new string[0]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        }

        private static string JoinLocation(string locality, string region, string country)
        {
            return string.Join(", ", new[] { locality, region, country }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private SmtpConfigDto CurrentSmtp()
        {
            int port = 587;
            int.TryParse(SmtpPortBox.Text.Trim(), out port);
            return new SmtpConfigDto
            {
                Host = SmtpHostBox.Text.Trim(),
                Port = port,
                Username = SmtpUserBox.Text.Trim(),
                Password = SmtpPasswordBox.Password,
                FromName = FromNameBox.Text.Trim(),
                EnableSsl = UseSslBox.IsChecked ?? true,
            };
        }

        private async void SendTest_Click(object sender, RoutedEventArgs e)
        {
            var smtp = CurrentSmtp();
            if (string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.Username) || string.IsNullOrWhiteSpace(smtp.Password))
            {
                StatusText.Text = "Completa host, usuario y contraseña antes de probar.";
                return;
            }

            try
            {
                SendButton.IsEnabled = false;
                StatusText.Text = "Enviando correo de prueba…";
                var request = json.Serialize(new TestEmailRequestDto { Smtp = smtp, ToEmail = smtp.Username });
                var response = await ApiClient.PostAsync("api/crm/test", new StringContent(request, Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var result = json.Deserialize<EmailTestResponseDto>(await response.Content.ReadAsStringAsync());
                if (result != null && result.Success)
                    StatusText.Text = "Correo de prueba enviado a " + smtp.Username + ". Revisa la bandeja.";
                else
                    StatusText.Text = "Prueba fallida: " + (result == null ? "sin respuesta" : result.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible probar el correo: " + ex.Message;
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }

        private async void SendEmails_Click(object sender, RoutedEventArgs e)
        {
            var selected = Recipients.Where(row => row.Selected).ToList();
            if (selected.Count == 0)
            {
                StatusText.Text = "Selecciona al menos un destinatario.";
                return;
            }

            var smtp = CurrentSmtp();
            if (string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.Username) || string.IsNullOrWhiteSpace(smtp.Password))
            {
                StatusText.Text = "Completa la cuenta de correo antes de enviar.";
                return;
            }
            if (string.IsNullOrWhiteSpace(BodyBox.Text))
            {
                StatusText.Text = "Escribe el cuerpo del correo.";
                return;
            }

            try
            {
                SendButton.IsEnabled = false;
                StatusText.Text = string.Format("Enviando a {0} empresa{1}…", selected.Count, selected.Count == 1 ? string.Empty : "s");
                var request = json.Serialize(new SendEmailRequestDto
                {
                    Smtp = smtp,
                    Recipients = selected.Select(row => new EmailRecipientDto
                    {
                        BusinessId = row.Id,
                        BusinessName = row.Name,
                        ToEmail = row.Email,
                    }).ToArray(),
                    Subject = SubjectBox.Text.Trim(),
                    Body = BodyBox.Text,
                });
                var response = await ApiClient.PostAsync("api/crm/send", new StringContent(request, Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var result = json.Deserialize<EmailSendResponseDto>(await response.Content.ReadAsStringAsync());
                if (result != null)
                {
                    StatusText.Text = string.Format("Envío completo · {0} enviado{1}, {2} fallido{3}.",
                        result.SentCount, result.SentCount == 1 ? string.Empty : "s",
                        result.FailedCount, result.FailedCount == 1 ? string.Empty : "s");
                    await LoadHistoryAsync();
                    await LoadStatsAsync();
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible enviar los correos: " + ex.Message;
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

    public sealed class RecipientRow : INotifyPropertyChanged
    {
        private bool selected;
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Location { get; set; }

        public bool Selected
        {
            get { return selected; }
            set
            {
                if (selected == value) return;
                selected = value;
                OnPropertyChanged("Selected");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }

    public sealed class HistoryRow
    {
        public string BusinessName { get; set; }
        public string ToEmail { get; set; }
        public string Subject { get; set; }
        public string SentAt { get; set; }
        public string Status { get; set; }
    }

    public sealed class BusinessRecipientDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string[] Emails { get; set; }
        public string Locality { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
    }

    public sealed class SmtpConfigDto
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FromName { get; set; }
        public bool EnableSsl { get; set; }
    }

    public sealed class TestEmailRequestDto
    {
        public SmtpConfigDto Smtp { get; set; }
        public string ToEmail { get; set; }
    }

    public sealed class EmailRecipientDto
    {
        public string BusinessId { get; set; }
        public string BusinessName { get; set; }
        public string ToEmail { get; set; }
    }

    public sealed class SendEmailRequestDto
    {
        public SmtpConfigDto Smtp { get; set; }
        public EmailRecipientDto[] Recipients { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    public sealed class EmailTestResponseDto
    {
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public sealed class EmailSendResponseDto
    {
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
    }

    public sealed class SentEmailDto
    {
        public string BusinessName { get; set; }
        public string ToEmail { get; set; }
        public string Subject { get; set; }
        public string SentAtUtc { get; set; }
        public int Status { get; set; }
        public string Error { get; set; }
    }

    public sealed class EmailStatsDto
    {
        public long SentCount { get; set; }
        public long FailedCount { get; set; }
        public long BusinessesWithEmailCount { get; set; }
        public string LastSentAtUtc { get; set; }
    }
}