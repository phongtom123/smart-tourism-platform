namespace VinhKhanh.Dtos
{
    public class DeviceHeartbeatRequestDto
    {
        public string? MaThietBi { get; set; }
        public string? AccessToken { get; set; }
        public string? Platform { get; set; }
        public string? Model { get; set; }
        public string? Manufacturer { get; set; }
        public string? AppVersion { get; set; }
    }

    public class DeviceHeartbeatResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime ServerTimeUtc { get; set; }
        public bool MustRevalidate { get; set; }
    }

    public class ValidateAccessRequestDto
    {
        public string? AccessToken { get; set; }
        public string? ClientDeviceId { get; set; }
    }
}
