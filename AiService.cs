using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LoboEstepario
{
    /// <summary>Cliente del agente de IA configurado en el módulo de Configuración.</summary>
    public static class AiService
    {
        private static readonly HttpClient Client = new HttpClient();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        private static string Provider => GetSetting("AiProvider") ?? "openai";
        private static string BaseUrl => GetSetting("AiBaseUrl");
        private static string TextModel => GetSetting("AiModel");
        private static string ImageModel => GetSetting("AiImageModel");
        private static string ApiKey => GetSetting("AiApiKey");

        static AiService()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Client.Timeout = TimeSpan.FromMinutes(3);
        }

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(TextModel) && !string.IsNullOrWhiteSpace(ApiKey);

        private static string GetSetting(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static async Task<string> GenerateTextAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
                throw new InvalidOperationException("Configura la API key del agente de IA en el módulo de Configuración.");
            if (string.IsNullOrWhiteSpace(TextModel))
                throw new InvalidOperationException("Configura el modelo de texto del agente de IA en el módulo de Configuración.");

            switch (Provider)
            {
                case "anthropic": return await GenerateAnthropicTextAsync(prompt);
                case "google_gemini": return await GenerateGeminiTextAsync(prompt);
                case "azure_openai": return await GenerateOpenAiTextAsync(prompt, azure: true);
                default: return await GenerateOpenAiTextAsync(prompt, azure: false);
            }
        }

        public static async Task<byte[]> GenerateImageAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
                throw new InvalidOperationException("Configura la API key del agente de IA en el módulo de Configuración.");
            if (string.IsNullOrWhiteSpace(ImageModel))
                throw new InvalidOperationException("Configura el modelo de imágenes del agente de IA en el módulo de Configuración.");

            switch (Provider)
            {
                case "anthropic":
                case "google_gemini":
                    throw new NotSupportedException("La generación de imágenes requiere un proveedor OpenAI, Azure OpenAI o compatible (OpenAI-compatible).");
                case "azure_openai":
                    return await GenerateOpenAiImageAsync(prompt, azure: true);
                default:
                    return await GenerateOpenAiImageAsync(prompt, azure: false);
            }
        }

        private static string ResolveBaseUrl(string providerDefault)
        {
            return string.IsNullOrWhiteSpace(BaseUrl) ? providerDefault : BaseUrl.TrimEnd('/');
        }

        private static async Task<string> GenerateOpenAiTextAsync(string prompt, bool azure)
        {
            var baseUrl = ResolveBaseUrl("https://api.openai.com/v1");
            var endpoint = azure
                ? baseUrl + "/openai/deployments/" + Uri.EscapeDataString(TextModel) + "/chat/completions?api-version=2024-06-01"
                : baseUrl + "/chat/completions";

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "model", TextModel },
                { "messages", new object[]
                    {
                        new Dictionary<string, object> { { "role", "user" }, { "content", prompt } },
                    }
                },
                { "temperature", 0.8 },
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            AddAuthorization(request, azure);
            return await ReadChatTextAsync(await SendAsync(request, "Texto"), "OpenAI");
        }

        private static async Task<string> GenerateAnthropicTextAsync(string prompt)
        {
            var baseUrl = ResolveBaseUrl("https://api.anthropic.com");
            var endpoint = baseUrl + "/v1/messages";

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "model", TextModel },
                { "max_tokens", 1024 },
                { "messages", new object[]
                    {
                        new Dictionary<string, object> { { "role", "user" }, { "content", prompt } },
                    }
                },
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("x-api-key", ApiKey);
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

            var body = await SendAsync(request, "Texto");
            var root = body as IDictionary<string, object>;
            var content = root?["content"] as object[] ?? new object[0];
            var first = content.FirstOrDefault() as IDictionary<string, object>;
            var text = first?["text"] as string;
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        }

        private static async Task<string> GenerateGeminiTextAsync(string prompt)
        {
            var baseUrl = ResolveBaseUrl("https://generativelanguage.googleapis.com/v1beta");
            var endpoint = baseUrl + "/models/" + Uri.EscapeDataString(TextModel) + ":generateContent?key=" + Uri.EscapeDataString(ApiKey);

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "contents", new object[]
                    {
                        new Dictionary<string, object>
                        {
                            { "parts", new object[] { new Dictionary<string, object> { { "text", prompt } } } },
                        },
                    }
                },
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };

            var body = await SendAsync(request, "Texto");
            var root = body as IDictionary<string, object>;
            var candidates = root?["candidates"] as object[] ?? new object[0];
            var candidate = candidates.FirstOrDefault() as IDictionary<string, object>;
            var content = candidate?["content"] as IDictionary<string, object>;
            var parts = content?["parts"] as object[] ?? new object[0];
            var first = parts.FirstOrDefault() as IDictionary<string, object>;
            var text = first?["text"] as string;
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        }

        private static async Task<byte[]> GenerateOpenAiImageAsync(string prompt, bool azure)
        {
            var baseUrl = ResolveBaseUrl("https://api.openai.com/v1");
            var endpoint = azure
                ? baseUrl + "/openai/deployments/" + Uri.EscapeDataString(ImageModel) + "/images/generations?api-version=2024-04-01-preview"
                : baseUrl + "/images/generations";

            var payload = Json.Serialize(new Dictionary<string, object>
            {
                { "model", ImageModel },
                { "prompt", prompt },
                { "n", 1 },
                { "size", "1024x1024" },
                { "response_format", "b64_json" },
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            AddAuthorization(request, azure);

            var body = await SendAsync(request, "Imagen");
            var root = body as IDictionary<string, object>;
            var data = root?["data"] as object[] ?? new object[0];
            var first = data.FirstOrDefault() as IDictionary<string, object>;
            var b64 = first?["b64_json"] as string;
            if (string.IsNullOrWhiteSpace(b64) && first != null && first.ContainsKey("url"))
                throw new NotSupportedException("El proveedor devolvió una URL en vez de la imagen en base64; usa response_format b64_json.");
            if (string.IsNullOrWhiteSpace(b64))
                throw new InvalidOperationException("El proveedor no devolvió una imagen.");
            return Convert.FromBase64String(b64);
        }

        private static void AddAuthorization(HttpRequestMessage request, bool azure)
        {
            if (azure)
                request.Headers.TryAddWithoutValidation("api-key", ApiKey);
            else
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
        }

        private static async Task<object> SendAsync(HttpRequestMessage request, string label)
        {
            var response = await Client.SendAsync(request);
            var bodyText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(label + " · el proveedor respondió " + (int)response.StatusCode + ": " + Truncate(bodyText));
            return Json.DeserializeObject(bodyText);
        }

        private static async Task<string> ReadChatTextAsync(object body, string label)
        {
            var root = body as IDictionary<string, object>;
            var choices = root?["choices"] as object[] ?? new object[0];
            var first = choices.FirstOrDefault() as IDictionary<string, object>;
            var message = first?["message"] as IDictionary<string, object>;
            var text = message?["content"] as string;
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException(label + " · el proveedor no devolvió texto.");
            return text.Trim();
        }

        internal static string Truncate(string value)
        {
            if (value == null) return string.Empty;
            return value.Length <= 400 ? value : value.Substring(0, 400);
        }
    }
}