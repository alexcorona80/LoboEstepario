using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LoboEstepario
{
    /// <summary>
    /// Único punto de creación del HttpClient hacia LoboEsteparioAPI.
    /// Render (plan free) duerme el servicio: la primera llamada puede tardar ~50 s,
    /// por eso el timeout es de 100 s y se notifica el estado "Despertando servidor…".
    /// </summary>
    public static class ApiClientFactory
    {
        public const string DefaultBaseUrl = "https://lobo-api.onrender.com/";
        public const string WakingMessage = "Despertando servidor…";

        private static readonly TimeSpan WakeNoticeDelay = TimeSpan.FromSeconds(3);
        private static int pending;
        private static volatile bool warm;
        private static volatile bool waking;

        public static string BaseUrl { get; } = ResolveBaseUrl();

        /// <summary>HttpClient compartido para toda la aplicación.</summary>
        public static HttpClient Client { get; } = CreateClient();

        /// <summary>True mientras la API tarda en responder por un arranque en frío.</summary>
        public static bool IsWaking => waking;

        /// <summary>Se dispara (en cualquier hilo) cuando cambia <see cref="IsWaking"/>.</summary>
        public static event Action<bool> WakingChanged;

        /// <summary>GET api/health. Devuelve false ante cualquier error o si se agota <paramref name="timeout"/>.</summary>
        public static async Task<bool> PingAsync(TimeSpan timeout)
        {
            try
            {
                using (var cts = new CancellationTokenSource(timeout))
                using (var response = await Client.GetAsync("api/health", cts.Token).ConfigureAwait(false))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string ResolveBaseUrl()
        {
            // Sobreescritura para desarrollo local, p. ej. http://localhost:5080/
            var value = Environment.GetEnvironmentVariable("LOBO_API_URL");
            if (string.IsNullOrWhiteSpace(value)) value = AppConfig.Get("ApiBaseUrl");
            if (string.IsNullOrWhiteSpace(value)) value = DefaultBaseUrl;
            value = value.Trim();
            return value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
        }

        private static HttpClient CreateClient()
        {
            var handler = new ColdStartHandler(new HttpClientHandler());
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(100),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LoboEstepario-Desktop/1.0");
            return client;
        }

        private static void SetWaking(bool value)
        {
            if (waking == value) return;
            waking = value;
            var handler = WakingChanged;
            if (handler != null) handler(value);
        }

        private sealed class ColdStartHandler : DelegatingHandler
        {
            public ColdStartHandler(HttpMessageHandler inner) : base(inner) { }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref pending);
                using (var notice = new CancellationTokenSource())
                {
                    if (!warm)
                    {
                        var token = notice.Token;
                        // Solo se avisa si la API no ha respondido todavía en esta sesión y la espera se alarga.
                        var ignored = Task.Delay(WakeNoticeDelay, token).ContinueWith(t =>
                        {
                            if (!t.IsCanceled && !warm) SetWaking(true);
                        }, TaskScheduler.Default);
                    }
                    try
                    {
                        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                        if (response.StatusCode != HttpStatusCode.BadGateway
                            && response.StatusCode != HttpStatusCode.ServiceUnavailable
                            && response.StatusCode != HttpStatusCode.GatewayTimeout)
                        {
                            warm = true;
                        }
                        return response;
                    }
                    finally
                    {
                        notice.Cancel();
                        if (Interlocked.Decrement(ref pending) == 0) SetWaking(false);
                    }
                }
            }
        }
    }
}
