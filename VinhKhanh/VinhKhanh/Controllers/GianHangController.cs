using Microsoft.AspNetCore.Mvc;
using VinhKhanh.Services;

namespace VinhKhanh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GianHangController : ControllerBase
    {
        private readonly GianHangService _gianHangService;
        private readonly AccountAccessService _accountAccessService;

        public GianHangController(GianHangService gianHangService, AccountAccessService accountAccessService)
        {
            _gianHangService = gianHangService;
            _accountAccessService = accountAccessService;
        }

        public class UpdateMoTaRequest
        {
            public string LanguageCode { get; set; } = "vi";
            public string MoTa { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string lang = "vi")
        {
            var data = await _gianHangService.GetAllAsync(lang);
            return Ok(data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id, [FromQuery] string lang = "vi")
        {
            var data = await _gianHangService.GetByIdAsync(id, lang);
            if (data == null)
                return NotFound(new { message = "Không tìm thấy gian hàng." });

            return Ok(data);
        }

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby(
            [FromQuery] double lat,
            [FromQuery] double lon,
            [FromQuery] double radiusMeters = 100,
            [FromQuery] string lang = "vi")
        {
            var data = await _gianHangService.GetNearbyAsync(lat, lon, radiusMeters, lang);
            return Ok(data);
        }

        [HttpPost("{id}/generate-audio")]
        public async Task<IActionResult> GenerateAudio(int id, [FromQuery] string languageCode = "vi")
        {
            var result = await _gianHangService.GenerateAudioFromMoTaAsync(id, languageCode);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy mô tả gian hàng theo ngôn ngữ yêu cầu."
                });
            }

            return Ok(result);
        }

        [HttpPut("{id}/update-mo-ta")]
        public async Task<IActionResult> UpdateMoTa(int id, [FromQuery] int idTaiKhoan, [FromBody] UpdateMoTaRequest request)
        {
            var isAdmin = await _accountAccessService.IsAdminAsync(idTaiKhoan);
            var ownsStore = !isAdmin && await _accountAccessService.IsStoreOwnedByAccountAsync(idTaiKhoan, id);
            if (!isAdmin && !ownsStore)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Tai khoan khong co quyen cap nhat mo ta gian hang nay."
                });
            }

            if (request == null || string.IsNullOrWhiteSpace(request.MoTa))
            {
                return BadRequest(new
                {
                    message = "Mô tả không hợp lệ."
                });
            }

            var result = await _gianHangService.UpdateMoTaAndGenerateAudioAsync(
                id,
                request.LanguageCode ?? "vi",
                request.MoTa
            );

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy gian hàng hoặc ngôn ngữ cần cập nhật."
                });
            }

            return Ok(result);
        }

        [HttpGet("appdata")]
        public async Task<IActionResult> GetAppData([FromQuery] string lang = "vi")
        {
            var data = await _gianHangService.GetAppDataAsync(lang);
            return Ok(data);
        }
    }
}
