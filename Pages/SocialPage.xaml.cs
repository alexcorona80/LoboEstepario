using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace LoboEstepario.Pages
{
    /// <summary>
    /// Redes sociales: genera contenido con IA, publica en Facebook/Instagram (Graph API)
    /// y reenvía el mensaje por correo (API de LoboEstepario).
    /// </summary>
    public partial class SocialPage : UserControl, IPage
    {
        private static readonly HttpClient ApiClient = ApiClientFactory.Client;
        // Graph API: cliente propio con timeout largo (subida de imágenes).
        private static readonly HttpClient SocialClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

        private readonly ObservableCollection<SocialRecipientRow> recipients = new ObservableCollection<SocialRecipientRow>();
        private readonly ICollectionView recipientsView;
        private byte[] currentImage;
        private bool busy;
        private bool loadingRecipients;

        public SocialPage()
        {
            InitializeComponent();
            recipientsView = CollectionViewSource.GetDefaultView(recipients);
            recipientsView.Filter = MatchesRecipientSearch;
            RecipientsGrid.ItemsSource = recipientsView;
            recipients.CollectionChanged += (s, e) => UpdateRecipientState();

            Unloaded += (s, e) => ApiClientFactory.WakingChanged -= OnWakingChanged;
            Loaded += (s, e) => ApiClientFactory.WakingChanged += OnWakingChanged;
            UpdateRecipientState();
            RefreshAiWarning();
        }

        public async Task OnNavigatedToAsync(object parameter)
        {
            RefreshAiWarning();
            await LoadRecipientsAsync();
        }

        private void OnWakingChanged(bool waking)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (loadingRecipients && waking) StatusText.Text = ApiClientFactory.WakingMessage;
            }));
        }

        private void RefreshAiWarning()
        {
            var configured = AiService.IsConfigured;
            AiWarning.Visibility = configured ? Visibility.Collapsed : Visibility.Visible;
            GenerateButton.IsEnabled = !busy && configured;
        }

        private static string GetSetting(string key)
        {
            var value = AppConfig.Get(key);
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private void SetBusy(bool value)
        {
            busy = value;
            GenerateButton.IsEnabled = !value && AiService.IsConfigured;
            PublishButton.IsEnabled = !value;
            EmailButton.IsEnabled = !value;
        }

        // ---------------------------------------------------------------- destinatarios

        private async Task LoadRecipientsAsync()
        {
            if (busy) return;
            busy = true;
            loadingRecipients = true;
            try
            {
                StatusText.Text = ApiClientFactory.IsWaking ? ApiClientFactory.WakingMessage : "Cargando destinatarios…";
                using (var response = await ApiClient.GetAsync("api/crm/recipients?limit=2000"))
                {
                    response.EnsureSuccessStatusCode();
                    var payload = Json.Deserialize<BusinessRecipientDto[]>(await response.Content.ReadAsStringAsync()) ?? new BusinessRecipientDto[0];
                    foreach (var row in recipients) row.PropertyChanged -= OnRowChanged;
                    recipients.Clear();
                    foreach (var place in payload)
                    {
                        var email = (place.Emails ?? new string[0]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                        if (string.IsNullOrWhiteSpace(email)) continue;
                        var row = new SocialRecipientRow
                        {
                            Id = place.Id ?? string.Empty,
                            Name = string.IsNullOrWhiteSpace(place.Name) ? "Sin nombre" : place.Name,
                            Email = email,
                            Location = JoinLocation(place.Locality, place.Region, place.Country),
                            Selected = false,
                        };
                        row.PropertyChanged += OnRowChanged;
                        recipients.Add(row);
                    }
                    StatusText.Text = string.Format("{0} empresas con correo disponibles para envío.", recipients.Count);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible cargar los destinatarios: " + FriendlyError(ex);
            }
            finally
            {
                loadingRecipients = false;
                busy = false;
                UpdateRecipientState();
            }
        }

        private void OnRowChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateRecipientState();
        }

        private bool MatchesRecipientSearch(object item)
        {
            var row = item as SocialRecipientRow;
            var query = RecipientSearchBox == null ? string.Empty : RecipientSearchBox.Text.Trim();
            return row != null && (string.IsNullOrWhiteSpace(query)
                || row.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || row.Email.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || row.Location.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void UpdateRecipientState()
        {
            if (RecipientCountText == null) return;
            var selected = recipients.Count(r => r.Selected);
            RecipientCountText.Text = string.Format("{0} con correo · {1} seleccionado(s)", recipients.Count, selected);
            var any = recipientsView.Cast<object>().Any();
            RecipientsEmpty.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
            RecipientsWrap.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RecipientSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchPlaceholder != null)
                SearchPlaceholder.Visibility = RecipientSearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (recipientsView == null) return;
            recipientsView.Refresh();
            UpdateRecipientState();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in recipients) row.Selected = true;
        }

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            await LoadRecipientsAsync();
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Current.Navigate(Routes.Configuracion);
        }

        // ---------------------------------------------------------------- placeholders

        private void TopicBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TopicPlaceholder != null)
                TopicPlaceholder.Visibility = TopicBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BodyBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (BodyPlaceholder != null)
                BodyPlaceholder.Visibility = BodyBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PublicImageUrlBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (UrlPlaceholder != null)
                UrlPlaceholder.Visibility = PublicImageUrlBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---------------------------------------------------------------- generar

        private void SetPreviewImage(byte[] bytes)
        {
            currentImage = bytes;
            if (bytes == null || bytes.Length == 0)
            {
                PreviewImage.Source = null;
                PreviewWrap.Visibility = Visibility.Collapsed;
                return;
            }
            using (var stream = new MemoryStream(bytes))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                PreviewImage.Source = bitmap;
            }
            PreviewWrap.Visibility = Visibility.Visible;
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            var topic = TopicBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(topic))
            {
                StatusText.Text = "Escribe el tema de la publicación.";
                return;
            }
            if (!AiService.IsConfigured)
            {
                StatusText.Text = "Configura el agente de IA (proveedor, modelo y API key) en el módulo de Configuración.";
                return;
            }

            try
            {
                SetBusy(true);
                GenerateButton.Content = "Generando…";
                StatusText.Text = "Generando el texto con el agente de IA…";
                var textPrompt = "Actúa como un community manager experto. Redacta una publicación para redes sociales en español sobre el tema: \"" + topic
                    + "\". Incluye un gancho inicial, el cuerpo con los beneficios y una llamada a la acción al final. Extensión entre 120 y 200 palabras. No uses hashtags.";
                BodyBox.Text = await AiService.GenerateTextAsync(textPrompt);

                StatusText.Text = "Generando la imagen de la publicación…";
                var imagePrompt = "Imagen publicitaria atractiva, moderna y limpia para redes sociales sobre: " + topic + ". Sin texto ni logotipos.";
                SetPreviewImage(await AiService.GenerateImageAsync(imagePrompt));

                StatusText.Text = "Contenido generado. Revísalo, ajusta el texto y publica o envía.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible generar el contenido: " + ex.Message;
            }
            finally
            {
                GenerateButton.Content = "Generar imagen y texto";
                SetBusy(false);
            }
        }

        // ---------------------------------------------------------------- publicar

        private async void Publish_Click(object sender, RoutedEventArgs e)
        {
            var text = BodyBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusText.Text = "Genera o escribe el texto antes de publicar.";
                return;
            }
            var useFb = UseFacebookBox.IsChecked == true;
            var useIg = UseInstagramBox.IsChecked == true;
            if (!useFb && !useIg)
            {
                StatusText.Text = "Marca Facebook o Instagram para publicar.";
                return;
            }

            var messages = new List<string>();
            try
            {
                SetBusy(true);
                PublishButton.Content = "Publicando…";
                if (useFb)
                {
                    StatusText.Text = "Publicando en Facebook…";
                    try
                    {
                        await PublishFacebookAsync(text, currentImage);
                        messages.Add("Facebook ✓");
                    }
                    catch (Exception ex)
                    {
                        messages.Add("Facebook ✕ " + ex.Message);
                    }
                }
                if (useIg)
                {
                    StatusText.Text = "Publicando en Instagram…";
                    try
                    {
                        var imageUrl = PublicImageUrlBox.Text.Trim();
                        if (string.IsNullOrWhiteSpace(imageUrl))
                            throw new InvalidOperationException("Instagram descarga la imagen por URL; indica la URL pública de la imagen.");
                        await PublishInstagramAsync(text, imageUrl);
                        messages.Add("Instagram ✓");
                    }
                    catch (Exception ex)
                    {
                        messages.Add("Instagram ✕ " + ex.Message);
                    }
                }
                StatusText.Text = string.Join("   ·   ", messages);
            }
            finally
            {
                PublishButton.Content = "Publicar";
                SetBusy(false);
            }
        }

        // ---------------------------------------------------------------- correo

        private async void Email_Click(object sender, RoutedEventArgs e)
        {
            if (UseEmailBox.IsChecked != true)
            {
                StatusText.Text = "Marca «Correo electrónico» para enviar.";
                return;
            }
            var text = BodyBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusText.Text = "Genera o escribe el texto antes de enviar.";
                return;
            }
            var selected = recipients.Where(r => r.Selected).ToList();
            if (selected.Count == 0)
            {
                StatusText.Text = "Selecciona al menos un destinatario en la lista.";
                return;
            }
            var smtp = CurrentSmtp();
            if (string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.Username) || string.IsNullOrWhiteSpace(smtp.Password))
            {
                StatusText.Text = "Configura la cuenta de correo en el módulo de Configuración.";
                return;
            }

            try
            {
                SetBusy(true);
                EmailButton.Content = "Enviando…";
                StatusText.Text = ApiClientFactory.IsWaking
                    ? ApiClientFactory.WakingMessage
                    : string.Format("Enviando a {0} empresa(s)…", selected.Count);
                var subject = TopicBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(subject)) subject = "Novedades";
                var request = Json.Serialize(new SendEmailRequestDto
                {
                    Smtp = smtp,
                    Recipients = selected.Select(r => new EmailRecipientDto
                    {
                        BusinessId = r.Id,
                        BusinessName = r.Name,
                        ToEmail = r.Email,
                    }).ToArray(),
                    Subject = subject,
                    Body = text,
                });
                using (var response = await ApiClient.PostAsync("api/crm/send", new StringContent(request, Encoding.UTF8, "application/json")))
                {
                    response.EnsureSuccessStatusCode();
                    var result = Json.Deserialize<EmailSendResponseDto>(await response.Content.ReadAsStringAsync());
                    StatusText.Text = result == null
                        ? "La API no devolvió un resultado de envío."
                        : string.Format("Correo · {0} enviado(s), {1} fallido(s).", result.SentCount, result.FailedCount);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible enviar los correos: " + FriendlyError(ex);
            }
            finally
            {
                EmailButton.Content = "Enviar por correo";
                SetBusy(false);
            }
        }

        private static SmtpConfigDto CurrentSmtp()
        {
            int port;
            if (!int.TryParse(AppConfig.Get("CrmSmtpPort"), out port) || port == 0) port = 587;
            return new SmtpConfigDto
            {
                Host = GetSetting("CrmSmtpHost"),
                Port = port,
                Username = GetSetting("CrmSmtpUser"),
                Password = GetSetting("CrmSmtpPassword"),
                FromName = GetSetting("CrmSmtpFromName"),
                EnableSsl = !string.Equals(AppConfig.Get("CrmSmtpUseSsl"), "False", StringComparison.OrdinalIgnoreCase),
            };
        }

        // ---------------------------------------------------------------- Graph API

        private static async Task PublishFacebookAsync(string text, byte[] image)
        {
            var pageId = GetSetting("FacebookPageId");
            var token = GetSetting("FacebookAccessToken");
            if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("configura la Página y el token de Facebook en Configuración.");

            var endpoint = "https://graph.facebook.com/v26.0/" + pageId;
            if (image == null || image.Length == 0)
            {
                using (var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "message", text },
                    { "access_token", token },
                }))
                using (var response = await SocialClient.PostAsync(endpoint + "/feed", content))
                {
                    await EnsureGraphSuccessAsync(response, "Facebook");
                }
                return;
            }

            using (var form = new MultipartFormDataContent())
            {
                var file = new ByteArrayContent(image);
                file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                form.Add(file, "source", "publicacion.png");
                form.Add(new StringContent(text), "message");
                form.Add(new StringContent(token), "access_token");
                using (var photoResponse = await SocialClient.PostAsync(endpoint + "/photos", form))
                {
                    await EnsureGraphSuccessAsync(photoResponse, "Facebook");
                }
            }
        }

        private static async Task PublishInstagramAsync(string caption, string imageUrl)
        {
            var igId = GetSetting("InstagramBusinessAccountId");
            var token = GetSetting("InstagramAccessToken");
            if (string.IsNullOrWhiteSpace(igId) || string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("configura la cuenta y el token de Instagram en Configuración.");

            var host = "https://graph.facebook.com/v26.0/" + igId;
            var container = await PostGraphFormAsync(host + "/media", new Dictionary<string, string>
            {
                { "image_url", imageUrl },
                { "caption", caption },
                { "access_token", token },
            }, "Instagram");

            var creationId = container != null && container.ContainsKey("id") ? Convert.ToString(container["id"]) : null;
            if (string.IsNullOrWhiteSpace(creationId))
                throw new InvalidOperationException("Instagram no devolvió el contenedor de la publicación.");

            await PostGraphFormAsync(host + "/media_publish", new Dictionary<string, string>
            {
                { "creation_id", creationId },
                { "access_token", token },
            }, "Instagram");
        }

        private static async Task<IDictionary<string, object>> PostGraphFormAsync(string endpoint, Dictionary<string, string> fields, string label)
        {
            using (var content = new FormUrlEncodedContent(fields))
            using (var response = await SocialClient.PostAsync(endpoint, content))
            {
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(label + ": " + (int)response.StatusCode + " " + AiService.Truncate(body));
                return Json.DeserializeObject(body) as IDictionary<string, object>;
            }
        }

        private static async Task EnsureGraphSuccessAsync(HttpResponseMessage response, string label)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(label + ": " + (int)response.StatusCode + " " + AiService.Truncate(body));
        }

        // ---------------------------------------------------------------- utilidades

        private static string FriendlyError(Exception ex)
        {
            if (ex is TaskCanceledException) return "la API tardó demasiado en responder. Inténtalo de nuevo.";
            if (ex is HttpRequestException) return "no se pudo conectar con el servidor. Revisa tu conexión.";
            return ex.Message;
        }

        private static string JoinLocation(string locality, string region, string country)
        {
            return string.Join(", ", new[] { locality, region, country }.Where(v => !string.IsNullOrWhiteSpace(v)));
        }
    }
}
