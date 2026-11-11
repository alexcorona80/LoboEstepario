namespace LoboEstepario
{
    public sealed class Company
    {
        public Company(string name, string industry, string location, string contact, string source, string country = null, string region = null, string city = null, string email = null, string phone = null)
        {
            Name = name; Industry = industry; Location = location; Contact = contact; Source = source; Country = country; Region = region; City = city;
            Email = email; Phone = phone;
        }
        public string Name { get; private set; }
        public string Industry { get; private set; }
        public string Location { get; private set; }
        public string Contact { get; private set; }
        public string Source { get; private set; }
        public string Country { get; private set; }
        public string Region { get; private set; }
        public string City { get; private set; }
        public string Email { get; private set; }
        public string Phone { get; private set; }
        public bool HasEmail => !string.IsNullOrWhiteSpace(Email);
        public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
    }

    public sealed class PlacesResponse
    {
        public string ReleaseVersion { get; set; }
        public int Count { get; set; }
        public PlaceDto[] Places { get; set; }
    }

    public sealed class PlaceDto
    {
        public string Name { get; set; }
        public string MainCategory { get; set; }
        public string DetailedCategory { get; set; }
        public string Locality { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        public string[] Emails { get; set; }
        public string[] Phones { get; set; }
        public string[] Websites { get; set; }
    }

    public sealed class ScrapeResultDto
    {
        public string ReleaseVersion { get; set; }
        public int FetchedCount { get; set; }
        public long PersistedCount { get; set; }
    }

    public sealed class BusinessStatsDto
    {
        public long TotalCount { get; set; }
        public long WithContactCount { get; set; }
        public int ActiveSourceCount { get; set; }
        public string LastScrapedAtUtc { get; set; }
    }

    public sealed class BusinessRecipientDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string[] Emails { get; set; }
        public string Locality { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
    }

    public sealed class SmtpConfigDto
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FromName { get; set; }
        public bool EnableSsl { get; set; }
    }

    public sealed class TestEmailRequestDto
    {
        public SmtpConfigDto Smtp { get; set; }
        public string ToEmail { get; set; }
    }

    public sealed class EmailRecipientDto
    {
        public string BusinessId { get; set; }
        public string BusinessName { get; set; }
        public string ToEmail { get; set; }
    }

    public sealed class SendEmailRequestDto
    {
        public SmtpConfigDto Smtp { get; set; }
        public EmailRecipientDto[] Recipients { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    public sealed class EmailTestResponseDto
    {
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public sealed class EmailSendResponseDto
    {
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
    }

    public sealed class SentEmailDto
    {
        public string BusinessName { get; set; }
        public string ToEmail { get; set; }
        public string Subject { get; set; }
        public string SentAtUtc { get; set; }
        public int Status { get; set; }
        public string Error { get; set; }
    }

    public sealed class EmailStatsDto
    {
        public long SentCount { get; set; }
        public long FailedCount { get; set; }
        public long BusinessesWithEmailCount { get; set; }
        public string LastSentAtUtc { get; set; }
    }
}
