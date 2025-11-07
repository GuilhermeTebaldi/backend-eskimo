namespace CSharpAssistant.API.DTOs
{
    public class StoreSettingInput
    {
        public string? TimeZone { get; set; }
        public string? OpeningHoursJson { get; set; }
        public string? ExceptionsJson { get; set; }
    }
}
