using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace LoboEstepario
{
    /// <summary>
    /// Configuración de la app. Orden de prioridad (mayor a menor):
    /// 1) settings.json de usuario en %AppData%\LoboEstepario (lo que se guarda desde Configuración),
    /// 2) variables de entorno LOBO_&lt;Clave&gt;,
    /// 3) appsettings.json junto al ejecutable (valores por defecto, sin secretos).
    /// </summary>
    public static class AppConfig
    {
        private const string ProtectedPrefix = "dpapi:";
        private static readonly object Gate = new object();

        /// <summary>Claves cuyo valor se guarda cifrado con DPAPI (ámbito CurrentUser).</summary>
        private static readonly HashSet<string> SecretKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "CrmSmtpPassword", "FacebookAccessToken", "FacebookAppSecret",
            "InstagramAccessToken", "InstagramAppSecret", "AiApiKey",
        };

        public static string UserSettingsPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LoboEstepario", "settings.json");

        private static readonly IConfigurationRoot Defaults = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables("LOBO_")
            .Build();

        private static readonly Dictionary<string, string> User = LoadUser();

        /// <summary>Valor de la clave, o null si no existe en ninguna capa.</summary>
        public static string Get(string key)
        {
            lock (Gate)
            {
                if (User.TryGetValue(key, out var value)) return value;
            }
            return Defaults[key];
        }

        public static void Set(string key, string value)
        {
            lock (Gate) { User[key] = value ?? string.Empty; }
        }

        public static void Save()
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(UserSettingsPath));
                var onDisk = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in User)
                    onDisk[pair.Key] = SecretKeys.Contains(pair.Key) ? Protect(pair.Value) : pair.Value;
                var json = JsonSerializer.Serialize(onDisk, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(UserSettingsPath, json);
            }
        }

        private static Dictionary<string, string> LoadUser()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(UserSettingsPath))
                {
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(UserSettingsPath));
                    if (loaded != null) foreach (var pair in loaded) result[pair.Key] = SecretKeys.Contains(pair.Key) ? Unprotect(pair.Value) : pair.Value;
                }
            }
            catch (Exception)
            {
                // Un settings.json corrupto no debe impedir que la app arranque.
            }
            return result;
        }

        private static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
            return ProtectedPrefix + Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Descifra un valor guardado. Un valor sin prefijo (archivo anterior en texto plano) se acepta tal cual
        /// y se cifrará en el próximo Save; si no se puede descifrar (otro usuario/equipo) se devuelve vacío.
        /// </summary>
        private static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored) || !stored.StartsWith(ProtectedPrefix, StringComparison.Ordinal)) return stored ?? string.Empty;
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(stored.Substring(ProtectedPrefix.Length)), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
