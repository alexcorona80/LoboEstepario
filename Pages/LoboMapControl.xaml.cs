using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Web.WebView2.Core;

namespace LoboEstepario.Pages
{
    /// <summary>Marcador de resultado para el mapa.</summary>
    public sealed class MapMarker
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Mapa Leaflet (mismo que la web) alojado en WebView2. API tipada sobre mensajes JSON.
    /// Los activos locales (Assets\map) se sirven por el host virtual https://lobo.map/.
    /// </summary>
    public partial class LoboMapControl : UserControl
    {
        private const string VirtualHost = "lobo.map";
        private bool started;
        private bool ready;
        private double lat = 20.6760;
        private double lng = -103.3490;
        private double radiusKm = 2;
        private IList<MapMarker> markers;

        public LoboMapControl()
        {
            InitializeComponent();
        }

        /// <summary>El usuario hizo clic o arrastró el mapa: nuevo centro de búsqueda.</summary>
        public event Action<double, double> CenterChanged;

        /// <summary>True si WebView2 se inicializó correctamente.</summary>
        public bool IsAvailable { get; private set; }

        public void SetCenter(double latitude, double longitude)
        {
            lat = latitude; lng = longitude;
            Post(new { type = "setCenter", lat = latitude, lng = longitude });
        }

        /// <summary>Mueve el centro y desplaza la vista del mapa hasta él.</summary>
        public void Recenter(double latitude, double longitude)
        {
            lat = latitude; lng = longitude;
            Post(new { type = "recenter", lat = latitude, lng = longitude });
        }

        public void SetRadius(double km)
        {
            radiusKm = km;
            Post(new { type = "setRadius", radiusKm = km });
        }

        public void SetMarkers(IList<MapMarker> items)
        {
            markers = items;
            PostMarkers();
        }

        private void PostMarkers()
        {
            var list = new List<object>();
            if (markers != null)
                foreach (var m in markers) list.Add(new { lat = m.Lat, lng = m.Lng, name = m.Name });
            Post(new { type = "setMarkers", items = list });
        }

        private void Post(object message)
        {
            if (!ready || Web.CoreWebView2 == null) return;
            try { Web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message)); }
            catch (Exception) { }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (started) return;
            started = true;
            try
            {
                var dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LoboEstepario", "WebView2");
                Directory.CreateDirectory(dataFolder);
                var env = await CoreWebView2Environment.CreateAsync(null, dataFolder);
                await Web.EnsureCoreWebView2Async(env);

                var assets = Path.Combine(AppContext.BaseDirectory, "Assets", "map");
                if (!File.Exists(Path.Combine(assets, "index.html")))
                    throw new FileNotFoundException("Faltan los archivos del mapa (Assets\\map) junto al ejecutable.");

                var core = Web.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping(VirtualHost, assets, CoreWebView2HostResourceAccessKind.Allow);
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.WebMessageReceived += OnWebMessage;
                core.NavigationCompleted += OnNavigationCompleted;
                IsAvailable = true;
                core.Navigate("https://" + VirtualHost + "/index.html");
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
                ShowError(new InvalidOperationException("La navegación del mapa falló (" + e.WebErrorStatus + ")."));
        }

        private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using (var doc = JsonDocument.Parse(e.WebMessageAsJson))
                {
                    var root = doc.RootElement;
                    var type = root.GetProperty("type").GetString();
                    if (type == "ready")
                    {
                        ready = true;
                        SetCenter(lat, lng);
                        SetRadius(radiusKm);
                        if (markers != null) PostMarkers();
                    }
                    else if (type == "center")
                    {
                        lat = root.GetProperty("lat").GetDouble();
                        lng = root.GetProperty("lng").GetDouble();
                        SetCenter(lat, lng);
                        var handler = CenterChanged;
                        if (handler != null) handler(lat, lng);
                    }
                }
            }
            catch (Exception) { }
        }

        private void ShowError(Exception ex)
        {
            IsAvailable = false;
            ready = false;
            Web.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            var missingRuntime = ex is WebView2RuntimeNotFoundException;
            ErrorTitle.Text = missingRuntime
                ? "No se encontró el runtime de WebView2, necesario para mostrar el mapa."
                : "No se pudo inicializar el mapa.";
            ErrorDetail.Text = missingRuntime ? "" : ex.Message;
            ErrorDetail.Visibility = missingRuntime ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnRuntimeLink(object sender, RequestNavigateEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception) { }
            e.Handled = true;
        }
    }
}
