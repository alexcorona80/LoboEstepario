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
            ("land_feature", "Accidente geografico"),
            ("aquarium", "Acuario"),
            ("airport", "Aeropuerto"),
            ("auto_dealer", "Agencia de autos"),
            ("vehicle_dealer", "Agencia de vehiculos"),
            ("travel_service", "Agencia de viajes"),
            ("swimming_pool", "Alberca"),
            ("food_and_drink", "Alimentos y bebidas"),
            ("lodging", "Alojamiento"),
            ("private_lodging", "Alojamiento privado"),
            ("dance_club", "Antro / club nocturno"),
            ("street_art", "Arte urbano"),
            ("arts_and_entertainment", "Arte y entretenimiento"),
            ("specialized_health_care", "Atencion medica especializada"),
            ("science_attraction", "Atraccion cientifica"),
            ("animal_attraction", "Atraccion con animales"),
            ("amusement_attraction", "Atraccion de entretenimiento"),
            ("food_bank", "Banco de alimentos"),
            ("bank_or_credit_union", "Banco o caja de ahorro"),
            ("public_restroom", "Bano publico"),
            ("bar", "Bar"),
            ("library", "Biblioteca"),
            ("storage_facility", "Bodega de almacenamiento"),
            ("forest", "Bosque"),
            ("cafe", "Cafe"),
            ("coffee_shop", "Cafeteria"),
            ("atm", "Cajero automatico"),
            ("campground", "Campamento"),
            ("golf_course", "Campo de golf"),
            ("sport_field", "Cancha deportiva (campo)"),
            ("sport_court", "Cancha deportiva (techada)"),
            ("casino", "Casino"),
            ("civic_center", "Centro civico"),
            ("shopping_mall", "Centro comercial"),
            ("community_center", "Centro comunitario"),
            ("cultural_center", "Centro cultural"),
            ("gaming_venue", "Centro de videojuegos"),
            ("place_of_learning", "Centro educativo"),
            ("specialized_medical_facility", "Centro medico especializado"),
            ("brewery", "Cerveceria"),
            ("movie_theater", "Cine"),
            ("surgery", "Cirugia"),
            ("tutoring_service", "Clases particulares"),
            ("outpatient_care_facility", "Clinica ambulatoria"),
            ("vision_or_eye_care_clinic", "Clinica de la vista"),
            ("behavioral_or_mental_health_clinic", "Clinica de salud mental"),
            ("dental_clinic", "Clinica dental"),
            ("primary_care_or_general_clinic", "Clinica general"),
            ("pediatric_clinic", "Clinica pediatrica"),
            ("comedy_club", "Club de comedia"),
            ("warehouse_club_store", "Club de precios / mayorista"),
            ("sport_or_recreation_club", "Club deportivo o recreativo"),
            ("social_club", "Club social"),
            ("shopping", "Comercio / tienda"),
            ("fast_food_restaurant", "Comida rapida"),
            ("water_utility_provider", "Comision de agua"),
            ("electric_utility_provider", "Comision de electricidad"),
            ("natural_gas_utility_provider", "Comision de gas natural"),
            ("community_and_government", "Comunidad y gobierno"),
            ("condominium", "Condominio"),
            ("building_or_construction_service", "Construccion"),
            ("astrological_advising", "Consulta astrologica"),
            ("psychic_advising", "Consulta esoterica"),
            ("apartment", "Departamento"),
            ("government_department", "Dependencia de gobierno"),
            ("sports_and_recreation", "Deporte y recreacion"),
            ("attorney_or_law_firm", "Despacho de abogados"),
            ("distillery", "Destileria"),
            ("campus_building", "Edificio universitario"),
            ("education", "Educacion"),
            ("embassy", "Embajada"),
            ("geographic_entities", "Entidad geografica"),
            ("adult_entertainment_venue", "Entretenimiento para adultos"),
            ("sport_team", "Equipo deportivo"),
            ("specialty_school", "Escuela especializada"),
            ("sculpture_statue", "Escultura o estatua"),
            ("arts_and_crafts_space", "Espacio de arte y manualidades"),
            ("fire_station", "Estacion de bomberos"),
            ("fueling_station", "Estacion de combustible"),
            ("police_station", "Estacion de policia"),
            ("radio_station", "Estacion de radio"),
            ("train_station", "Estacion de tren"),
            ("parking", "Estacionamiento"),
            ("stadium_arena", "Estadio o arena"),
            ("fitness_studio", "Estudio de fitness"),
            ("manufacturer", "Fabricante"),
            ("pharmacy_and_drug_store", "Farmacia"),
            ("lighthouse", "Faro"),
            ("hardware_home_and_garden_store", "Ferreteria, hogar y jardin"),
            ("flowers_and_gifts_store", "Floreria y regalos"),
            ("performing_arts_venue", "Foro de artes escenicas"),
            ("music_venue", "Foro musical"),
            ("public_fountain", "Fuente publica"),
            ("art_gallery", "Galeria de arte"),
            ("gas_station", "Gasolinera"),
            ("gym", "Gimnasio"),
            ("farm", "Granja"),
            ("hospital", "Hospital"),
            ("bed_and_breakfast", "Hostal con desayuno"),
            ("hotel", "Hotel"),
            ("sport_or_fitness_facility", "Instalacion deportiva"),
            ("educational_facility", "Instalacion educativa"),
            ("industrial_facility_or_service", "Instalacion industrial"),
            ("military_site", "Instalacion militar"),
            ("research_institute", "Instituto de investigacion"),
            ("island", "Isla"),
            ("garden", "Jardin"),
            ("playground", "Juegos infantiles"),
            ("smoothie_juice_bar", "Jugos y licuados"),
            ("courthouse", "Juzgado"),
            ("diagnostics_imaging_or_lab_service", "Laboratorio clinico / imagenologia"),
            ("lake", "Lago"),
            ("laundry_service", "Lavanderia"),
            ("sport_league", "Liga deportiva"),
            ("alcoholic_beverage_venue", "Local de bebidas alcoholicas"),
            ("non_alcoholic_beverage_venue", "Local de bebidas sin alcohol"),
            ("lounge", "Lounge"),
            ("wholesaler", "Mayorista"),
            ("complementary_and_alternative_medicine", "Medicina alternativa"),
            ("market", "Mercado"),
            ("muslim_place_of_worship", "Mezquita"),
            ("mountain", "Montana"),
            ("monument", "Monumento"),
            ("pier", "Muelle"),
            ("museum", "Museo"),
            ("corporate_or_business_office", "Oficina corporativa"),
            ("government_office", "Oficina de gobierno"),
            ("civic_organization", "Organizacion civica"),
            ("youth_organization", "Organizacion juvenil"),
            ("political_organization", "Organizacion politica"),
            ("religious_organization", "Organizacion religiosa"),
            ("shipping_or_delivery_service", "Paqueteria y envios"),
            ("park", "Parque"),
            ("amusement_park", "Parque de diversiones"),
            ("national_park", "Parque nacional"),
            ("rv_park", "Parque para casas rodantes"),
            ("dog_park", "Parque para perros"),
            ("skating_rink", "Pista de patinaje"),
            ("planetarium", "Planetario"),
            ("beach", "Playa"),
            ("food_court", "Plaza de comidas"),
            ("public_plaza", "Plaza publica"),
            ("inn", "Posada"),
            ("preschool", "Preescolar"),
            ("high_school", "Preparatoria"),
            ("elementary_school", "Primaria"),
            ("jail_or_prison", "Prision"),
            ("supplier_or_distributor", "Proveedor o distribuidor"),
            ("bridge", "Puente"),
            ("food_truck_stand", "Puesto de comida (food truck)"),
            ("fairgrounds", "Recinto ferial"),
            ("physical_medicine_and_rehabilitation", "Rehabilitacion fisica"),
            ("recreational_equipment_rental", "Renta de equipo recreativo"),
            ("nature_reserve", "Reserva natural"),
            ("senior_living_facility", "Residencia para adultos mayores"),
            ("resort", "Resort"),
            ("restaurant", "Restaurante"),
            ("casual_eatery", "Restaurante informal"),
            ("river", "Rio"),
            ("rodeo", "Rodeo"),
            ("arcade", "Sala de videojuegos"),
            ("event_venue", "Salon de eventos"),
            ("reproductive_perinatal_and_womens_care", "Salud de la mujer"),
            ("middle_school", "Secundaria"),
            ("recreational_trail_or_path", "Sendero recreativo"),
            ("agricultural_service", "Servicio agricola"),
            ("environmental_or_ecological_service", "Servicio ambiental"),
            ("automotive_service", "Servicio automotriz"),
            ("b2b_science_and_technology_service", "Servicio cientifico y tecnologico (B2B)"),
            ("food_service", "Servicio de alimentos"),
            ("personal_or_beauty_service", "Servicio de belleza o cuidado personal"),
            ("wellness_service", "Servicio de bienestar"),
            ("design_service", "Servicio de diseno"),
            ("b2b_energy_and_utility_service", "Servicio de energia (B2B)"),
            ("event_or_party_service", "Servicio de eventos y fiestas"),
            ("printing_service", "Servicio de impresion"),
            ("media_service", "Servicio de medios"),
            ("b2b_office_and_professional_service", "Servicio de oficina profesional (B2B)"),
            ("rental_service", "Servicio de renta"),
            ("health_care", "Servicio de salud"),
            ("security_service", "Servicio de seguridad"),
            ("housing_or_property_service", "Servicio de vivienda"),
            ("educational_service", "Servicio educativo"),
            ("b2b_service", "Servicio empresarial (B2B)"),
            ("family_service", "Servicio familiar"),
            ("financial_service", "Servicio financiero"),
            ("b2b_industrial_and_machine_service", "Servicio industrial y maquinaria (B2B)"),
            ("real_estate_service", "Servicio inmobiliario"),
            ("legal_service", "Servicio legal"),
            ("medical_service", "Servicio medico"),
            ("home_service", "Servicio para el hogar"),
            ("animal_or_pet_service", "Servicio para mascotas"),
            ("professional_service", "Servicio profesional"),
            ("public_utility", "Servicio publico (utilities)"),
            ("social_or_community_service", "Servicio social o comunitario"),
            ("technical_service", "Servicio tecnico"),
            ("jewish_place_of_worship", "Sinagoga"),
            ("labor_union", "Sindicato"),
            ("historic_site", "Sitio historico"),
            ("skate_park", "Skatepark"),
            ("vehicle_service", "Taller mecanico"),
            ("taxi_or_ride_share_service", "Taxi o app de transporte"),
            ("theatre_venue", "Teatro"),
            ("telecommunications_service", "Telecomunicaciones"),
            ("place_of_worship", "Templo / lugar de culto"),
            ("buddhist_place_of_worship", "Templo budista"),
            ("christian_place_of_worship", "Templo cristiano"),
            ("hindu_place_of_worship", "Templo hindu"),
            ("it_hardware_repair", "TI: equipo de computo y reparacion"),
            ("it_software_services", "TI: software y servicios"),
            ("it_telecom_internet", "TI: telecom, internet y hosting"),
            ("farmers_market", "Tianguis / mercado sobre ruedas"),
            ("food_and_beverage_store", "Tienda de alimentos y bebidas"),
            ("office_supply_store", "Tienda de articulos de oficina"),
            ("sporting_goods_store", "Tienda de articulos deportivos"),
            ("vehicle_parts_store", "Tienda de autopartes"),
            ("personal_care_and_beauty_store", "Tienda de belleza y cuidado personal"),
            ("convenience_store", "Tienda de conveniencia"),
            ("discount_store", "Tienda de descuento"),
            ("electronics_store", "Tienda de electronica"),
            ("musical_instrument_and_pro_audio_store", "Tienda de instrumentos musicales"),
            ("toys_and_games_store", "Tienda de juguetes"),
            ("books_music_and_video_store", "Tienda de libros, musica y video"),
            ("arts_crafts_and_hobby_store", "Tienda de manualidades y pasatiempos"),
            ("animal_and_pet_store", "Tienda de mascotas"),
            ("fashion_and_apparel_store", "Tienda de ropa y moda"),
            ("second_hand_store", "Tienda de segunda mano"),
            ("department_store", "Tienda departamental"),
            ("superstore", "Tienda departamental grande"),
            ("specialty_store", "Tienda especializada"),
            ("air_transport_facility_or_service", "Transporte aereo"),
            ("public_transit_facility_or_service", "Transporte publico"),
            ("b2b_transportation_and_storage_service", "Transporte y almacenamiento (B2B)"),
            ("college_university", "Universidad"),
            ("emergency_department", "Urgencias (hospital)"),
            ("emergency_or_urgent_care_facility", "Urgencias medicas"),
            ("travel_and_transportation", "Viajes y transporte"),
            ("nightlife_venue", "Vida nocturna"),
            ("winery", "Vinicola"),
            ("zoo", "Zoologico"),
        };
    }
    
}

