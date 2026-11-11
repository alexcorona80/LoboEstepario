using System.Threading.Tasks;

namespace LoboEstepario.Pages
{
    /// <summary>Claves de navegación (mismas rutas que la web).</summary>
    public static class Routes
    {
        public const string Inicio = "inicio";
        public const string Negocios = "negocios";
        public const string Crm = "crm";
        public const string Social = "social";
        public const string Fuentes = "fuentes";
        public const string Ejecuciones = "ejecuciones";
        public const string Configuracion = "configuracion";
    }

    /// <summary>
    /// Las páginas (UserControl) pueden implementarla para cargar datos cada vez que se muestran
    /// o para recibir un parámetro de navegación (p. ej. el nombre de un negocio hacia CRM).
    /// </summary>
    public interface IPage
    {
        Task OnNavigatedToAsync(object parameter);
    }
}
