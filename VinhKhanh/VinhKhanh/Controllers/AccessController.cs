using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VinhKhanh.Dtos;
using VinhKhanh.Services;

namespace VinhKhanh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccessController : ControllerBase
    {
        private readonly AccessSessionService _accessSessionService;

        public AccessController(AccessSessionService accessSessionService)
        {
            _accessSessionService = accessSessionService;
        }

        [HttpPost("scan")]
        public async Task<IActionResult> Scan([FromBody] ScanQrRequestDto request)
        {
            var result = await _accessSessionService.CreateFromQrAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("validate")]
        public async Task<IActionResult> Validate([FromBody] ValidateAccessRequestDto request)
        {
            var result = await _accessSessionService.ValidateAsync(request.AccessToken ?? string.Empty, request.ClientDeviceId);
            return Ok(result);
        }

        [HttpPost("token/activate")]
        public async Task<IActionResult> ActivateToken([FromBody] ActivateAccessTokenRequestDto request)
        {
            var result = await _accessSessionService.ActivateTokenAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("packages")]
        public async Task<IActionResult> GetPackages()
        {
            return Ok(await _accessSessionService.GetActiveServicePackagesAsync());
        }

        [HttpPost("package/register")]
        public async Task<IActionResult> RegisterPackage([FromBody] RegisterPackageAccessRequestDto request)
        {
            var result = await _accessSessionService.RegisterPackageAccessAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("package/payment")]
        public async Task<IActionResult> CreatePackagePayment([FromBody] CreatePackagePaymentRequestDto request)
        {
            var result = await _accessSessionService.CreatePackagePaymentAsync(request);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("package/payment/{paymentReference}")]
        public async Task<IActionResult> GetPackagePaymentStatus([FromRoute] string paymentReference)
        {
            var result = await _accessSessionService.GetPackagePaymentStatusAsync(paymentReference);

            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        [HttpPost("casso/webhook")]
        [HttpPost("~/api/webhook/casso")]
        public async Task<IActionResult> CassoWebhook([FromBody] JsonElement payload)
        {
            var securityKey = ResolveCassoSecurityKey();
            if (!_accessSessionService.IsValidCassoSecurityKey(securityKey))
                return Unauthorized(new { success = false, message = "Casso security key khong hop le." });

            var result = await _accessSessionService.HandleCassoWebhookAsync(payload);
            return Ok(new
            {
                success = result.Success,
                message = result.Message,
                processed = result.Processed,
                activated = result.Activated,
                ignored = result.Ignored
            });
        }

        private string? ResolveCassoSecurityKey()
        {
            foreach (var headerName in new[]
            {
                "secure-token",
                "Secure-Token",
                "x-casso-secure-token",
                "X-Casso-Secure-Token",
                "x-api-key",
                "X-Api-Key"
            })
            {
                if (Request.Headers.TryGetValue(headerName, out var value) &&
                    !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    return value.ToString();
                }
            }

            var authorization = Request.Headers.Authorization.ToString();
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return authorization["Bearer ".Length..].Trim();

            return null;
        }
    }
}
