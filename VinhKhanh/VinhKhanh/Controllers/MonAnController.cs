using Microsoft.AspNetCore.Mvc;
using VinhKhanh.Services;

namespace VinhKhanh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MonAnController : ControllerBase
    {
        private readonly MonAnService _service;

        public MonAnController(MonAnService service)
        {
            _service = service;
        }

        [HttpGet("by-gianhang/{idGianHang}")]
        public async Task<IActionResult> GetByGianHang(int idGianHang, [FromQuery] string lang = "vi")
        {
            var data = await _service.GetByGianHangAsync(idGianHang, lang);
            return Ok(data);
        }
    }
}