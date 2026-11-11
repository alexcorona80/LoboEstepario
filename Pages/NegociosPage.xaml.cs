using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace LoboEstepario.Pages
{
    public sealed class CategoryItem
    {
        public string Slug { get; set; }
        public string Label { get; set; }
        public override string ToString() { return Label; }
    }

    public partial class NegociosPage : UserControl, IPage
    {
        private static readonly HttpClient GeocodingClient = CreateGeocodingClient();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly List<CompanyRow> companies = new List<CompanyRow>();
        private ICollectionView view;
        private string currentSource = "sin datos";
        private string actionName;
        private string statusMessage = "";
        private bool statusIsError;
        private bool syncing;
        private bool loadingCategories;
        private bool categoriesLoaded;
        private bool initialized;

        public NegociosPage()
        {
            InitializeComponent();

            CategoryBox.ItemsSource = OvertureCategories.All.Select(c => new CategoryItem { Slug = c.Slug, Label = c.Label }).ToList();
            CategoryBox.SelectedIndex = 0;
            LatBox.Text = "20.6760";
            LngBox.Text = "-103.3490";

            view = CollectionViewSource.GetDefaultView(companies);
            view.Filter = MatchesSearch;
            ResultsGrid.ItemsSource = view;

            Map.CenterChanged += OnMapCenterChanged;
            Map.SetCenter(20.6760, -103.3490);
            Map.SetRadius(2);
            ResetSavedCategories(new List<string>());
            RefreshCenterHint();
            RefreshState();
            initialized = true;
        }

        private static HttpClient CreateGeocodingClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LoboEstepario-Desktop/1.0");
            return client;
        }

        public async Task OnNavigatedToAsync(object parameter)
        {
            if (!categoriesLoaded) await LoadSavedCategoriesAsync();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApiClientFactory.WakingChanged += OnWakingChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ApiClientFactory.WakingChanged -= OnWakingChanged;
        }

        private void OnWakingChanged(bool waking)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (actionName == null) return;
                SetStatus(waking ? ApiClientFactory.WakingMessage : "Procesando…", false);
            }));
        }

        // ------------------------------------------------------------------ Estado

        private bool Busy { get { return actionName != null; } }

        private void SetStatus(string text, bool isError)
        {
            statusMessage = text;
            statusIsError = isError;
            RefreshState();
        }

        private void RefreshState()
        {
            var brush = (Brush)FindResource(statusIsError ? "EmberBrush" : "MutedBrush");
            StatusText.Text = statusMessage;
            StatusText.Foreground = brush;
            BusyLine.Text = statusMessage;
            BusyLine.Foreground = brush;
            SourceRun.Text = currentSource;

            var count = view.Cast<object>().Count();
            CountText.Text = count + " resultado(s)";

            BusyLine.Visibility = Busy ? Visibility.Visible : Visibility.Collapsed;
            EmptyPanel.Visibility = !Busy && count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TablePanel.Visibility = !Busy && count > 0 ? Visibility.Visible : Visibility.Collapsed;

            SearchButton.Content = Busy && actionName == "buscando" ? "Buscando…" : "Buscar en Overture";
            ScrapeButton.Content = Busy && actionName == "guardando" ? "Guardando…" : "Guardar en la base";
            LoadButton.Content = Busy && actionName == "cargando" ? "Cargando…" : "Cargar base de datos";
            SearchButton.IsEnabled = ScrapeButton.IsEnabled = LoadButton.IsEnabled = GoButton.IsEnabled = !Busy;
            ExportButton.IsEnabled = count > 0;
        }

        private async Task RunAsync(string name, Func<Task> work)
        {
            if (Busy) return;
            actionName = name;
            SetStatus(ApiClientFactory.IsWaking ? ApiClientFactory.WakingMessage : "Procesando…", false);
            try
            {
                await work();
            }
            catch (TaskCanceledException)
            {
                SetStatus("No fue posible completar la operación: el servidor tardó demasiado en responder. Inténtalo de nuevo.", true);
            }
            catch (HttpRequestException ex)
            {
                SetStatus("No fue posible completar la operación: no se pudo conectar con el servidor (" + FirstLine(ex.Message) + ").", true);
            }
            catch (Exception ex)
            {
                SetStatus("No fue posible completar la operación: " + FirstLine(ex.Message), true);
            }
            finally
            {
                actionName = null;
                RefreshState();
            }
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "error desconocido";
            var i = text.IndexOfAny(new[] { '\r', '\n' });
            return i > 0 ? text.Substring(0, i) : text;
        }

        // ------------------------------------------------------------------ Filtros

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!initialized) return;
            view.Refresh();
            RefreshState();
        }

        private bool MatchesSearch(object item)
        {
            var c = (CompanyRow)item;
            var query = SearchBox.Text.Trim();
            var matches = (string.IsNullOrWhiteSpace(query)
                    || Has(c.Name, query) || Has(c.Industry, query) || Has(c.CategoryTag, query) || Has(c.Location, query))
                && Contains(c.Country, CountryBox.Text)
                && Contains(c.Region, RegionBox.Text)
                && Contains(c.City, CityBox.Text)
                && (OnlyEmail.IsChecked != true || c.HasEmail)
                && (OnlyPhone.IsChecked != true || c.HasPhone);
            return matches;
        }

        private static bool Has(string value, string query)
        {
            return value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Contains(string value, string query)
        {
            return string.IsNullOrWhiteSpace(query)
                || (!string.IsNullOrWhiteSpace(value) && value.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void SetCompanies(IEnumerable<CompanyRow> rows, string source)
        {
            companies.Clear();
            companies.AddRange(rows);
            currentSource = source;
            view.Refresh();
        }

        // ------------------------------------------------------------------ Mapa y coordenadas

        private double SelectedCategoryRadiusKm { get { return RadiusMeters / 1000.0; } }

        private int RadiusMeters
        {
            get { return (int)Math.Round(RadiusSlider.Value); }
        }

        private string RadiusText
        {
            get
            {
                var m = RadiusMeters;
                if (m == 0) return "0 m";
                if (m < 1000) return m + " m";
                return (m / 1000.0).ToString("0.#", Inv) + " km";
            }
        }

        private static bool TryCoordinate(string text, out double value)
        {
            return double.TryParse((text ?? "").Trim(), NumberStyles.Float, Inv, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void RefreshCenterHint()
        {
            RadiusLabel.Text = RadiusText;
            CenterHint.Inlines.Clear();
            CenterHint.Inlines.Add("Centro actual: ");
            CenterHint.Inlines.Add(new System.Windows.Documents.Run(LatBox.Text + ", " + LngBox.Text) { FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
            CenterHint.Inlines.Add(" · cobertura " + RadiusText + ". Al guardar, los negocios se registrarán etiquetados con la categoría seleccionada.");
        }

        private void Coord_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!initialized || syncing) return;
            double lat, lng;
            if (TryCoordinate(LatBox.Text, out lat) && TryCoordinate(LngBox.Text, out lng))
                Map.SetCenter(lat, lng);
            RefreshCenterHint();
        }

        private void OnMapCenterChanged(double lat, double lng)
        {
            syncing = true;
            LatBox.Text = lat.ToString(Inv);
            LngBox.Text = lng.ToString(Inv);
            syncing = false;
            RefreshCenterHint();
            SetStatus("Centro de búsqueda actualizado en el mapa: " + LatBox.Text + ", " + LngBox.Text + ".", false);
        }

        private void RadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!initialized || syncing) return;
            syncing = true;
            RadiusBox.Text = RadiusMeters.ToString(Inv);
            syncing = false;
            Map.SetRadius(SelectedCategoryRadiusKm);
            RefreshCenterHint();
        }

        private void RadiusBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!initialized || syncing) return;
            int meters;
            if (!int.TryParse(RadiusBox.Text.Trim(), NumberStyles.Integer, Inv, out meters)) return;
            meters = Math.Max(0, Math.Min(5000, meters));
            syncing = true;
            RadiusSlider.Value = meters;
            syncing = false;
            Map.SetRadius(SelectedCategoryRadiusKm);
            RefreshCenterHint();
        }

        private bool TryBox(out double minLng, out double minLat, out double maxLng, out double maxLat)
        {
            minLng = minLat = maxLng = maxLat = 0;
            double lat, lng;
            if (!TryCoordinate(LatBox.Text, out lat) || !TryCoordinate(LngBox.Text, out lng))
            {
                SetStatus("Latitud y longitud deben ser números válidos.", true);
                return false;
            }
            var radiusKm = SelectedCategoryRadiusKm;
            var latDelta = radiusKm / 111.32;
            var lngDelta = radiusKm / (111.32 * Math.Cos(lat * Math.PI / 180));
            minLng = lng - lngDelta; minLat = lat - latDelta; maxLng = lng + lngDelta; maxLat = lat + latDelta;
            return true;
        }

        // ------------------------------------------------------------------ Geocodificación

        private void PlaceBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Geocode_Click(sender, e);
        }

        private async void Geocode_Click(object sender, RoutedEventArgs e)
        {
            var query = PlaceBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;
            await RunAsync("buscando", async () =>
            {
                var url = "https://nominatim.openstreetmap.org/search?format=json&limit=1&q=" + Uri.EscapeDataString(query);
                using (var resp = await GeocodingClient.GetAsync(url))
                {
                    resp.EnsureSuccessStatusCode();
                    var hits = Json.Deserialize<List<NominatimHit>>(await resp.Content.ReadAsStringAsync());
                    var hit = hits == null ? null : hits.FirstOrDefault();
                    double lat, lng;
                    if (hit == null)
                    {
                        SetStatus("No se encontró «" + query + "».", false);
                        return;
                    }
                    if (!TryCoordinate(hit.Lat, out lat) || !TryCoordinate(hit.Lon, out lng))
                    {
                        SetStatus("No fue posible interpretar la ubicación encontrada.", true);
                        return;
                    }
                    syncing = true;
                    LatBox.Text = lat.ToString(Inv);
                    LngBox.Text = lng.ToString(Inv);
                    syncing = false;
                    Map.Recenter(lat, lng);
                    RefreshCenterHint();
                    SetStatus("Centro de búsqueda en " + (hit.display_name ?? query) + ".", false);
                }
            });
        }

        // ------------------------------------------------------------------ API

        private string SelectedCategory
        {
            get { return CategoryBox.SelectedValue as string ?? ""; }
        }

        private string SavedCategoryFilter
        {
            get { return SavedCategoryBox.SelectedValue as string ?? ""; }
        }

        private async Task LoadSavedCategoriesAsync()
        {
            try
            {
                var list = await GetAsync<List<string>>("api/business/categories");
                ResetSavedCategories(list ?? new List<string>());
                categoriesLoaded = true;
            }
            catch (Exception)
            {
                ResetSavedCategories(new List<string>());
            }
        }

        private void ResetSavedCategories(List<string> cats)
        {
            var keep = SavedCategoryBox.SelectedValue as string ?? "";
            loadingCategories = true;
            var items = new List<CategoryItem> { new CategoryItem { Slug = "", Label = "-- Todas las categorías --" } };
            items.AddRange(cats.Select(s => new CategoryItem { Slug = s, Label = s }));
            SavedCategoryBox.ItemsSource = items;
            SavedCategoryBox.SelectedValue = items.Any(i => i.Slug == keep) ? keep : "";
            loadingCategories = false;
        }

        private async void SavedCategory_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!initialized || loadingCategories) return;
            await LoadSavedAsync();
        }

        private async void Load_Click(object sender, RoutedEventArgs e)
        {
            await LoadSavedAsync();
        }

        private async Task<T> GetAsync<T>(string url) where T : class
        {
            using (var resp = await ApiClientFactory.Client.GetAsync(url))
            {
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException("GET " + url + " → " + (int)resp.StatusCode);
                return Json.Deserialize<T>(body);
            }
        }

        private Task<List<BusinessDto>> ListBusinessesAsync(string category)
        {
            var url = "api/business?limit=1000";
            if (!string.IsNullOrWhiteSpace(category)) url += "&category=" + Uri.EscapeDataString(category.Trim());
            return GetAsync<List<BusinessDto>>(url);
        }

        private static string BoxQuery(double minLng, double minLat, double maxLng, double maxLat, string category, string name)
        {
            var q = new StringBuilder(
                "minLongitude=" + minLng.ToString(Inv) + "&minLatitude=" + minLat.ToString(Inv) +
                "&maxLongitude=" + maxLng.ToString(Inv) + "&maxLatitude=" + maxLat.ToString(Inv));
            if (!string.IsNullOrWhiteSpace(category)) q.Append("&category=" + Uri.EscapeDataString(category.Trim()));
            if (!string.IsNullOrWhiteSpace(name)) q.Append("&nameContains=" + Uri.EscapeDataString(name.Trim()));
            q.Append("&limit=100");
            return q.ToString();
        }

        private async Task LoadSavedAsync()
        {
            await RunAsync("cargando", async () =>
            {
                var filter = SavedCategoryFilter;
                var list = await ListBusinessesAsync(filter) ?? new List<BusinessDto>();
                SetCompanies(list.Select(FromBusiness), "Base de datos");
                SetStatus(list.Count + " negocios cargados desde la base de datos" +
                          (string.IsNullOrWhiteSpace(filter) ? "." : " en la categoría «" + filter + "»."), false);
                await LoadSavedCategoriesAsync();
            });
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync("buscando", async () =>
            {
                double minLng, minLat, maxLng, maxLat;
                if (!TryBox(out minLng, out minLat, out maxLng, out maxLat)) return;
                var result = await GetAsync<PlaceSearchResponseDto>("api/places?" + BoxQuery(minLng, minLat, maxLng, maxLat, SelectedCategory, NameBox.Text))
                             ?? new PlaceSearchResponseDto();
                SetCompanies((result.Places ?? new List<BusinessDto>()).Select(p => FromPlace(p, "Overture")), "Overture Maps");
                SetStatus((result.Count + " negocios encontrados · Overture " + result.ReleaseVersion).Trim(), false);
            });
        }

        private async void Scrape_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync("guardando", async () =>
            {
                double minLng, minLat, maxLng, maxLat;
                if (!TryBox(out minLng, out minLat, out maxLng, out maxLat)) return;
                var url = "api/business/scrape?" + BoxQuery(minLng, minLat, maxLng, maxLat, SelectedCategory, NameBox.Text);
                ScrapeResultDto result;
                using (var resp = await ApiClientFactory.Client.PostAsync(url, null))
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode)
                        throw new HttpRequestException("POST api/business/scrape → " + (int)resp.StatusCode);
                    result = Json.Deserialize<ScrapeResultDto>(body);
                }
                if (result == null)
                {
                    SetStatus("El scrape no devolvió resultado.", true);
                    return;
                }
                SetStatus("Scrape completo · " + result.FetchedCount + " obtenidos, " + result.PersistedCount +
                          " guardados en la base (categoría «" + SelectedCategory + "»).", false);
                await LoadSavedCategoriesAsync();
                var list = await ListBusinessesAsync(SelectedCategory) ?? new List<BusinessDto>();
                SetCompanies(list.Select(FromBusiness), "Base de datos");
            });
        }

        // ------------------------------------------------------------------ Mapeo

        private static CompanyRow FromBusiness(BusinessDto b)
        {
            return new CompanyRow
            {
                Name = string.IsNullOrWhiteSpace(b.Name) ? "Sin nombre" : b.Name,
                Industry = b.MainCategory ?? b.DetailedCategory ?? "Sin categoría",
                CategoryTag = b.CategoryTag,
                Location = JoinLocation(b.Locality, b.Region, b.Country),
                Source = "Base de datos",
                Country = b.Country,
                Region = b.Region,
                City = b.Locality,
                Email = (b.Emails ?? new List<string>()).FirstOrDefault(),
                Phone = (b.Phones ?? new List<string>()).FirstOrDefault(),
                Emails = b.Emails ?? new List<string>(),
                Phones = b.Phones ?? new List<string>(),
                LastContactedAtUtc = b.LastContactedAtUtc,
                ContactCount = b.ContactCount,
                LastCampaignRound = b.LastCampaignRound,
            };
        }

        private static CompanyRow FromPlace(BusinessDto p, string source)
        {
            return new CompanyRow
            {
                Name = string.IsNullOrWhiteSpace(p.Name) ? "Sin nombre" : p.Name,
                Industry = p.MainCategory ?? p.DetailedCategory ?? "Sin categoría",
                CategoryTag = p.CategoryTag,
                Location = JoinLocation(p.Locality, p.Region, p.Country),
                Contact = FirstNonEmpty(p.Emails) ?? FirstNonEmpty(p.Phones) ?? FirstNonEmpty(p.Websites) ?? "Sin contacto",
                Source = source,
                Country = p.Country,
                Region = p.Region,
                City = p.Locality,
                Email = FirstNonEmpty(p.Emails),
                Phone = FirstNonEmpty(p.Phones),
                Emails = p.Emails ?? new List<string>(),
                Phones = p.Phones ?? new List<string>(),
            };
        }

        private static string FirstNonEmpty(List<string> values)
        {
            return values == null ? null : values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        }

        private static string JoinLocation(string locality, string region, string country)
        {
            return string.Join(", ", new[] { locality, region, country }.Where(v => !string.IsNullOrWhiteSpace(v)));
        }

        // ------------------------------------------------------------------ Tabla: CRM y CSV

        private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var row = ItemsControl.ContainerFromElement(ResultsGrid, e.OriginalSource as DependencyObject) as DataGridRow;
            var company = row == null ? null : row.Item as CompanyRow;
            if (company == null) return;
            MainWindow.Current.Navigate(Routes.Crm, company.Name);
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var rows = view.Cast<CompanyRow>().ToList();
            if (rows.Count == 0)
            {
                SetStatus("No hay resultados para exportar. Ejecuta una búsqueda primero.", false);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Exportar resultados",
                Filter = "CSV (delimitado por punto y coma)|*.csv",
                FileName = "negocios.csv",
                AddExtension = true,
                DefaultExt = ".csv",
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

            try
            {
                var builder = new StringBuilder();
                builder.AppendLine("Empresa;Sector;Ubicacion;Correo;Telefono;Contacto;Fuente");
                foreach (var c in rows)
                {
                    builder.AppendLine(string.Join(";",
                        CsvField(c.Name), CsvField(c.Industry), CsvField(c.Location),
                        CsvField(string.Join(" | ", c.Emails)), CsvField(string.Join(" | ", c.Phones)),
                        CsvField(c.Contact), CsvField(c.Source)));
                }
                File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
                SetStatus("Exportados " + rows.Count + " resultado" + (rows.Count == 1 ? "" : "s") + " a " + dialog.FileName + ".", false);
            }
            catch (Exception ex)
            {
                SetStatus("No fue posible exportar: " + ex.Message, true);
            }
        }

        private static string CsvField(string value)
        {
            value = value ?? string.Empty;
            if (value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
