using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoboEstepario
{
    public partial class ConfigWindow : Window
    {
        private static readonly HttpClient ApiClient = ApiClientFactory.Client;
        private readonly JavaScriptSerializer json = CreateSerializer();

        public ConfigWindow()
        {
            InitializeComponent();
            Loaded += (sender, args) => LoadSavedSettings();
        }


        private static JavaScriptSerializer CreateSerializer()
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 100 };
            return serializer;
        }

        private static string GetSetting(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }

        private void LoadSavedSettings()
        {
            SmtpHostBox.Text = GetSetting("CrmSmtpHost");
            SmtpPortBox.Text = string.IsNullOrWhiteSpace(GetSetting("CrmSmtpPort")) ? "587" : GetSetting("CrmSmtpPort");
            SmtpUserBox.Text = GetSetting("CrmSmtpUser");
            SmtpPasswordBox.Password = GetSetting("CrmSmtpPassword");
            FromNameBox.Text = GetSetting("CrmSmtpFromName");
            UseSslBox.IsChecked = !string.Equals(ConfigurationManager.AppSettings["CrmSmtpUseSsl"], "False", StringComparison.OrdinalIgnoreCase);
            MatchProvider(SmtpHostBox.Text);

            FacebookPageIdBox.Text = GetSetting("FacebookPageId");
            FacebookTokenBox.Text = GetSetting("FacebookAccessToken");
            FacebookAppIdBox.Text = GetSetting("FacebookAppId");
            FacebookAppSecretBox.Password = GetSetting("FacebookAppSecret");

            InstagramAccountIdBox.Text = GetSetting("InstagramBusinessAccountId");
            InstagramTokenBox.Text = GetSetting("InstagramAccessToken");
            InstagramAppIdBox.Text = GetSetting("InstagramAppId");
            InstagramAppSecretBox.Password = GetSetting("InstagramAppSecret");

            MatchAiProvider(GetSetting("AiProvider"));
            AiBaseUrlBox.Text = GetSetting("AiBaseUrl");
            AiModelBox.Text = GetSetting("AiModel");
            AiImageModelBox.Text = GetSetting("AiImageModel");
            AiApiKeyBox.Password = GetSetting("AiApiKey");
        }

        private static int AiProviderIndex(string provider)
        {
            switch ((provider ?? string.Empty).ToLowerInvariant())
            {
                case "azure_openai": return 1;
                case "anthropic": return 2;
                case "google_gemini": return 3;
                case "openai_compatible": return 4;
                default: return 0;
            }
        }

        private static string AiProviderTag(int index)
        {
            switch (index)
            {
                case 1: return "azure_openai";
                case 2: return "anthropic";
                case 3: return "google_gemini";
                case 4: return "openai_compatible";
                default: return "openai";
            }
        }

        private void MatchAiProvider(string provider)
        {
            var index = AiProviderIndex(provider);
            AiProviderBox.SelectionChanged -= AiProviderBox_SelectionChanged;
            AiProviderBox.SelectedIndex = index;
            AiProviderBox.SelectionChanged += AiProviderBox_SelectionChanged;
        }

        private void AiProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AiProviderBox.SelectedIndex == -1) return;
            switch (AiProviderBox.SelectedIndex)
            {
                case 0:
                    if (string.IsNullOrWhiteSpace(AiModelBox.Text)) AiModelBox.Text = "gpt-4o-mini";
                    if (string.IsNullOrWhiteSpace(AiImageModelBox.Text)) AiImageModelBox.Text = "gpt-image-1";
                    break;
                case 1:
                    if (string.IsNullOrWhiteSpace(AiModelBox.Text)) AiModelBox.Text = "gpt-4o-mini";
                    if (string.IsNullOrWhiteSpace(AiImageModelBox.Text)) AiImageModelBox.Text = "gpt-image-1";
                    break;
                case 2:
                    if (string.IsNullOrWhiteSpace(AiModelBox.Text)) AiModelBox.Text = "claude-3-5-sonnet-latest";
                    break;
                case 3:
                    if (string.IsNullOrWhiteSpace(AiModelBox.Text)) AiModelBox.Text = "gemini-1.5-pro";
                    break;
                case 4:
                    break;
            }
        }

        private void SaveAiAgent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                SetSetting(config, "AiProvider", AiProviderTag(AiProviderBox.SelectedIndex));
                SetSetting(config, "AiBaseUrl", AiBaseUrlBox.Text.Trim());
                SetSetting(config, "AiModel", AiModelBox.Text.Trim());
                SetSetting(config, "AiImageModel", AiImageModelBox.Text.Trim());
                SetSetting(config, "AiApiKey", AiApiKeyBox.Password);
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                StatusText.Text = "Configuración del agente de IA guardada.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible guardar el agente de IA: " + ex.Message;
            }
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

        private static void SetSetting(System.Configuration.Configuration config, string key, string value)
        {
            if (config.AppSettings.Settings[key] == null)
                config.AppSettings.Settings.Add(key, value);
            else
                config.AppSettings.Settings[key].Value = value;
        }

        private void SaveAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                SetSetting(config, "CrmSmtpHost", SmtpHostBox.Text.Trim());
                SetSetting(config, "CrmSmtpPort", string.IsNullOrWhiteSpace(SmtpPortBox.Text) ? "587" : SmtpPortBox.Text.Trim());
                SetSetting(config, "CrmSmtpUser", SmtpUserBox.Text.Trim());
                SetSetting(config, "CrmSmtpPassword", SmtpPasswordBox.Password);
                SetSetting(config, "CrmSmtpFromName", FromNameBox.Text.Trim());
                SetSetting(config, "CrmSmtpUseSsl", (UseSslBox.IsChecked ?? true).ToString());
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                StatusText.Text = "Cuenta de correo guardada.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible guardar la cuenta: " + ex.Message;
            }
        }

        private void SaveFacebook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                SetSetting(config, "FacebookPageId", FacebookPageIdBox.Text.Trim());
                SetSetting(config, "FacebookAccessToken", FacebookTokenBox.Text.Trim());
                SetSetting(config, "FacebookAppId", FacebookAppIdBox.Text.Trim());
                SetSetting(config, "FacebookAppSecret", FacebookAppSecretBox.Password);
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                StatusText.Text = "Configuración de Facebook guardada.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible guardar Facebook: " + ex.Message;
            }
        }

        private void SaveInstagram_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                SetSetting(config, "InstagramBusinessAccountId", InstagramAccountIdBox.Text.Trim());
                SetSetting(config, "InstagramAccessToken", InstagramTokenBox.Text.Trim());
                SetSetting(config, "InstagramAppId", InstagramAppIdBox.Text.Trim());
                SetSetting(config, "InstagramAppSecret", InstagramAppSecretBox.Password);
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                StatusText.Text = "Configuración de Instagram guardada.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible guardar Instagram: " + ex.Message;
            }
        }

        private static SmtpConfigDto CurrentSmtp()
        {
            int port = 587;
            int.TryParse(ConfigurationManager.AppSettings["CrmSmtpPort"] ?? "587", out port);
            return new SmtpConfigDto
            {
                Host = GetSetting("CrmSmtpHost"),
                Port = port,
                Username = GetSetting("CrmSmtpUser"),
                Password = GetSetting("CrmSmtpPassword"),
                FromName = GetSetting("CrmSmtpFromName"),
                EnableSsl = !string.Equals(ConfigurationManager.AppSettings["CrmSmtpUseSsl"], "False", StringComparison.OrdinalIgnoreCase),
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
                TestButton.IsEnabled = false;
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
                TestButton.IsEnabled = true;
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
}