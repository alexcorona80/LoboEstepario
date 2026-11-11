using System;
using System.Collections.Generic;
using System.Windows;

namespace LoboEstepario.Pages
{
    public sealed class BusinessDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string MainCategory { get; set; }
        public string DetailedCategory { get; set; }
        public string CategoryTag { get; set; }
        public string Address { get; set; }
        public string Locality { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        public List<string> Phones { get; set; } = new List<string>();
        public List<string> Websites { get; set; } = new List<string>();
        public List<string> Emails { get; set; } = new List<string>();
        public DateTime? LastContactedAtUtc { get; set; }
        public int ContactCount { get; set; }
        public string LastCampaignRound { get; set; }
    }

    public sealed class PlaceSearchResponseDto
    {
        public string ReleaseVersion { get; set; }
        public int Count { get; set; }
        public List<BusinessDto> Places { get; set; } = new List<BusinessDto>();
    }

    public sealed class NominatimHit
    {
        public string Lat { get; set; }
        public string Lon { get; set; }
        public string display_name { get; set; }
    }

    /// <summary>Fila de la tabla de resultados.</summary>
    public sealed class CompanyRow
    {
        public string Name { get; set; }
        public string Industry { get; set; }
        public string CategoryTag { get; set; }
        public string Location { get; set; }
        public string Contact { get; set; }
        public string Source { get; set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public string City { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public List<string> Emails { get; set; } = new List<string>();
        public List<string> Phones { get; set; } = new List<string>();
        public DateTime? LastContactedAtUtc { get; set; }
        public int ContactCount { get; set; }
        public string LastCampaignRound { get; set; }
        public bool HasEmail { get { return !string.IsNullOrWhiteSpace(Email); } }
        public bool HasPhone { get { return !string.IsNullOrWhiteSpace(Phone); } }
        public Visibility TagVisibility { get { return !string.IsNullOrWhiteSpace(CategoryTag) && !string.Equals(CategoryTag, Industry, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility EmailChipVisibility { get { return HasEmail ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility PhoneChipVisibility { get { return HasPhone ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility SentVisibility { get { return LastContactedAtUtc.HasValue ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility UnsentVisibility { get { return LastContactedAtUtc.HasValue ? Visibility.Collapsed : Visibility.Visible; } }
        public bool FromDatabase { get { return Source == "Base de datos"; } }
        public Visibility DbVisibility { get { return FromDatabase ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility OvertureVisibility { get { return FromDatabase ? Visibility.Collapsed : Visibility.Visible; } }
        public Visibility FallbackVisibility { get { return Emails.Count == 0 && Phones.Count == 0 && !string.IsNullOrEmpty(Contact) ? Visibility.Visible : Visibility.Collapsed; } }
        public string SentTooltip { get { return LastContactedAtUtc.HasValue ? "Último contacto: " + LastContactedAtUtc.Value.ToLocalTime().ToString("g") : null; } }
        public string EmailsText { get { return string.Join("  ", Emails); } }
        public string PhonesText { get { return string.Join("  ", Phones); } }
        public string ContactText
        {
            get
            {
                var parts = new List<string>(Emails);
                parts.AddRange(Phones);
                if (parts.Count == 0) return Contact ?? "";
                return string.Join("  ·  ", parts);
            }
        }
        public string Tags
        {
            get
            {
                var t = new List<string>();
                if (HasEmail) t.Add("✉ correo");
                if (HasPhone) t.Add("☏ teléfono");
                return string.Join("  ", t);
            }
        }
        public string CategoryText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CategoryTag) && !string.Equals(CategoryTag, Industry, StringComparison.OrdinalIgnoreCase))
                    return Industry + "  ·  " + CategoryTag;
                return Industry;
            }
        }
        public string History
        {
            get
            {
                if (LastContactedAtUtc.HasValue)
                    return "Enviado (" + ContactCount + ") · " + (LastCampaignRound ?? "General");
                return "Sin contactar";
            }
        }
    }
    public static class OvertureCategories
    {
        public static readonly (string Slug, string Label)[] All = new (string Slug, string Label)[] {
            ("", "Todas las categorias"),
            ("auto_dealer", "Agencia de autos"),
            ("vehicle_dealer", "Agencia de vehiculos"),
            ("travel_service", "Agencia de viajes"),
            ("food_and_drink", "Alimentos y bebidas"),
            ("private_lodging", "Alojamiento privado"),
            ("specialized_health_care", "Atencion medica especializada"),
            ("bank_or_credit_union", "Banco o caja de ahorro"),
            ("storage_facility", "Bodega de almacenamiento"),
            ("casino", "Casino"),
            ("shopping_mall", "Centro comercial"),
            ("gaming_venue", "Centro de videojuegos"),
            ("place_of_learning", "Centro educativo"),
            ("specialized_medical_facility", "Centro medico especializado"),
            ("brewery", "Cerveceria"),
            ("movie_theater", "Cine"),
            ("warehouse_club_store", "Club de precios / mayorista"),
            ("social_club", "Club social"),
            ("condominium", "Condominio"),
            ("building_or_construction_service", "Construccion"),
            ("attorney_or_law_firm", "Despacho de abogados"),
            ("distillery", "Destileria"),
            ("specialty_school", "Escuela especializada"),
            ("fueling_station", "Estacion de combustible"),
            ("radio_station", "Estacion de radio"),
            ("parking", "Estacionamiento"),
            ("manufacturer", "Fabricante"),
            ("hardware_home_and_garden_store", "Ferreteria, hogar y jardin"),
            ("gas_station", "Gasolinera"),
            ("gym", "Gimnasio"),
            ("farm", "Granja"),
            ("hospital", "Hospital"),
            ("hotel", "Hotel"),
            ("industrial_facility_or_service", "Instalacion industrial"),
            ("diagnostics_imaging_or_lab_service", "Laboratorio clinico / imagenologia"),
            ("corporate_or_business_office", "Oficina corporativa"),
            ("food_court", "Plaza de comidas"),
            ("supplier_or_distributor", "Proveedor o distribuidor"),
            ("arcade", "Sala de videojuegos"),
            ("reproductive_perinatal_and_womens_care", "Salud de la mujer"),
            ("environmental_or_ecological_service", "Servicio ambiental"),
            ("b2b_science_and_technology_service", "Servicio cientifico y tecnologico (B2B)"),
            ("design_service", "Servicio de diseno"),
            ("media_service", "Servicio de medios"),
            ("security_service", "Servicio de seguridad"),
            ("b2b_industrial_and_machine_service", "Servicio industrial y maquinaria (B2B)"),
            ("professional_service", "Servicio profesional"),
            ("vehicle_service", "Taller mecanico"),
            ("taxi_or_ride_share_service", "Taxi o app de transporte"),
            ("telecommunications_service", "Telecomunicaciones"),
            ("it_hardware_repair", "TI: equipo de computo y reparacion"),
            ("it_software_services", "TI: software y servicios"),
            ("it_telecom_internet", "TI: telecom, internet y hosting"),
            ("food_and_beverage_store", "Tienda de alimentos y bebidas"),
            ("sporting_goods_store", "Tienda de articulos deportivos"),
            ("electronics_store", "Tienda de electronica"),
            ("department_store", "Tienda departamental"),
            ("superstore", "Tienda departamental grande"),
            ("b2b_transportation_and_storage_service", "Transporte y almacenamiento (B2B)"),
            ("college_university", "Universidad"),
            ("nightlife_venue", "Vida nocturna"),
        };
    }
    
}

