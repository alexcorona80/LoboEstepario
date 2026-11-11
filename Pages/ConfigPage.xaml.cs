using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LoboEstepario.Pages
{
    public partial class ConfigPage : UserControl
    {
        private static readonly HttpClient ApiClient = ApiClientFactory.Client;
        private bool loading;

        public ConfigPage()
        {
            InitializeComponent();
            ApiUrlText.Text = ApiClientFactory.BaseUrl;
            SettingsPathText.Text = AppConfig.UserSettingsPath;
            Loaded += (sender, args) => LoadSavedSettings();
            SmtpUserBox.TextChanged += (sender, args) => UpdateTestTarget();
        }

        private static string GetSetting(string key)
        {
            var value = AppConfig.Get(key);
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }

        private void UpdateTestTarget()
        {
            TestTargetRun.Text = SmtpUserBox.Text.Trim();
        }

        private void ShowSaved()
        {
            ClosureRun.Text = "Los cambios se guardaron en el archivo local de configuración (" + AppConfig.UserSettingsPath + ").";
            ClosureAlert.Visibility = Visibility.Visible;
        }

        private void LoadSavedSettings()
        {
            loading = true;
            try
            {
                SmtpHostBox.Text = GetSetting("CrmSmtpHost");
                SmtpPortBox.Text = string.IsNullOrWhiteSpace(GetSetting("CrmSmtpPort")) ? "587" : GetSetting("CrmSmtpPort");
                SmtpUserBox.Text = GetSetting("CrmSmtpUser");
                SmtpPasswordBox.Password = GetSetting("CrmSmtpPassword");
                FromNameBox.Text = GetSetting("CrmSmtpFromName");
                UseSslBox.IsChecked = !string.Equals(AppConfig.Get("CrmSmtpUseSsl"), "False", StringComparison.OrdinalIgnoreCase);
                ProviderBox.SelectedIndex = SmtpProviderIndex(SmtpHostBox.Text);
                UpdateTestTarget();

                FbPageIdBox.Text = GetSetting("FacebookPageId");
                FbTokenBox.Password = GetSetting("FacebookAccessToken");
                FbAppIdBox.Text = GetSetting("FacebookAppId");
                FbAppSecretBox.Password = GetSetting("FacebookAppSecret");

                IgAccountIdBox.Text = GetSetting("InstagramBusinessAccountId");
                IgTokenBox.Password = GetSetting("InstagramAccessToken");
                IgAppIdBox.Text = GetSetting("InstagramAppId");
                IgAppSecretBox.Password = GetSetting("InstagramAppSecret");

                AiProviderBox.SelectedIndex = AiProviderIndex(GetSetting("AiProvider"));
                AiBaseUrlBox.Text = GetSetting("AiBaseUrl");
                AiModelBox.Text = GetSetting("AiModel");
                AiImageModelBox.Text = GetSetting("AiImageModel");
                AiApiKeyBox.Password = GetSetting("AiApiKey");
            }
            finally
            {
                loading = false;
            }
        }

        private static int SmtpProviderIndex(string host)
        {
            var h = (host ?? string.Empty).ToLowerInvariant();
            if (h.Contains("gmail")) return 0;
            if (h.Contains("office365")) return 1;
            if (h.Contains("yahoo")) return 2;
            return 3;
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

        private static string TagOf(ComboBox box)
        {
            var item = box.SelectedItem as ComboBoxItem;
            return item == null ? string.Empty : item.Tag as string ?? string.Empty;
        }

        private void ProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (loading || ProviderBox.SelectedIndex == -1) return;
            string host;
            switch (TagOf(ProviderBox))
            {
                case "Gmail": host = "smtp.gmail.com"; break;
                case "Office365": host = "smtp.office365.com"; break;
                case "Yahoo": host = "smtp.mail.yahoo.com"; break;
                default: return;
            }
            SmtpHostBox.Text = host;
            SmtpPortBox.Text = "587";
            UseSslBox.IsChecked = true;
        }

        private void AiProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (loading || AiProviderBox.SelectedIndex == -1) return;
            switch (TagOf(AiProviderBox))
            {
                case "openai":
                case "azure_openai":
                    if (string.IsNullOrWhiteSpace(AiModelBox.Text)) AiModelBox.Text = "gpt-4o-mini";
                    if (string.IsNullOrWhiteSpace(AiImageModelBox.Text)) AiImageModelBox.Text = "gpt-image-1";
                    break;
                case "anthropic":
                    if (string.IsNullOrWhiteSpace(AiModelBox.Text)) AiModelBox.Text = "claude-3-5-sonnet-latest";
                    break;
                case "google_gemini":
                    if (string.IsNullOrWhiteSpace(AiModelBox.Text)) AiModelBox.Text = "gemini-1.5-pro";
                    break;
            }
        }

        private void SaveAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppConfig.Set("CrmSmtpHost", SmtpHostBox.Text.Trim());
                AppConfig.Set("CrmSmtpPort", string.IsNullOrWhiteSpace(SmtpPortBox.Text) ? "587" : SmtpPortBox.Text.Trim());
                AppConfig.Set("CrmSmtpUser", SmtpUserBox.Text.Trim());
                AppConfig.Set("CrmSmtpPassword", SmtpPasswordBox.Password);
                AppConfig.Set("CrmSmtpFromName", FromNameBox.Text.Trim());
                AppConfig.Set("CrmSmtpUseSsl", (UseSslBox.IsChecked ?? true).ToString());
                AppConfig.Save();
                SmtpStatus.Text = "Cuenta de correo guardada.";
                ShowSaved();
            }
            catch (Exception ex)
            {
                SmtpStatus.Text = "No fue posible guardar la cuenta: " + ex.Message;
            }
        }

        private void SaveFacebook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppConfig.Set("FacebookPageId", FbPageIdBox.Text.Trim());
                AppConfig.Set("FacebookAccessToken", FbTokenBox.Password.Trim());
                AppConfig.Set("FacebookAppId", FbAppIdBox.Text.Trim());
                AppConfig.Set("FacebookAppSecret", FbAppSecretBox.Password);
                AppConfig.Save();
                FbStatus.Text = "Configuración de Facebook guardada.";
                ShowSaved();
            }
            catch (Exception ex)
            {
                FbStatus.Text = "No fue posible guardar Facebook: " + ex.Message;
            }
        }

        private void SaveInstagram_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppConfig.Set("InstagramBusinessAccountId", IgAccountIdBox.Text.Trim());
                AppConfig.Set("InstagramAccessToken", IgTokenBox.Password.Trim());
                AppConfig.Set("InstagramAppId", IgAppIdBox.Text.Trim());
                AppConfig.Set("InstagramAppSecret", IgAppSecretBox.Password);
                AppConfig.Save();
                IgStatus.Text = "Configuración de Instagram guardada.";
                ShowSaved();
            }
            catch (Exception ex)
            {
                IgStatus.Text = "No fue posible guardar Instagram: " + ex.Message;
            }
        }

        private void SaveAiAgent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var provider = TagOf(AiProviderBox);
                AppConfig.Set("AiProvider", string.IsNullOrEmpty(provider) ? "openai" : provider);
                AppConfig.Set("AiBaseUrl", AiBaseUrlBox.Text.Trim());
                AppConfig.Set("AiModel", AiModelBox.Text.Trim());
                AppConfig.Set("AiImageModel", AiImageModelBox.Text.Trim());
                AppConfig.Set("AiApiKey", AiApiKeyBox.Password);
                AppConfig.Save();
                AiStatus.Text = "Configuración del agente de IA guardada.";
                ShowSaved();
            }
            catch (Exception ex)
            {
                AiStatus.Text = "No fue posible guardar el agente de IA: " + ex.Message;
            }
        }

        private SmtpConfigDto CurrentSmtp()
        {
            int port;
            if (!int.TryParse(SmtpPortBox.Text, out port) || port == 0) port = 587;
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
                SmtpStatus.Text = "Completa host, usuario y contraseña antes de probar.";
                return;
            }

            try
            {
                TestButton.IsEnabled = false;
                SaveSmtpButton.IsEnabled = false;
                SmtpStatus.Text = ApiClientFactory.IsWaking ? ApiClientFactory.WakingMessage : "Enviando correo de prueba…";
                var request = Json.Serialize(new TestEmailRequestDto { Smtp = smtp, ToEmail = smtp.Username });
                var response = await ApiClient.PostAsync("api/crm/test", new StringContent(request, Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var result = Json.Deserialize<EmailTestResponseDto>(await response.Content.ReadAsStringAsync());
                if (result != null && result.Success)
                    SmtpStatus.Text = "Correo de prueba enviado a " + smtp.Username + ". Revisa la bandeja.";
                else
                    SmtpStatus.Text = "Prueba fallida: " + (result == null || string.IsNullOrEmpty(result.Error) ? "sin respuesta" : result.Error);
            }
            catch (TaskCanceledException)
            {
                SmtpStatus.Text = "No fue posible probar el correo: la API tardó demasiado en responder. Inténtalo de nuevo.";
            }
            catch (HttpRequestException ex)
            {
                SmtpStatus.Text = "No fue posible probar el correo: no se pudo conectar con la API (" + ex.Message + ").";
            }
            catch (Exception ex)
            {
                SmtpStatus.Text = "No fue posible probar el correo: " + ex.Message;
            }
            finally
            {
                TestButton.IsEnabled = true;
                SaveSmtpButton.IsEnabled = true;
            }
        }
    }
}
