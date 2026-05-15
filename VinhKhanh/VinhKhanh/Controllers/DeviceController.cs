using Microsoft.AspNetCore.Mvc;
using VinhKhanh.Dtos;
using VinhKhanh.Services;

namespace VinhKhanh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceController : ControllerBase
    {
        private readonly DeviceService _deviceService;
        private readonly AccessSessionService _accessSessionService;

        public DeviceController(DeviceService deviceService, AccessSessionService accessSessionService)
        {
            _deviceService = deviceService;
            _accessSessionService = accessSessionService;
        }

        [HttpPost("activate")]
        public async Task<IActionResult> Activate([FromBody] ActivateDeviceRequestDto request)
        {
            var result = await _deviceService.ActivateAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("{maThietBi}/status")]
        public async Task<IActionResult> GetStatus(string maThietBi)
        {
            var result = await _deviceService.GetStatusAsync(maThietBi);

            if (!result.Found)
                return NotFound(result);

            return Ok(result);
        }

        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat([FromBody] DeviceHeartbeatRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.MaThietBi) || string.IsNullOrWhiteSpace(request.AccessToken))
            {
                return BadRequest(new DeviceHeartbeatResponseDto
                {
                    Success = false,
                    Message = "Thieu maThietBi hoac accessToken.",
                    ServerTimeUtc = DateTime.UtcNow
                });
            }

            var validation = await _accessSessionService.ValidateAsync(request.AccessToken, request.MaThietBi);
            if (!validation.IsValid)
            {
                return Unauthorized(new DeviceHeartbeatResponseDto
                {
                    Success = false,
                    Message = validation.Message,
                    ServerTimeUtc = DateTime.UtcNow,
                    MustRevalidate = true
                });
            }

            await _deviceService.TouchByCodeAsync(
                request.MaThietBi,
                request.Platform,
                request.Model,
                request.Manufacturer,
                request.AppVersion);

            var mustRevalidate = validation.HetHanLuc.HasValue &&
                                 (validation.HetHanLuc.Value - DateTime.UtcNow) < TimeSpan.FromMinutes(10);

            return Ok(new DeviceHeartbeatResponseDto
            {
                Success = true,
                Message = "ok",
                ServerTimeUtc = DateTime.UtcNow,
                MustRevalidate = mustRevalidate
            });
        }
    }
}
