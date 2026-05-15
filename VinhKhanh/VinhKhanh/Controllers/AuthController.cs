using Microsoft.AspNetCore.Mvc;
using VinhKhanh.Dtos;
using VinhKhanh.Services;

namespace VinhKhanh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _service;

        public AuthController(AuthService service)
        {
            _service = service;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var result = await _service.LoginAsync(request);

            if (!result.Success)
                return Unauthorized(result);

            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterOwnerRequestDto request)
        {
            try
            {
                var result = await _service.RegisterOwnerAsync(request);
                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new OperationResultDto
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
    }
}
