using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;

namespace LoboEstepario
{
    public partial class MainWindow : Window
    {
        private readonly ICollectionView companiesView;
        private static readonly HttpClient ApiClient = CreateApiClient();
        private static readonly HttpClient GeocodingClient = CreateGeocodingClient();
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private double selectedLongitude = -103.349;
        private double selectedLatitude = 20.676;
        private GMapMarker centerMarker;
        private GMapPolygon radiusPolygon;
        private Point mapMouseDownPoint;
        public ObservableCollection<Company> Companies { get; } = new ObservableCollection<Company>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            companiesView = CollectionViewSource.GetDefaultView(Companies);
            companiesView.Filter = MatchesSearch;
            UpdateResultCount();
            Loaded += (sender, args) =>
            {
                ConfigureMap();
                LoadDashboardStats();
                LoadSavedBusinesses_Click(sender, args);
            };
        }

        private static HttpClient CreateGeocodingClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LoboEstepario-Desktop/0.1");
            return client;
        }

        private static HttpClient CreateApiClient()
        {
            var configuredBaseUrl = ConfigurationManager.AppSettings["ApiBaseUrl"] ?? "http://localhost:5080/";
            return new HttpClient { BaseAddress = new Uri(configuredBaseUrl) };
        }

        private bool MatchesSearch(object item)
        {
            var company = item as Company;
            var query = SearchBox == null ? string.Empty : SearchBox.Text.Trim();
            var country = CountryBox == null ? string.Empty : CountryBox.Text.Trim();
            var region = RegionBox == null ? string.Empty : RegionBox.Text.Trim();
            var city = CityBox == null ? string.Empty : CityBox.Text.Trim();
            return company != null && (string.IsNullOrWhiteSpace(query)
                || company.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || company.Industry.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || company.Location.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                && Contains(company.Country, country)
                && Contains(company.Region, region)
                && Contains(company.City, city);
        }

        private static bool Contains(string value, string query) => string.IsNullOrWhiteSpace(query)
            || (!string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (companiesView == null) return;
            companiesView.Refresh();
            UpdateResultCount();
        }

        private void LocationFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (companiesView == null) return;
            companiesView.Refresh();
            UpdateResultCount();
        }

        private void UpdateResultCount()
        {
            if (ResultCount != null) ResultCount.Text = string.Format("{0} resultados", companiesView == null ? Companies.Count : companiesView.Cast<object>().Count());
        }

        private void Navigation_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && string.Equals(button.Tag as string, "CRM", StringComparison.OrdinalIgnoreCase))
            {
                new CrmWindow { Owner = this }.Show();
                return;
            }
            StatusText.Text = string.Format("La sección {0} estará disponible al conectar los datos.", button == null ? "seleccionada" : button.Tag);
        }

        private void CompaniesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var company = CompaniesGrid.SelectedItem as Company;
            if (company == null) return;
            new CrmWindow(company.Name) { Owner = this }.Show();
        }

        private void NewSearch_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Nueva búsqueda preparada. Conecta el servicio de prospección para ejecutarla.";
            SearchBox.Focus();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "La exportación se habilitará al conectar el repositorio de empresas.";
        }

        private void ConfigureMap()
        {
            GMapProvider.UserAgent = "LoboEstepario-Desktop/0.1";
            MapControl.MapProvider = GMapProviders.OpenStreetMap;
            MapControl.MinZoom = 2;
            MapControl.MaxZoom = 18;
            MapControl.Position = new PointLatLng(selectedLatitude, selectedLongitude);
            MapControl.Zoom = 12;
            MapControl.CanDragMap = true;
            MapControl.MouseWheelZoomType = MouseWheelZoomType.MousePositionAndCenter;

            MapControl.MouseDown += MapControl_MouseDown;
            MapControl.MouseUp += MapControl_MouseUp;

            UpdateMapMarker();
        }

        private void MapControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            mapMouseDownPoint = e.GetPosition(MapControl);
        }

        private void MapControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            var point = e.GetPosition(MapControl);
            var delta = point - mapMouseDownPoint;
            if (delta.Length > 4) return;

            var position = MapControl.FromLocalToLatLng((int)point.X, (int)point.Y);
            selectedLatitude = position.Lat;
            selectedLongitude = position.Lng;
            UpdateMapMarker();
            StatusText.Text = "Centro de búsqueda actualizado en el mapa.";
        }

        private void Radius_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateMapMarker();
        }

        private void UpdateMapMarker()
        {
            if (MapControl == null) return;

            var position = new PointLatLng(selectedLatitude, selectedLongitude);
            if (centerMarker == null)
            {
                centerMarker = new GMapMarker(position)
                {
                    Shape = new Ellipse
                    {
                        Width = 16,
                        Height = 16,
                        Fill = new SolidColorBrush(Color.FromRgb(226, 166, 61)),
                        Stroke = Brushes.White,
                        StrokeThickness = 1.5,
                    },
                    Offset = new Point(-8, -8),
                };
                MapControl.Markers.Add(centerMarker);
            }
            else
            {
                centerMarker.Position = position;
            }

            var radiusKm = SelectedRadiusKilometers();
            var circlePoints = BuildCirclePoints(selectedLatitude, selectedLongitude, radiusKm);
            if (radiusPolygon != null) MapControl.Markers.Remove(radiusPolygon);
            radiusPolygon = new GMapPolygon(circlePoints);
            MapControl.Markers.Add(radiusPolygon);
            MapControl.RegenerateShape(radiusPolygon);
            var radiusShape = radiusPolygon.Shape as Path;
            if (radiusShape != null)
            {
                radiusShape.Fill = new SolidColorBrush(Color.FromArgb(60, 226, 166, 61));
                radiusShape.Stroke = new SolidColorBrush(Color.FromRgb(226, 166, 61));
                radiusShape.StrokeThickness = 1.5;
            }

            if (MapCoordinates != null)
                MapCoordinates.Text = string.Format(CultureInfo.InvariantCulture, "Centro: {0:F4}, {1:F4} · radio {2} km", selectedLatitude, selectedLongitude, radiusKm);
        }

        private static List<PointLatLng> BuildCirclePoints(double latitude, double longitude, double radiusKm)
        {
            var points = new List<PointLatLng>();
            var latDelta = radiusKm / 111.32;
            var lonDelta = radiusKm / (111.32 * Math.Cos(latitude * Math.PI / 180));
            const int steps = 72;
            for (var i = 0; i <= steps; i++)
            {
                var angle = 2 * Math.PI * i / steps;
                points.Add(new PointLatLng(latitude + latDelta * Math.Sin(angle), longitude + lonDelta * Math.Cos(angle)));
            }
            return points;
        }

        private async void MapSearch_Click(object sender, RoutedEventArgs e)
        {
            var query = MapSearchBox == null ? string.Empty : MapSearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            try
            {
                MapSearchButton.IsEnabled = false;
                StatusText.Text = "Buscando \"" + query + "\"…";
                var url = "https://nominatim.openstreetmap.org/search?format=json&limit=1&q=" + Uri.EscapeDataString(query);
                var response = await GeocodingClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var raw = json.DeserializeObject(await response.Content.ReadAsStringAsync()) as object[];
                if (raw == null || raw.Length == 0)
                {
                    StatusText.Text = "No se encontraron resultados para \"" + query + "\".";
                    return;
                }

                var first = raw[0] as IDictionary<string, object>;
                double latitude, longitude;
                if (first == null
                    || !double.TryParse(Convert.ToString(first["lat"], CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
                    || !double.TryParse(Convert.ToString(first["lon"], CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
                {
                    StatusText.Text = "No fue posible interpretar la ubicación encontrada.";
                    return;
                }

                selectedLatitude = latitude;
                selectedLongitude = longitude;
                MapControl.Position = new PointLatLng(latitude, longitude);
                MapControl.Zoom = 12;
                UpdateMapMarker();
                var displayName = first.ContainsKey("display_name") ? first["display_name"] as string : query;
                StatusText.Text = "Mapa centrado en " + (displayName ?? query) + ".";
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible buscar la ubicación: " + ex.Message;
            }
            finally
            {
                MapSearchButton.IsEnabled = true;
            }
        }

        private void MapSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) MapSearch_Click(sender, e);
        }

        private int SelectedRadiusKilometers()
        {
            var item = RadiusBox == null ? null : RadiusBox.SelectedItem as ComboBoxItem;
            int radius;
            return item != null && int.TryParse(item.Tag as string, out radius) ? radius : 5;
        }

        // El Tag de cada ComboBoxItem guarda el basic_category real de Overture (en ingles);
        // el Content es solo la etiqueta en espanol que ve el usuario.
        private string SelectedCategorySlug()
        {
            var item = CategoryBox == null ? null : CategoryBox.SelectedItem as ComboBoxItem;
            return item == null ? string.Empty : (item.Tag as string ?? string.Empty);
        }

        private async void RunSearch_Click(object sender, RoutedEventArgs e)
        {
            var radiusKm = SelectedRadiusKilometers();
            var latitudeDelta = radiusKm / 111.32;
            var longitudeDelta = radiusKm / (111.32 * Math.Cos(selectedLatitude * Math.PI / 180));
            var category = SelectedCategorySlug();
            var name = BusinessNameBox == null ? string.Empty : BusinessNameBox.Text.Trim();
            var requestUri = string.Format(CultureInfo.InvariantCulture,
                "api/places?minLongitude={0}&minLatitude={1}&maxLongitude={2}&maxLatitude={3}&limit=100{4}{5}",
                selectedLongitude - longitudeDelta, selectedLatitude - latitudeDelta,
                selectedLongitude + longitudeDelta, selectedLatitude + latitudeDelta,
                string.IsNullOrWhiteSpace(category) ? string.Empty : "&category=" + Uri.EscapeDataString(category),
                string.IsNullOrWhiteSpace(name) ? string.Empty : "&nameContains=" + Uri.EscapeDataString(name));

            try
            {
                SearchButton.IsEnabled = false;
                StatusText.Text = "Consultando Overture mediante la API local…";
                var response = await ApiClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();
                var payload = json.Deserialize<PlacesResponse>(await response.Content.ReadAsStringAsync());
                Companies.Clear();
                foreach (var place in payload.Places ?? new PlaceDto[0])
                {
                    Companies.Add(FromPlace(place, "Overture"));
                }
                companiesView.Refresh();
                UpdateResultCount();
                StatusText.Text = string.Format("{0} negocios encontrados · Overture {1}", payload.Count, payload.ReleaseVersion ?? "");
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible conectar con la API: " + ex.Message;
            }
            finally
            {
                SearchButton.IsEnabled = true;
            }
        }

        private async void LoadSavedBusinesses_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SavedBusinessesButton.IsEnabled = false;
                StatusText.Text = "Cargando negocios guardados desde MongoDB…";
                var response = await ApiClient.GetAsync("api/business?limit=1000");
                response.EnsureSuccessStatusCode();
                var places = json.Deserialize<PlaceDto[]>(await response.Content.ReadAsStringAsync()) ?? new PlaceDto[0];
                Companies.Clear();
                foreach (var place in places) Companies.Add(FromPlace(place, "Base de datos"));
                companiesView.Refresh();
                UpdateResultCount();
                StatusText.Text = string.Format("{0} negocios cargados desde la base de datos.", places.Length);
                LoadDashboardStats();
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible cargar la base de datos: " + ex.Message;
            }
            finally
            {
                SavedBusinessesButton.IsEnabled = true;
            }
        }

        private async void RunScrape_Click(object sender, RoutedEventArgs e)
        {
            var radiusKm = SelectedRadiusKilometers();
            var latitudeDelta = radiusKm / 111.32;
            var longitudeDelta = radiusKm / (111.32 * Math.Cos(selectedLatitude * Math.PI / 180));
            var category = SelectedCategorySlug();
            var name = BusinessNameBox == null ? string.Empty : BusinessNameBox.Text.Trim();
            var requestUri = string.Format(CultureInfo.InvariantCulture,
                "api/business/scrape?minLongitude={0}&minLatitude={1}&maxLongitude={2}&maxLatitude={3}&limit=100{4}{5}",
                selectedLongitude - longitudeDelta, selectedLatitude - latitudeDelta,
                selectedLongitude + longitudeDelta, selectedLatitude + latitudeDelta,
                string.IsNullOrWhiteSpace(category) ? string.Empty : "&category=" + Uri.EscapeDataString(category),
                string.IsNullOrWhiteSpace(name) ? string.Empty : "&nameContains=" + Uri.EscapeDataString(name));

            try
            {
                ScrapeButton.IsEnabled = false;
                StatusText.Text = "Scrapeando Overture y guardando en MongoDB…";
                var response = await ApiClient.PostAsync(requestUri, null);
                response.EnsureSuccessStatusCode();
                var result = json.Deserialize<ScrapeResultDto>(await response.Content.ReadAsStringAsync());
                StatusText.Text = string.Format(CultureInfo.InvariantCulture,
                    "Scrape completo · {0} obtenidos, {1} guardados en la base de datos (Overture {2}).",
                    result.FetchedCount, result.PersistedCount, result.ReleaseVersion ?? "");
                LoadDashboardStats();
            }
            catch (Exception ex)
            {
                StatusText.Text = "No fue posible completar el scrape: " + ex.Message;
            }
            finally
            {
                ScrapeButton.IsEnabled = true;
            }
        }

        private async void LoadDashboardStats()
        {
            try
            {
                var response = await ApiClient.GetAsync("api/business/stats");
                response.EnsureSuccessStatusCode();
                var stats = json.Deserialize<BusinessStatsDto>(await response.Content.ReadAsStringAsync());
                if (stats == null) return;

                if (StatTotalCompanies != null) StatTotalCompanies.Text = stats.TotalCount.ToString("N0", CultureInfo.InvariantCulture);
                if (StatWithContact != null) StatWithContact.Text = stats.WithContactCount.ToString("N0", CultureInfo.InvariantCulture);
                if (StatActiveSources != null) StatActiveSources.Text = stats.ActiveSourceCount.ToString(CultureInfo.InvariantCulture);

                DateTime lastRunUtc;
                if (StatLastRun != null)
                {
                    StatLastRun.Text = DateTime.TryParse(stats.LastScrapedAtUtc, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out lastRunUtc)
                        ? FormatLastRun(lastRunUtc)
                        : "Sin ejecuciones";
                }
            }
            catch (Exception ex)
            {
                if (StatusText != null) StatusText.Text = "No fue posible cargar las estadísticas: " + ex.Message;
            }
        }

        private static string FormatLastRun(DateTime utcTime)
        {
            var local = utcTime.ToLocalTime();
            return local.Date == DateTime.Now.Date
                ? string.Format(CultureInfo.InvariantCulture, "Hoy · {0:HH:mm}", local)
                : string.Format(CultureInfo.InvariantCulture, "{0:dd/MM} · {0:HH:mm}", local);
        }

        private static Company FromPlace(PlaceDto place, string source)
        {
            return new Company(place.Name ?? "Sin nombre", place.MainCategory ?? place.DetailedCategory ?? "Sin categoría",
                JoinLocation(place.Locality, place.Region, place.Country), FirstOrDefault(place.Emails, place.Phones, place.Websites), source,
                place.Country, place.Region, place.Locality);
        }

        private static string JoinLocation(string locality, string region, string country)
        {
            return string.Join(", ", new[] { locality, region, country }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string FirstOrDefault(string[] emails, string[] phones, string[] websites)
        {
            return (emails ?? new string[0]).FirstOrDefault() ?? (phones ?? new string[0]).FirstOrDefault() ?? (websites ?? new string[0]).FirstOrDefault() ?? "Sin contacto";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
                return;
            }
            DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }

    public sealed class Company
    {
        public Company(string name, string industry, string location, string contact, string source, string country = null, string region = null, string city = null)
        {
            Name = name; Industry = industry; Location = location; Contact = contact; Source = source; Country = country; Region = region; City = city;
        }
        public string Name { get; private set; }
        public string Industry { get; private set; }
        public string Location { get; private set; }
        public string Contact { get; private set; }
        public string Source { get; private set; }
        public string Country { get; private set; }
        public string Region { get; private set; }
        public string City { get; private set; }
    }

    public sealed class PlacesResponse
    {
        public string ReleaseVersion { get; set; }
        public int Count { get; set; }
        public PlaceDto[] Places { get; set; }
    }

    public sealed class PlaceDto
    {
        public string Name { get; set; }
        public string MainCategory { get; set; }
        public string DetailedCategory { get; set; }
        public string Locality { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        public string[] Emails { get; set; }
        public string[] Phones { get; set; }
        public string[] Websites { get; set; }
    }

    public sealed class ScrapeResultDto
    {
        public string ReleaseVersion { get; set; }
        public int FetchedCount { get; set; }
        public long PersistedCount { get; set; }
    }

    public sealed class BusinessStatsDto
    {
        public long TotalCount { get; set; }
        public long WithContactCount { get; set; }
        public int ActiveSourceCount { get; set; }
        public string LastScrapedAtUtc { get; set; }
    }
}
