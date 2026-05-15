using Microsoft.AspNetCore.Mvc;
using VinhKhanh.Dtos;
using VinhKhanh.Services;

namespace VinhKhanh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TourController : ControllerBase
    {
        private readonly TourService _tourService;
        private readonly AccountAccessService _accountAccessService;
        private readonly GoogleDirectionsService _directionsService;

        public TourController(
            TourService tourService,
            AccountAccessService accountAccessService,
            GoogleDirectionsService directionsService)
        {
            _tourService = tourService;
            _accountAccessService = accountAccessService;
            _directionsService = directionsService;
        }

        private IActionResult ForbiddenResult() =>
            StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Khong co quyen truy cap." });

        [HttpGet]
        public async Task<IActionResult> GetActive([FromQuery] int? lang = null, CancellationToken ct = default)
        {
            var tours = await _tourService.GetActiveToursAsync(lang, ct);
            return Ok(tours);
        }

        [HttpGet("route")]
        public async Task<IActionResult> GetRoute(
            [FromQuery] double fromLat,
            [FromQuery] double fromLon,
            [FromQuery] double toLat,
            [FromQuery] double toLon,
            CancellationToken ct = default)
        {
            if (!IsValidCoordinate(fromLat, fromLon) || !IsValidCoordinate(toLat, toLon))
                return BadRequest(new { success = false, message = "Toa do khong hop le." });

            var route = await _directionsService.GetWalkingRouteAsync(fromLat, fromLon, toLat, toLon, ct);
            return Ok(route);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetDetail(int id, CancellationToken ct = default)
        {
            var detail = await _tourService.GetTourDetailAsync(id, ct);
            if (detail is null)
                return NotFound(new { success = false, message = "Khong tim thay tour." });
            return Ok(detail);
        }

        [HttpGet("{id:int}/progress")]
        public async Task<IActionResult> GetProgress(int id, CancellationToken ct = default)
        {
            var deviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(deviceId))
                return BadRequest(new { success = false, message = "Thieu header X-Device-Id." });

            var tienDo = await _tourService.GetTienDoAsync(id, deviceId, ct);
            return Ok(tienDo ?? new TourTienDoDto { IdTour = id, MaThietBi = deviceId, StepHienTai = 0 });
        }

        [HttpPost("{id:int}/advance")]
        public async Task<IActionResult> Advance(int id, [FromBody] AdvanceTourRequestDto body, CancellationToken ct = default)
        {
            var deviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(deviceId))
                return BadRequest(new { success = false, message = "Thieu header X-Device-Id." });

            if (body is null || body.IdGianHangVuaDen <= 0)
                return BadRequest(new { success = false, message = "Thieu idGianHangVuaDen." });

            var result = await _tourService.AdvanceAsync(id, deviceId, body.IdGianHangVuaDen, ct);
            return Ok(result);
        }

        // ===== Admin-only endpoints =====
        // Yeu cau idTaiKhoan tu query string + check IsAdminAsync, theo dung pattern AdminController.

        [HttpGet("/api/admin/tour")]
        public async Task<IActionResult> AdminGetAll([FromQuery] int idTaiKhoan, CancellationToken ct = default)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();
            var list = await _tourService.GetAllForAdminAsync(ct);
            return Ok(list);
        }

        [HttpGet("/api/admin/tour/{id:int}")]
        public async Task<IActionResult> AdminGetDetail(int id, [FromQuery] int idTaiKhoan, CancellationToken ct = default)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();
            var detail = await _tourService.GetTourDetailAsync(id, ct);
            if (detail is null)
                return NotFound(new { success = false, message = "Khong tim thay tour." });
            return Ok(detail);
        }

        [HttpPost("/api/admin/tour")]
        public async Task<IActionResult> AdminCreate([FromQuery] int idTaiKhoan, [FromBody] UpsertTourRequestDto body, CancellationToken ct = default)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();
            if (body is null)
                return BadRequest(new { success = false, message = "Thieu du lieu." });
            var result = await _tourService.CreateAsync(body, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("/api/admin/tour/{id:int}")]
        public async Task<IActionResult> AdminUpdate(int id, [FromQuery] int idTaiKhoan, [FromBody] UpsertTourRequestDto body, CancellationToken ct = default)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();
            if (body is null)
                return BadRequest(new { success = false, message = "Thieu du lieu." });
            var result = await _tourService.UpdateAsync(id, body, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("/api/admin/tour/{id:int}")]
        public async Task<IActionResult> AdminDelete(int id, [FromQuery] int idTaiKhoan, CancellationToken ct = default)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();
            var result = await _tourService.DeleteAsync(id, ct);
            return result.Success ? Ok(result) : NotFound(result);
        }

        private static bool IsValidCoordinate(double lat, double lon)
        {
            return !double.IsNaN(lat) &&
                   !double.IsNaN(lon) &&
                   !double.IsInfinity(lat) &&
                   !double.IsInfinity(lon) &&
                   lat is >= -90 and <= 90 &&
                   lon is >= -180 and <= 180;
        }
    }
}
