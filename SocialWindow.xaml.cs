using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace LoboEstepario
{
    public partial class SocialWindow : Window
    {
        private static readonly HttpClient ApiClient = ApiClientFactory.Client;
        private static readonly HttpClient SocialClient = CreateSocialClient();
        private readonly ICollectionView recipientsView;
        private byte[] currentImage;

        public ObservableCollection<RecipientRow> Recipients { get; } = new ObservableCollection<RecipientRow>();

        public SocialWindow()
        {
            InitializeComponent();
            recipientsView = CollectionViewSource.GetDefaultView(Recipients);
            recipientsView.Filter = MatchesRecipientSearch;
            RecipientsGrid.ItemsSource = recipientsView;

            Loaded += async (sender, args) => await LoadRecipientsAsync();
        }


        private static HttpClient CreateSocialClient()
        {
            
            return new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        }

        private static string GetSetting(string key)
        {
            var value = AppConfig.Get(key);
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private async Task LoadRecipientsAsync()
        {
            try
            {
                StatusText.Text = "Cargando destinatarios desde la base de datos…";
                var response = await ApiClient.GetAsync("api/crm/recipients?limit=2000");
                response.EnsureSuccessStatusCode();
                var payload = Json.Deserialize<BusinessRecipientDto[]>(await response.Content.ReadAsStringAsync()) ?? new BusinessRecipientDto[0];
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
                recipientsView.Refresh();
                UpdateRecipientCounters();
                StatusText.Text = string.Format("{0} empresas con correo disponibles para envío.", Recipients.Count);
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible cargar los destinatarios: " + ex.Message;
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
            foreach (var row in Recipients)
            {
                row.Selected = true;
            }
            UpdateRecipientCounters();
        }

        private void UpdateRecipientCounters()
        {
            var selected = Recipients.Count(row => row.Selected);
            if (RecipientCount != null)
                RecipientCount.Text = selected == 0 ? "ninguno" : string.Format("{0} seleccionado{1}", selected, selected == 1 ? string.Empty : "s");
        }

        private void SetPreviewImage(byte[] bytes)
        {
            currentImage = bytes;
            if (bytes == null || bytes.Length == 0)
            {
                PreviewImage.Source = null;
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
                DisableActions(true);
                StatusText.Text = "Generando el texto con el agente de IA…";
                var textPrompt = "Actúa como un community manager experto. Redacta una publicación para redes sociales en español sobre el tema: \"" + topic
                    + "\". Incluye un gancho inicial, el cuerpo con los beneficios y una llamada a la acción al final. Extensión entre 120 y 200 palabras. No uses hashtags.";
                ContentBodyBox.Text = await AiService.GenerateTextAsync(textPrompt);

                StatusText.Text = "Generando la imagen de la publicación…";
                var imagePrompt = "Imagen publicitaria atractiva, moderna y limpia para redes sociales sobre: " + topic + ". Sin texto ni logotipos.";
                var bytes = await AiService.GenerateImageAsync(imagePrompt);
                SetPreviewImage(bytes);

                StatusText.Text = "Contenido generado. Revísalo, ajusta el texto y publica o envía.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible generar el contenido: " + ex.Message;
            }
            finally
            {
                DisableActions(false);
            }
        }

        private async void Publish_Click(object sender, RoutedEventArgs e)
        {
            var text = ContentBodyBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusText.Text = "Genera o escribe el texto antes de publicar.";
                return;
            }
            if (!(UseFacebookBox.IsChecked ?? false) && !(UseInstagramBox.IsChecked ?? false))
            {
                StatusText.Text = "Marca Facebook o Instagram para publicar.";
                return;
            }

            var messages = new List<string>();
            try
            {
                DisableActions(true);
                if (UseFacebookBox.IsChecked ?? false)
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
                if (UseInstagramBox.IsChecked ?? false)
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
                DisableActions(false);
            }
        }

        private async void Email_Click(object sender, RoutedEventArgs e)
        {
            if (!(UseEmailBox.IsChecked ?? false))
            {
                StatusText.Text = "Marca «Correo electrónico» para enviar.";
                return;
            }
            var text = ContentBodyBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusText.Text = "Genera o escribe el texto antes de enviar.";
                return;
            }
            var selected = Recipients.Where(row => row.Selected).ToList();
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
                DisableActions(true);
                StatusText.Text = string.Format("Enviando a {0} empresa{1}…", selected.Count, selected.Count == 1 ? string.Empty : "s");
                var subject = TopicBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(subject)) subject = "Novedades";
                var request = Json.Serialize(new SendEmailRequestDto
                {
                    Smtp = smtp,
                    Recipients = selected.Select(row => new EmailRecipientDto
                    {
                        BusinessId = row.Id,
                        BusinessName = row.Name,
                        ToEmail = row.Email,
                    }).ToArray(),
                    Subject = subject,
                    Body = text,
                });
                var response = await ApiClient.PostAsync("api/crm/send", new StringContent(request, Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var result = Json.Deserialize<EmailSendResponseDto>(await response.Content.ReadAsStringAsync());
                if (result != null)
                {
                    StatusText.Text = string.Format("Correo · {0} enviado{1}, {2} fallido{3}.",
                        result.SentCount, result.SentCount == 1 ? string.Empty : "s",
                        result.FailedCount, result.FailedCount == 1 ? string.Empty : "s");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible enviar los correos: " + ex.Message;
            }
            finally
            {
                DisableActions(false);
            }
        }

        private void DisableActions(bool busy)
        {
            GenerateButton.IsEnabled = !busy;
            PublishButton.IsEnabled = !busy;
            EmailButton.IsEnabled = !busy;
        }

        private static SmtpConfigDto CurrentSmtp()
        {
            int port = 587;
            int.TryParse(AppConfig.Get("CrmSmtpPort") ?? "587", out port);
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

        private static async Task PublishFacebookAsync(string text, byte[] image)
        {
            var pageId = GetSetting("FacebookPageId");
            var token = GetSetting("FacebookAccessToken");
            if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("configura la Página y el token de Facebook en Configuración.");

            var endpoint = "https://graph.facebook.com/v26.0/" + pageId;
            if (image == null || image.Length == 0)
            {
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "message", text },
                    { "access_token", token },
                });
                var response = await SocialClient.PostAsync(endpoint + "/feed", content);
                await EnsureGraphSuccessAsync(response, "Facebook");
                return;
            }

            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(image);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            form.Add(file, "source", "publicacion.png");
            form.Add(new StringContent(text), "message");
            form.Add(new StringContent(token), "access_token");
            var photoResponse = await SocialClient.PostAsync(endpoint + "/photos", form);
            await EnsureGraphSuccessAsync(photoResponse, "Facebook");
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
            using var content = new FormUrlEncodedContent(fields);
            var response = await SocialClient.PostAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(label + ": " + (int)response.StatusCode + " " + AiService.Truncate(body));
            return Json.DeserializeObject(body) as IDictionary<string, object>;
        }

        private static async Task EnsureGraphSuccessAsync(HttpResponseMessage response, string label)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(label + ": " + (int)response.StatusCode + " " + AiService.Truncate(body));
        }

        private static string FirstEmail(string[] emails)
        {
            return (emails ?? new string[0]).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string JoinLocation(string locality, string region, string country)
        {
            return string.Join(", ", new[] { locality, region, country }.Where(value => !string.IsNullOrWhiteSpace(value)));
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