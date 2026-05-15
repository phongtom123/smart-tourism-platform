using Microsoft.AspNetCore.Mvc;
using VinhKhanh.Services;

namespace VinhKhanh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PoiController : ControllerBase
    {
        private readonly GianHangService _gianHangService;
        private readonly PoiVisitQueue _visitQueue;

        public PoiController(GianHangService gianHangService, PoiVisitQueue visitQueue)
        {
            _gianHangService = gianHangService;
            _visitQueue = visitQueue;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string lang = "vi")
        {
            var data = await _gianHangService.GetAllAsync(lang);

            var pois = data
                .Where(x => x.Lat.HasValue && x.Lon.HasValue)
                .Select(x => new
                {
                    id = x.IdGianHang,
                    ten = x.Ten,
                    lat = x.Lat,
                    lon = x.Lon
                })
                .ToList();

            return Ok(pois);
        }

        [HttpPost("{id}/visit")]
        public IActionResult RecordVisit(int id)
        {
            var deviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return BadRequest(new { success = false, message = "Thieu header X-Device-Id." });
            }

            // Enqueue fire-and-forget. Worker batch-flush moi 5s (hoac 50 items),
            // dedup ngay van duoc DB dam bao qua bang luot_truy_cap_thiet_bi_ngay.
            var enqueued = _visitQueue.TryEnqueue(id, deviceId);

            return Accepted(new
            {
                success = true,
                queued = enqueued,
                message = enqueued
                    ? "Da xep hang ghi nhan luot truy cap."
                    : "Yeu cau bi loai do dedup cooldown hoac queue day."
            });
        }
    }
}
