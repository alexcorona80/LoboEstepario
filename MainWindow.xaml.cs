using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LoboEstepario.Pages;

namespace LoboEstepario
{
    /// <summary>
    /// Ventana única: sidebar de 264 px + topbar de 64 px + una página (UserControl) por sección.
    /// Los iconos y el orden de navegación son los de LoboEsteparioWeb/Components/Layout/NavMenu.razor.
    /// </summary>
    public partial class MainWindow : Window
    {
        private sealed class NavEntry
        {
            public string Key;
            public string Icon;
            public string Label;
            public Func<UserControl> Create;
            public UserControl Page;
        }

        // Para registrar una página nueva basta con cambiar su fábrica aquí.
        private readonly List<NavEntry> entries = new List<NavEntry>
        {
            new NavEntry { Key = Routes.Inicio, Icon = "▦", Label = "Inicio", Create = () => new HomePage() },
            new NavEntry { Key = Routes.Negocios, Icon = "⌕", Label = "Buscar negocios", Create = () => new NegociosPage() },
            new NavEntry { Key = Routes.Crm, Icon = "✉", Label = "CRM", Create = () => new CrmPage() },
            new NavEntry { Key = Routes.Social, Icon = "◈", Label = "Redes sociales", Create = () => new SocialPage() },
            new NavEntry { Key = Routes.Ejecuciones, Icon = "↻", Label = "Ejecuciones", Create = () => new EjecucionesPage() },
            new NavEntry { Key = Routes.Configuracion, Icon = "⚙", Label = "Configuración", Create = () => new ConfigPage() },
        };

        private readonly Dictionary<string, RadioButton> navButtons = new Dictionary<string, RadioButton>();
        private readonly DispatcherTimer pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        private bool navigating;
        private bool apiOk;

        /// <summary>Ventana principal en ejecución; las páginas la usan para navegar entre secciones.</summary>
        public static MainWindow Current { get; private set; }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Barra de título oscura (Windows 10 20H1+ / 11); si no está soportado se ignora.
            var dark = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
        }

        public MainWindow()
        {
            InitializeComponent();
            Current = this;
            BuildNavigation();
            Loaded += async (sender, args) =>
            {
                var start = Environment.GetEnvironmentVariable("LOBO_START_PAGE");
                await NavigateAsync(string.IsNullOrWhiteSpace(start) ? Routes.Inicio : start.Trim().ToLowerInvariant());
                await StartApiStatusAsync();
            };
        }

        private void BuildNavigation()
        {
            foreach (var entry in entries)
            {
                var content = new StackPanel { Orientation = Orientation.Horizontal };
                content.Children.Add(new TextBlock { Text = entry.Icon, Width = 20, TextAlignment = TextAlignment.Center, FontSize = 16, Opacity = 0.9 });
                content.Children.Add(new TextBlock { Text = entry.Label, Margin = new Thickness(12, 0, 0, 0) });
                var button = new RadioButton
                {
                    GroupName = "nav",
                    Style = (Style)FindResource("NavItem"),
                    Content = content,
                    Tag = entry.Key,
                };
                var key = entry.Key;
                button.Checked += async (sender, args) => { if (!navigating) await NavigateAsync(key); };
                navButtons[entry.Key] = button;
                NavPanel.Children.Add(button);
            }
        }

        /// <summary>Muestra la página de la sección. <paramref name="parameter"/> se entrega a <see cref="IPage"/>.</summary>
        public async Task NavigateAsync(string key, object parameter = null)
        {
            var entry = entries.Find(e => e.Key == key);
            if (entry == null) return;

            navigating = true;
            try
            {
                navButtons[key].IsChecked = true;
            }
            finally
            {
                navigating = false;
            }

            if (entry.Page == null) entry.Page = entry.Create();
            PageHost.Content = entry.Page;
            PageScroll.ScrollToTop();

            var page = entry.Page as IPage;
            if (page != null) await page.OnNavigatedToAsync(parameter);
        }

        public void Navigate(string key, object parameter = null)
        {
            var ignored = NavigateAsync(key, parameter);
        }

        // ------------------------------------------------------------------ estado de la API

        private async Task StartApiStatusAsync()
        {
            ApiClientFactory.WakingChanged += waking => Dispatcher.BeginInvoke(new Action(RefreshStatus));
            RefreshStatus();

            // Render free duerme el servicio: la primera llamada puede tardar ~50 s.
            apiOk = await ApiClientFactory.PingAsync(TimeSpan.FromSeconds(95));
            RefreshStatus();

            pollTimer.Tick += async (sender, args) =>
            {
                apiOk = await ApiClientFactory.PingAsync(TimeSpan.FromSeconds(15));
                RefreshStatus();
            };
            pollTimer.Start();
        }

        private void RefreshStatus()
        {
            if (ApiClientFactory.IsWaking && !apiOk)
            {
                SetStatus(ApiClientFactory.WakingMessage, "Gold");
                return;
            }
            if (apiOk) SetStatus("API conectada", "Green");
            else SetStatus("API sin conexión", "Ember");
        }

        private void SetStatus(string text, string accent)
        {
            var brush = (Brush)FindResource(accent + "Brush");
            StatusText.Text = text;
            StatusText.Foreground = brush;
            StatusDot.Fill = brush;
            StatusPill.BorderBrush = (Brush)FindResource(accent + "LineBrush");
            StatusPill.ToolTip = text;
        }
    }
}
