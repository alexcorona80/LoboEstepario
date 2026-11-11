using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace LoboEstepario.Pages
{
    /// <summary>CRM: campañas de prospección, plantillas, rondas y reglas anti-spam (equivale a Crm.razor).</summary>
    public partial class CrmPage : UserControl, IPage
    {
        private readonly ObservableCollection<RecipientRow> recipients = new ObservableCollection<RecipientRow>();
        private readonly ObservableCollection<CrmHistoryRow> history = new ObservableCollection<CrmHistoryRow>();
        private readonly ICollectionView recipientsView;
        private List<EmailTemplateDto> templates = new List<EmailTemplateDto>();

        private bool ready;
        private bool initialized;
        private bool busy;
        private bool suppressSelection;
        private bool suppressTemplate;
        private bool suppressCategory;
        private bool categoriesLoaded;
        private bool historyLoaded;
        private string pendingCompany;
        private string selectedTemplateId = string.Empty;

        public CrmPage()
        {
            InitializeComponent();

            recipientsView = CollectionViewSource.GetDefaultView(recipients);
            recipientsView.Filter = MatchesFilter;
            RecipientsGrid.ItemsSource = recipientsView;
            HistoryGrid.ItemsSource = history;

            SetCategories(new List<string>());
            SetTemplates();
            ready = true;

            ApiClientFactory.WakingChanged += OnWakingChanged;
            UpdateRuleLabels();
            RefreshSmtpWarning();
            UpdateCounters();
            SetHistoryState(null);
        }

        // ------------------------------------------------------------------ navegación

        public async Task OnNavigatedToAsync(object parameter)
        {
            var company = parameter as string;
            if (!string.IsNullOrWhiteSpace(company))
            {
                pendingCompany = company.Trim();
                SearchBox.Text = pendingCompany; // precarga la búsqueda de destinatarios
            }

            RefreshSmtpWarning();

            if (!initialized)
            {
                initialized = true;
                await LoadTemplatesAsync();
            }
            // Las categorías se reintentan en cada visita mientras no se hayan podido cargar
            // (p. ej. si la API de Render estaba despertando y respondió con un error).
            if (!categoriesLoaded) await LoadCategoriesAsync();
            await RefreshAsync();
            // Respaldo: si el endpoint no devolvió categorías, se derivan de los destinatarios ya cargados.
            if (!categoriesLoaded) SetCategoriesFromRecipients();
        }

        // ------------------------------------------------------------------ estado "Despertando servidor"

        private void OnWakingChanged(bool waking)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                WakingText.Text = ApiClientFactory.WakingMessage;
                WakingBanner.Visibility = waking ? Visibility.Visible : Visibility.Collapsed;
            }));
        }

        // ------------------------------------------------------------------ SMTP

        private static string Setting(string key)
        {
            var value = AppConfig.Get(key);
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }

        private static bool SmtpConfigured
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Setting("CrmSmtpHost"))
                    && !string.IsNullOrWhiteSpace(Setting("CrmSmtpUser"))
                    && !string.IsNullOrWhiteSpace(Setting("CrmSmtpPassword"));
            }
        }

        private static SmtpConfigDto CurrentSmtp()
        {
            int port;
            if (!int.TryParse(Setting("CrmSmtpPort"), out port) || port == 0) port = 587;
            return new SmtpConfigDto
            {
                Host = Setting("CrmSmtpHost"),
                Port = port,
                Username = Setting("CrmSmtpUser"),
                Password = Setting("CrmSmtpPassword"),
                FromName = Setting("CrmSmtpFromName"),
                EnableSsl = !string.Equals(AppConfig.Get("CrmSmtpUseSsl"), "False", StringComparison.OrdinalIgnoreCase),
            };
        }

        private void RefreshSmtpWarning()
        {
            SmtpWarning.Visibility = SmtpConfigured ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ConfigLink_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Current != null) MainWindow.Current.Navigate(Routes.Configuracion);
        }

        // ------------------------------------------------------------------ HTTP

        private static async Task<T> GetAsync<T>(string url)
        {
            using (var response = await ApiClientFactory.Client.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();
                return Json.Deserialize<T>(await response.Content.ReadAsStringAsync());
            }
        }

        private static async Task<T> PostAsync<T>(string url, object body)
        {
            using (var content = new StringContent(Json.Serialize(body), Encoding.UTF8, "application/json"))
            using (var response = await ApiClientFactory.Client.PostAsync(url, content))
            {
                response.EnsureSuccessStatusCode();
                return Json.Deserialize<T>(await response.Content.ReadAsStringAsync());
            }
        }

        private static string Friendly(Exception ex)
        {
            if (ex is TaskCanceledException || ex is TimeoutException)
                return "el servidor tardó demasiado en responder. Inténtalo de nuevo en unos segundos.";
            var http = ex as HttpRequestException;
            if (http != null)
            {
                if (ex.Message.IndexOf("status code", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "el servidor respondió con un error (" + ex.Message + ").";
                return "no se pudo conectar con el servidor. Revisa tu conexión a internet.";
            }
            return ex.Message;
        }

        // ------------------------------------------------------------------ carga de datos

        private async Task LoadCategoriesAsync()
        {
            const int attempts = 3;
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    var list = await GetAsync<List<string>>("api/business/categories");
                    SetCategories(list ?? new List<string>());
                    categoriesLoaded = list != null && list.Any(c => !string.IsNullOrWhiteSpace(c));
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Categorías (intento " + attempt + "): " + ex.Message);
                    if (attempt < attempts) await Task.Delay(TimeSpan.FromSeconds(3));
                }
            }
            SetCategories(new List<string>());
        }

        private void SetCategoriesFromRecipients()
        {
            var derived = recipients
                .Select(r => r.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();
            if (derived.Count == 0) return;
            SetCategories(derived);
            categoriesLoaded = true;
        }

        private void SetCategories(List<string> categories)
        {
            var current = SelectedCategory;
            var options = new List<CrmOption> { new CrmOption { Value = string.Empty, Text = "-- Todas las categorías --" } };
            options.AddRange(categories.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => new CrmOption { Value = c, Text = c }));
            suppressCategory = true;
            CategoryCombo.ItemsSource = options;
            CategoryCombo.SelectedValue = options.Any(o => o.Value == current) ? current : string.Empty;
            suppressCategory = false;
        }

        private string SelectedCategory
        {
            get { return CategoryCombo == null ? string.Empty : ((CategoryCombo.SelectedValue as string) ?? string.Empty); }
        }

        private async Task LoadTemplatesAsync()
        {
            try
            {
                templates = await GetAsync<List<EmailTemplateDto>>("api/crm/templates") ?? new List<EmailTemplateDto>();
                if (string.IsNullOrWhiteSpace(SubjectBox.Text) && templates.Count > 0)
                {
                    var first = templates.FirstOrDefault(t => t.IsDefault) ?? templates[0];
                    selectedTemplateId = first.Id ?? string.Empty;
                    SubjectBox.Text = first.Subject ?? string.Empty;
                    BodyBox.Text = first.Body ?? string.Empty;
                }
            }
            catch (Exception)
            {
                templates = new List<EmailTemplateDto>();
            }
            SetTemplates();
        }

        private void SetTemplates()
        {
            var options = new List<CrmOption> { new CrmOption { Value = string.Empty, Text = "-- Redacción libre o elegir plantilla --" } };
            options.AddRange(templates.Select(t => new CrmOption
            {
                Value = t.Id ?? string.Empty,
                Text = t.IsDefault ? t.Name + " (Predeterminada)" : t.Name,
            }));
            suppressTemplate = true;
            TemplateCombo.ItemsSource = options;
            TemplateCombo.SelectedValue = options.Any(o => o.Value == selectedTemplateId) ? selectedTemplateId : string.Empty;
            if (!options.Any(o => o.Value == selectedTemplateId)) selectedTemplateId = string.Empty;
            suppressTemplate = false;
            UpdateTemplateButtons();
        }

        private void UpdateTemplateButtons()
        {
            var has = !string.IsNullOrWhiteSpace(selectedTemplateId);
            DeleteTemplateButton.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            UpdateTemplateButton.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task RefreshAsync()
        {
            if (busy) return;
            SetBusy(true);
            try
            {
                SetStatus("Cargando destinatarios e historial…");
                var data = await GetAsync<List<CrmRecipientDto>>("api/crm/recipients?limit=2000") ?? new List<CrmRecipientDto>();

                var rows = data
                    .Where(r => r.Emails != null && !string.IsNullOrWhiteSpace(r.Emails.FirstOrDefault()))
                    .Select(r => new RecipientRow
                    {
                        Id = r.Id ?? string.Empty,
                        Name = string.IsNullOrWhiteSpace(r.Name) ? "Sin nombre" : r.Name,
                        Email = r.Emails.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty,
                        Category = r.EffectiveCategory,
                        Location = JoinLocation(r.Locality, r.Region, r.Country),
                        LastContactedAtUtc = r.LastContactedAtUtc,
                        ContactCount = r.ContactCount,
                        LastCampaignRound = r.LastCampaignRound,
                        Selected = false,
                    }).ToList();

                suppressSelection = true;
                foreach (var old in recipients) old.PropertyChanged -= Row_PropertyChanged;
                recipients.Clear();
                foreach (var row in rows)
                {
                    row.PropertyChanged += Row_PropertyChanged;
                    recipients.Add(row);
                }
                suppressSelection = false;
                RecomputeStatuses();

                var historyData = await GetAsync<List<CrmHistoryDto>>("api/crm/history?limit=100") ?? new List<CrmHistoryDto>();
                SetHistory(historyData);

                var stats = await GetAsync<EmailStatsDto>("api/crm/stats");
                if (stats != null)
                {
                    SentStat.Text = stats.SentCount.ToString("N0");
                    FailedStat.Text = stats.FailedCount.ToString("N0");
                    WithEmailStat.Text = stats.BusinessesWithEmailCount.ToString("N0");
                    StatsGrid.Visibility = Visibility.Visible;
                }

                SelectEligibleOnly();
                if (!string.IsNullOrEmpty(pendingCompany))
                {
                    // Llegamos desde Negocios: dejar marcada solo la empresa pedida.
                    var name = pendingCompany;
                    pendingCompany = null;
                    var exact = recipientsView.Cast<RecipientRow>()
                        .Where(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (exact.Count > 0)
                    {
                        suppressSelection = true;
                        foreach (var r in recipientsView.Cast<RecipientRow>()) r.Selected = exact.Contains(r);
                        suppressSelection = false;
                    }
                }
                UpdateCounters();
                SetStatus(recipients.Count + " empresas con correo registradas en MongoDB.");
            }
            catch (Exception ex)
            {
                if (!historyLoaded) SetHistoryError();
                SetStatus("No fue posible cargar el CRM: " + Friendly(ex));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetHistory(List<CrmHistoryDto> items)
        {
            history.Clear();
            foreach (var h in items)
            {
                var sent = h.SentAtUtc;
                string when = "—";
                if (sent.HasValue)
                {
                    var utc = sent.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(sent.Value, DateTimeKind.Utc) : sent.Value;
                    when = utc.ToLocalTime().ToString("dd/MM HH:mm");
                }
                history.Add(new CrmHistoryRow
                {
                    BusinessName = h.BusinessName ?? string.Empty,
                    ToEmail = h.ToEmail ?? string.Empty,
                    Subject = h.Subject ?? string.Empty,
                    Round = string.IsNullOrWhiteSpace(h.CampaignRound) ? "General" : h.CampaignRound,
                    SentAt = when,
                    Failed = h.Status == 1,
                    StatusText = h.Status == 1 ? "FALLIDO" : "ENVIADO",
                    Error = h.Error,
                });
            }
            historyLoaded = true;
            SetHistoryState(history.Count == 0 ? "empty" : "data");
        }

        private void SetHistoryError()
        {
            SetHistoryState("error");
        }

        /// <summary>null = cargando, "empty", "error" o "data".</summary>
        private void SetHistoryState(string state)
        {
            if (state == "data")
            {
                HistoryEmpty.Visibility = Visibility.Collapsed;
                HistoryWrap.Visibility = Visibility.Visible;
                return;
            }
            HistoryWrap.Visibility = Visibility.Collapsed;
            HistoryEmpty.Visibility = Visibility.Visible;
            if (state == "empty")
            {
                HistoryEmptyIcon.Text = "☰";
                HistoryEmptyTitle.Text = "Sin envíos";
                HistoryEmptyText.Text = "El historial aparecerá aquí cuando envíes la primera campaña.";
            }
            else if (state == "error")
            {
                HistoryEmptyIcon.Text = "!";
                HistoryEmptyTitle.Text = "No fue posible cargar el historial";
                HistoryEmptyText.Text = "Pulsa «Recargar» para intentarlo de nuevo.";
            }
            else
            {
                HistoryEmptyIcon.Text = "⟳";
                HistoryEmptyTitle.Text = "Cargando historial…";
                HistoryEmptyText.Text = string.Empty;
            }
        }

        private static string JoinLocation(string locality, string region, string country)
        {
            return string.Join(", ", new[] { locality, region, country }.Where(v => !string.IsNullOrWhiteSpace(v)));
        }

        // ------------------------------------------------------------------ filtro, reglas y selección

        private int CooldownDays
        {
            get
            {
                int days;
                if (!int.TryParse((CooldownBox == null ? string.Empty : CooldownBox.Text).Trim(), out days)) return 0;
                return Math.Max(0, Math.Min(365, days));
            }
        }

        private string CampaignRound { get { return RoundBox == null ? string.Empty : RoundBox.Text; } }
        private bool ExcludeContacted { get { return ExcludeRoundCheck != null && ExcludeRoundCheck.IsChecked == true; } }
        private bool EnforceCooldown { get { return CooldownCheck != null && CooldownCheck.IsChecked == true; } }

        private bool IsEligible(RecipientRow r)
        {
            if (ExcludeContacted && r.IsContactedInRound(CampaignRound)) return false;
            if (EnforceCooldown && r.IsInCooldown(CooldownDays)) return false;
            return true;
        }

        private bool MatchesFilter(object item)
        {
            var r = item as RecipientRow;
            if (r == null) return false;
            var category = SelectedCategory;
            if (!string.IsNullOrWhiteSpace(category) && !string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase))
                return false;
            var q = SearchBox == null ? string.Empty : SearchBox.Text.Trim();
            if (q.Length == 0) return true;
            return Contains(r.Name, q) || Contains(r.Email, q) || Contains(r.Category, q) || Contains(r.Location, q);
        }

        private static bool Contains(string value, string q)
        {
            return value != null && value.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private List<RecipientRow> Filtered()
        {
            return recipientsView.Cast<RecipientRow>().ToList();
        }

        private void RecomputeStatuses()
        {
            var round = CampaignRound;
            var days = CooldownDays;
            var enforce = EnforceCooldown;
            foreach (var r in recipients)
            {
                if (r.IsContactedInRound(round))
                {
                    r.StatusKind = "round";
                    r.StatusLabel = "Ronda actual (" + r.LastCampaignRound + ")";
                    r.StatusTip = "Ya fue contactado en " + r.LastCampaignRound;
                }
                else if (enforce && r.IsInCooldown(days))
                {
                    var daysAgo = (int)(DateTime.UtcNow - r.LastContactedAtUtc.Value).TotalDays;
                    r.StatusKind = "cooldown";
                    r.StatusLabel = "Enfriamiento (" + daysAgo + " d)";
                    r.StatusTip = "Último envío hace " + daysAgo + " día(s)";
                }
                else if (r.ContactCount > 0)
                {
                    r.StatusKind = "previous";
                    r.StatusLabel = "Elegible (previo: " + (r.LastCampaignRound ?? "General") + ")";
                    r.StatusTip = "Total contactos: " + r.ContactCount + ". Ronda previa: " + r.LastCampaignRound;
                }
                else
                {
                    r.StatusKind = "new";
                    r.StatusLabel = "Elegible (nuevo)";
                    r.StatusTip = string.Empty;
                }
                r.RowOpacity = IsEligible(r) ? 1.0 : 0.65;
            }
        }

        private void UpdateRuleLabels()
        {
            ExcludeRoundText.Text = "Excluir empresas ya contactadas en esta ronda (" + CampaignRound + ")";
            CooldownText.Text = "No enviar si fue contactada hace menos de " + CooldownDays + " días";
        }

        private void UpdateCounters()
        {
            if (!ready) return;
            var filtered = Filtered();
            var selected = filtered.Count(r => r.Selected);
            var eligible = filtered.Count(IsEligible);

            FilteredCountRun.Text = filtered.Count.ToString();
            SelectedCountRun.Text = selected.ToString();
            EligibleButton.Content = "Seleccionar elegibles (" + eligible + ")";
            AllButton.IsEnabled = recipients.Count > 0;
            SendButton.Content = busy ? "Enviando…" : "Enviar a " + selected + " destinatario(s)";
            SendButton.IsEnabled = !busy && selected > 0;

            if (filtered.Count == 0)
            {
                RecipientsWrap.Visibility = Visibility.Collapsed;
                RecipientsEmpty.Visibility = Visibility.Visible;
                if (busy && recipients.Count == 0)
                {
                    RecipientsEmptyIcon.Text = "⟳";
                    RecipientsEmptyTitle.Text = "Cargando destinatarios…";
                    RecipientsEmptyText.Text = string.Empty;
                }
                else
                {
                    RecipientsEmptyIcon.Text = "✉";
                    RecipientsEmptyTitle.Text = "Sin destinatarios para este filtro";
                    RecipientsEmptyText.Text = "Ajusta el filtro de categoría o busca más negocios en «Buscar negocios».";
                }
            }
            else
            {
                RecipientsEmpty.Visibility = Visibility.Collapsed;
                RecipientsWrap.Visibility = Visibility.Visible;
            }
        }

        private void SetBusy(bool value)
        {
            busy = value;
            RefreshButton.IsEnabled = !value;
            SaveNewTemplateButton.IsEnabled = !value;
            UpdateTemplateButton.IsEnabled = !value;
            UpdateCounters();
        }

        private void SetStatus(string text)
        {
            StatusText.Text = text;
        }

        private void Row_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Selected" && !suppressSelection) UpdateCounters();
        }

        private void SelectEligibleOnly()
        {
            suppressSelection = true;
            foreach (var r in Filtered()) r.Selected = IsEligible(r);
            suppressSelection = false;
            UpdateCounters();
        }

        private void SetSelectionForFiltered(bool value)
        {
            suppressSelection = true;
            foreach (var r in Filtered()) r.Selected = value;
            suppressSelection = false;
            UpdateCounters();
        }

        private void SelectEligible_Click(object sender, RoutedEventArgs e) { SelectEligibleOnly(); }
        private void SelectAll_Click(object sender, RoutedEventArgs e) { SetSelectionForFiltered(true); }
        private void DeselectAll_Click(object sender, RoutedEventArgs e) { SetSelectionForFiltered(false); }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!ready) return;
            recipientsView.Refresh();
            UpdateCounters();
        }

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ready || suppressCategory) return;
            recipientsView.Refresh();
            SelectEligibleOnly();
        }

        private void Rules_Changed(object sender, RoutedEventArgs e)
        {
            if (!ready) return;
            UpdateRuleLabels();
            RecomputeStatuses();
            UpdateCounters();
        }

        private void Digits_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(c => !char.IsDigit(c));
        }

        // ------------------------------------------------------------------ plantillas

        private void BodyBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (BodyPlaceholder == null) return;
            BodyPlaceholder.Visibility = string.IsNullOrEmpty(BodyBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ready || suppressTemplate) return;
            selectedTemplateId = (TemplateCombo.SelectedValue as string) ?? string.Empty;
            UpdateTemplateButtons();
            if (string.IsNullOrWhiteSpace(selectedTemplateId)) return;
            var t = templates.FirstOrDefault(x => x.Id == selectedTemplateId);
            if (t != null)
            {
                SubjectBox.Text = t.Subject ?? string.Empty;
                BodyBox.Text = t.Body ?? string.Empty;
                SetStatus("Plantilla «" + t.Name + "» cargada.");
            }
        }

        private void ShowSaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            SaveTemplateButtons.Visibility = Visibility.Collapsed;
            SaveTemplateForm.Visibility = Visibility.Visible;
            NewTemplateName.Focus();
        }

        private void CancelSaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            SaveTemplateForm.Visibility = Visibility.Collapsed;
            SaveTemplateButtons.Visibility = Visibility.Visible;
        }

        private async void SaveNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewTemplateName.Text))
            {
                SetStatus("Asigna un nombre a la plantilla.");
                return;
            }

            SetBusy(true);
            try
            {
                var saved = await PostAsync<EmailTemplateDto>("api/crm/templates", new EmailTemplateDto
                {
                    Name = NewTemplateName.Text.Trim(),
                    Subject = SubjectBox.Text.Trim(),
                    Body = BodyBox.Text,
                    IsDefault = false,
                });
                if (saved != null)
                {
                    selectedTemplateId = saved.Id ?? string.Empty;
                    await LoadTemplatesAsync();
                    NewTemplateName.Text = string.Empty;
                    CancelSaveTemplate_Click(null, null);
                    SetStatus("Plantilla «" + saved.Name + "» guardada exitosamente en MongoDB.");
                }
            }
            catch (Exception ex)
            {
                SetStatus("No fue posible guardar la plantilla: " + Friendly(ex));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void UpdateTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(selectedTemplateId)) return;
            var t = templates.FirstOrDefault(x => x.Id == selectedTemplateId);
            if (t == null) return;

            SetBusy(true);
            try
            {
                t.Subject = SubjectBox.Text.Trim();
                t.Body = BodyBox.Text;
                var updated = await PostAsync<EmailTemplateDto>("api/crm/templates", t);
                if (updated != null) SetStatus("Plantilla «" + updated.Name + "» actualizada en MongoDB.");
            }
            catch (Exception ex)
            {
                SetStatus("No fue posible actualizar la plantilla: " + Friendly(ex));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void DeleteTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(selectedTemplateId)) return;
            var t = templates.FirstOrDefault(x => x.Id == selectedTemplateId);
            if (t == null) return;

            var answer = MessageBox.Show(Window.GetWindow(this),
                "¿Eliminar la plantilla «" + t.Name + "» de MongoDB?", "Eliminar plantilla",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            SetBusy(true);
            try
            {
                using (var response = await ApiClientFactory.Client.DeleteAsync("api/crm/templates/" + Uri.EscapeDataString(selectedTemplateId)))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        SetStatus("Plantilla «" + t.Name + "» eliminada de MongoDB.");
                        selectedTemplateId = string.Empty;
                        await LoadTemplatesAsync();
                    }
                    else
                    {
                        SetStatus("No fue posible eliminar la plantilla: el servidor respondió " + (int)response.StatusCode + ".");
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatus("No fue posible eliminar la plantilla: " + Friendly(ex));
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ------------------------------------------------------------------ envío

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            var selected = Filtered().Where(r => r.Selected).ToList();
            if (selected.Count == 0)
            {
                SetStatus("Selecciona al menos un destinatario.");
                return;
            }
            if (!SmtpConfigured)
            {
                SetStatus("Completa la cuenta de correo en el módulo de Configuración antes de enviar.");
                return;
            }
            if (string.IsNullOrWhiteSpace(BodyBox.Text))
            {
                SetStatus("Escribe el cuerpo del correo.");
                return;
            }

            var round = CampaignRound.Trim();
            var confirm = MessageBox.Show(Window.GetWindow(this),
                "Se enviará «" + SubjectBox.Text.Trim() + "» (" + (round.Length == 0 ? "General" : round) + ") a "
                    + selected.Count + " destinatario(s).\n\n¿Continuar?",
                "Confirmar envío", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            SetBusy(true);
            try
            {
                SetStatus("Enviando " + CampaignRound + " a " + selected.Count + " empresa(s)…");
                var result = await PostAsync<EmailSendResponseDto>("api/crm/send", new CrmSendRequestDto
                {
                    Smtp = CurrentSmtp(),
                    CampaignRound = round,
                    Recipients = selected.Select(r => new CrmEmailRecipientDto
                    {
                        BusinessId = r.Id,
                        BusinessName = r.Name,
                        Category = r.Category,
                        Location = r.Location,
                        ToEmail = r.Email,
                    }).ToArray(),
                    Subject = SubjectBox.Text.Trim(),
                    Body = BodyBox.Text,
                });

                SetStatus(result == null
                    ? "La API no devolvió un resultado de envío."
                    : "Envío completo de " + CampaignRound + " · " + result.SentCount + " enviado(s), " + result.FailedCount + " fallido(s).");

                SetBusy(false);
                var summary = StatusText.Text;
                await RefreshAsync();
                if (result != null) SetStatus(summary);
            }
            catch (Exception ex)
            {
                SetStatus("No fue posible enviar los correos: " + Friendly(ex));
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ------------------------------------------------------------------ scroll anidado

        /// <summary>Si el DataGrid no puede desplazarse más, el rodillo pasa al ScrollViewer de la página.</summary>
        private void Grid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var grid = sender as FrameworkElement;
            if (grid == null || e.Handled) return;
            var viewer = FindChild<ScrollViewer>(grid);
            var canScroll = viewer != null && viewer.ScrollableHeight > 0
                && ((e.Delta > 0 && viewer.VerticalOffset > 0) || (e.Delta < 0 && viewer.VerticalOffset < viewer.ScrollableHeight));
            if (canScroll) return;

            e.Handled = true;
            var parent = grid.Parent as UIElement;
            if (parent == null) return;
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender,
            });
        }

        private static T FindChild<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var match = child as T ?? FindChild<T>(child);
                if (match != null) return match;
            }
            return null;
        }
    }
}
