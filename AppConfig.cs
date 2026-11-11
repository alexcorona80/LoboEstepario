using System;
using System.Collections.Generic;
using System.IO;
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
        private static readonly object Gate = new object();

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
                var json = JsonSerializer.Serialize(User, new JsonSerializerOptions { WriteIndented = true });
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
                    if (loaded != null) foreach (var pair in loaded) result[pair.Key] = pair.Value;
                }
            }
            catch (Exception)
            {
                // Un settings.json corrupto no debe impedir que la app arranque.
            }
            return result;
        }
    }
}
