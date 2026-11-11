using System;
using System.ComponentModel;

namespace LoboEstepario
{
    /// <summary>Fila de destinatario (CRM y Redes sociales). Notifica cambios para el DataGrid.</summary>
    public sealed class RecipientRow : INotifyPropertyChanged
    {
        private bool selected;
        private string statusKind = "new";
        private string statusLabel = string.Empty;
        private string statusTip = string.Empty;
        private double rowOpacity = 1.0;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Category { get; set; }
        public string Location { get; set; }
        public DateTime? LastContactedAtUtc { get; set; }
        public int ContactCount { get; set; }
        public string LastCampaignRound { get; set; }

        public bool Selected
        {
            get { return selected; }
            set { if (selected != value) { selected = value; Raise("Selected"); } }
        }

        /// <summary>round | cooldown | previous | new</summary>
        public string StatusKind
        {
            get { return statusKind; }
            set { if (statusKind != value) { statusKind = value; Raise("StatusKind"); } }
        }

        public string StatusLabel
        {
            get { return statusLabel; }
            set { if (statusLabel != value) { statusLabel = value; Raise("StatusLabel"); } }
        }

        public string StatusTip
        {
            get { return statusTip; }
            set { if (statusTip != value) { statusTip = value; Raise("StatusTip"); } }
        }

        public double RowOpacity
        {
            get { return rowOpacity; }
            set { if (rowOpacity != value) { rowOpacity = value; Raise("RowOpacity"); } }
        }

        public bool HasCategory { get { return !string.IsNullOrWhiteSpace(Category); } }

        public bool IsContactedInRound(string round)
        {
            return !string.IsNullOrWhiteSpace(round)
                && !string.IsNullOrWhiteSpace(LastCampaignRound)
                && string.Equals(LastCampaignRound.Trim(), round.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public bool IsInCooldown(int days)
        {
            return days > 0 && LastContactedAtUtc.HasValue && LastContactedAtUtc.Value > DateTime.UtcNow.AddDays(-days);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}

namespace LoboEstepario.Pages
{
    public sealed class CrmRecipientDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string[] Emails { get; set; }
        public string MainCategory { get; set; }
        public string CategoryTag { get; set; }
        public string Locality { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        public DateTime? LastContactedAtUtc { get; set; }
        public int ContactCount { get; set; }
        public string LastCampaignRound { get; set; }

        public string EffectiveCategory
        {
            get { return !string.IsNullOrWhiteSpace(CategoryTag) ? CategoryTag : (MainCategory ?? string.Empty); }
        }
    }

    public sealed class EmailTemplateDto
    {
        public string Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
        public bool IsDefault { get; set; }
    }

    public sealed class CrmHistoryDto
    {
        public string BusinessName { get; set; }
        public string ToEmail { get; set; }
        public string Subject { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public int Status { get; set; }
        public string CampaignRound { get; set; }
        public string Error { get; set; }
    }

    /// <summary>Fila ya formateada del historial.</summary>
    public sealed class CrmHistoryRow
    {
        public string BusinessName { get; set; }
        public string ToEmail { get; set; }
        public string Subject { get; set; }
        public string Round { get; set; }
        public string SentAt { get; set; }
        public bool Failed { get; set; }
        public string StatusText { get; set; }
        public string Error { get; set; }
        public bool HasError { get { return Failed && !string.IsNullOrWhiteSpace(Error); } }
    }

    public sealed class CrmEmailRecipientDto
    {
        public string BusinessId { get; set; }
        public string BusinessName { get; set; }
        public string Category { get; set; }
        public string Location { get; set; }
        public string ToEmail { get; set; }
    }

    public sealed class CrmSendRequestDto
    {
        public SmtpConfigDto Smtp { get; set; }
        public string CampaignRound { get; set; }
        public CrmEmailRecipientDto[] Recipients { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    /// <summary>Elemento de un ComboBox (valor + texto).</summary>
    public sealed class CrmOption
    {
        public string Value { get; set; }
        public string Text { get; set; }

        public override string ToString() { return Text; }
    }
}
