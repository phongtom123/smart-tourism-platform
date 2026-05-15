using Microsoft.AspNetCore.Mvc;
using VinhKhanh.Dtos;
using VinhKhanh.Services;

namespace VinhKhanh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AdminService _adminService;
        private readonly AccountAccessService _accountAccessService;

        public AdminController(AdminService adminService, AccountAccessService accountAccessService)
        {
            _adminService = adminService;
            _accountAccessService = accountAccessService;
        }

        private IActionResult ForbiddenResult()
        {
            return StatusCode(StatusCodes.Status403Forbidden, new OperationResultDto
            {
                Success = false,
                Message = "Tai khoan khong co quyen admin."
            });
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int idTaiKhoan)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await _adminService.GetSummaryAsync());
        }

        [HttpGet("stores")]
        public async Task<IActionResult> GetStores([FromQuery] int idTaiKhoan)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await _adminService.GetStoresAsync());
        }

        [HttpGet("stores/{idGianHang}")]
        public async Task<IActionResult> GetStore(int idGianHang, [FromQuery] int idTaiKhoan, [FromServices] StoreManagementService storeManagementService, [FromQuery] string lang = "vi")
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            var result = await storeManagementService.GetStoreByIdAsync(idGianHang, lang);
            if (result == null)
                return NotFound(new OperationResultDto { Success = false, Message = "Khong tim thay gian hang." });

            return Ok(result);
        }

        [HttpGet("owners")]
        public async Task<IActionResult> GetOwners([FromQuery] int idTaiKhoan)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await _adminService.GetOwnersAsync());
        }

        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts([FromQuery] int idTaiKhoan)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await _adminService.GetAccountsAsync());
        }

        [HttpGet("store-requests")]
        public async Task<IActionResult> GetStoreRequests([FromQuery] int idTaiKhoan, [FromServices] StoreRequestService storeRequestService, [FromQuery] string? status = null)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            try
            {
                return Ok(await storeRequestService.GetRequestsAsync(status));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }
        }

        [HttpPatch("store-requests/{idYeuCau}/review")]
        public async Task<IActionResult> ReviewStoreRequest(int idYeuCau, [FromQuery] int idTaiKhoan, [FromBody] ReviewStoreRequestDto request, [FromServices] StoreRequestService storeRequestService)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            try
            {
                var result = await storeRequestService.ReviewRequestAsync(idYeuCau, idTaiKhoan, request);
                if (result == null)
                    return NotFound(new OperationResultDto { Success = false, Message = "Khong tim thay yeu cau." });

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("accounts")]
        public async Task<IActionResult> CreateAccount([FromQuery] int idTaiKhoan, [FromBody] CreateAdminAccountRequestDto request)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            try
            {
                return Ok(await _adminService.CreateAccountAsync(request));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }
        }

        [HttpPatch("accounts/{targetAccountId}/status")]
        public async Task<IActionResult> UpdateAccountStatus(int targetAccountId, [FromQuery] int idTaiKhoan, [FromBody] UpdateAccountStatusRequestDto request)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            try
            {
                var result = await _adminService.UpdateAccountStatusAsync(idTaiKhoan, targetAccountId, request.TinhTrang);
                if (!result.Success)
                    return NotFound(result);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("service-packages")]
        public async Task<IActionResult> GetServicePackages([FromQuery] int idTaiKhoan)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await _adminService.GetServicePackagesAsync());
        }

        [HttpPost("service-packages")]
        public async Task<IActionResult> CreateServicePackage([FromQuery] int idTaiKhoan, [FromBody] UpsertServicePackageRequestDto request)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            try
            {
                return Ok(await _adminService.CreateServicePackageAsync(request));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }
        }

        [HttpPut("service-packages/{idGoi}")]
        public async Task<IActionResult> UpdateServicePackage(int idGoi, [FromQuery] int idTaiKhoan, [FromBody] UpsertServicePackageRequestDto request)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            try
            {
                var result = await _adminService.UpdateServicePackageAsync(idGoi, request);
                if (result == null)
                    return NotFound(new OperationResultDto { Success = false, Message = "Khong tim thay goi dich vu." });

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }
        }

        [HttpPatch("service-packages/{idGoi}/status")]
        public async Task<IActionResult> UpdateServicePackageStatus(int idGoi, [FromQuery] int idTaiKhoan, [FromBody] UpdateServicePackageStatusRequestDto request)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            try
            {
                var result = await _adminService.UpdateServicePackageStatusAsync(idGoi, request.TrangThai);
                if (!result.Success)
                    return NotFound(result);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("stores")]
        public async Task<IActionResult> CreateStore([FromQuery] int idTaiKhoan, [FromBody] UpsertStoreRequestDto request, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            var ownerId = await ResolveOwnerIdAsync(request);
            if (!ownerId.HasValue)
                return BadRequest(new OperationResultDto { Success = false, Message = "Admin phai chi dinh chu quan ly hop le." });

            try
            {
                request.IdChuQuanLy = ownerId.Value;
                return Ok(await storeManagementService.CreateStoreAsync(request, ownerId.Value));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }
        }

        [HttpPut("stores/{idGianHang}")]
        public async Task<IActionResult> UpdateStore(int idGianHang, [FromQuery] int idTaiKhoan, [FromBody] UpsertStoreRequestDto request, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            var ownerId = await ResolveOwnerIdAsync(request);
            if (ownerId.HasValue)
                request.IdChuQuanLy = ownerId.Value;

            OwnerStoreDto? result;
            try
            {
                result = await storeManagementService.UpdateStoreAsync(idGianHang, request);
                if (result == null)
                    return NotFound(new OperationResultDto { Success = false, Message = "Khong tim thay gian hang." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }

            return Ok(result);
        }

        [HttpPatch("stores/{idGianHang}/status")]
        public async Task<IActionResult> UpdateStoreStatus(int idGianHang, [FromQuery] int idTaiKhoan, [FromBody] UpdateStoreStatusRequestDto request, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            var result = await storeManagementService.UpdateStoreStatusAsync(idGianHang, request.TinhTrang);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        [HttpGet("stores/{idGianHang}/foods")]
        public async Task<IActionResult> GetStoreFoods(int idGianHang, [FromQuery] int idTaiKhoan, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await storeManagementService.GetFoodsByStoreAsync(idGianHang));
        }

        [HttpPost("stores/{idGianHang}/image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadStoreImage(int idGianHang, [FromQuery] int idTaiKhoan, [FromForm] UploadStoreImageRequestDto request, [FromServices] StoreManagementService storeManagementService, [FromServices] IWebHostEnvironment env)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();
            if (request.Image == null || request.Image.Length <= 0)
                return BadRequest(new OperationResultDto { Success = false, Message = "Vui long chon anh hop le." });

            var imagePath = await storeManagementService.SaveStoreImageAsync(idGianHang, request.Image, env);
            if (imagePath == null)
                return NotFound(new OperationResultDto { Success = false, Message = "Khong tim thay gian hang." });

            return Ok(new
            {
                success = true,
                message = "Cap nhat anh gian hang thanh cong.",
                imagePath
            });
        }

        [HttpPost("foods")]
        public async Task<IActionResult> CreateFood([FromQuery] int idTaiKhoan, [FromBody] UpsertFoodRequestDto request, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await storeManagementService.CreateFoodAsync(request));
        }

        [HttpPut("foods/{idMonAn}")]
        public async Task<IActionResult> UpdateFood(int idMonAn, [FromQuery] int idTaiKhoan, [FromBody] UpsertFoodRequestDto request, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            var result = await storeManagementService.UpdateFoodAsync(idMonAn, request);
            if (result == null)
                return NotFound(new OperationResultDto { Success = false, Message = "Khong tim thay mon an." });

            return Ok(result);
        }

        [HttpPatch("foods/{idMonAn}/status")]
        public async Task<IActionResult> UpdateFoodStatus(int idMonAn, [FromQuery] int idTaiKhoan, [FromBody] UpdateFoodStatusRequestDto request, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            var result = await storeManagementService.UpdateFoodStatusAsync(idMonAn, request.TinhTrang);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        [HttpPost("foods/{idMonAn}/image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFoodImage(int idMonAn, [FromQuery] int idTaiKhoan, [FromForm] UploadFoodImageRequestDto request, [FromServices] StoreManagementService storeManagementService, [FromServices] IWebHostEnvironment env)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();
            if (request.Image == null || request.Image.Length <= 0)
                return BadRequest(new OperationResultDto { Success = false, Message = "Vui long chon anh hop le." });

            var imagePath = await storeManagementService.SaveFoodImageAsync(idMonAn, request.Image, env);
            if (imagePath == null)
                return NotFound(new OperationResultDto { Success = false, Message = "Khong tim thay mon an." });

            return Ok(new
            {
                success = true,
                message = "Cap nhat anh mon an thanh cong.",
                imagePath
            });
        }

        [HttpGet("devices")]
        public async Task<IActionResult> GetDevices([FromQuery] int idTaiKhoan, [FromQuery] string? loai = null)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await _adminService.GetDevicesAsync(loai));
        }

        [HttpPatch("devices/{maThietBi}/status")]
        public async Task<IActionResult> UpdateDeviceStatus(string maThietBi, [FromQuery] int idTaiKhoan, [FromBody] UpdateDeviceStatusRequestDto request)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            var result = await _adminService.UpdateDeviceStatusAsync(maThietBi, request.TrangThai);
            if (!result.Success)
                return result.Message.Contains("không tìm thấy", StringComparison.OrdinalIgnoreCase)
                    ? NotFound(result)
                    : BadRequest(result);

            return Ok(result);
        }

        private async Task<int?> ResolveOwnerIdAsync(UpsertStoreRequestDto request)
        {
            if (request.IdChuQuanLy.HasValue && request.IdChuQuanLy.Value > 0)
                return request.IdChuQuanLy.Value;

            if (!string.IsNullOrWhiteSpace(request.EmailChuQuanLy))
                return await _adminService.GetOwnerIdByEmailAsync(request.EmailChuQuanLy);

            return null;
        }

        [HttpGet("poi-map")]
        public async Task<IActionResult> GetPoiMap([FromQuery] int idTaiKhoan, [FromQuery] bool ownerOnly = false)
        {
            if (ownerOnly)
            {
                if (!await _accountAccessService.IsOwnerAsync(idTaiKhoan))
                    return ForbiddenResult();

                return Ok(await _adminService.GetPoiMapAsync(idTaiKhoan));
            }

            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await _adminService.GetPoiMapAsync(null));
        }

        [HttpGet("stores/{idGianHang}/daily-visits")]
        public async Task<IActionResult> GetStoreDailyVisits(int idGianHang, [FromQuery] int idTaiKhoan)
        {
            var canView = await _accountAccessService.IsAdminAsync(idTaiKhoan) ||
                          await _accountAccessService.IsStoreOwnedByAccountAsync(idTaiKhoan, idGianHang);
            if (!canView)
                return ForbiddenResult();

            return Ok(await _adminService.GetStoreDailyVisitsAsync(idGianHang));
        }

        [HttpGet("invoices")]
        public async Task<IActionResult> GetInvoices(
            [FromQuery] int idTaiKhoan,
            [FromQuery] bool ownerOnly,
            [FromServices] InvoiceService invoiceService)
        {
            if (ownerOnly)
            {
                if (!await _accountAccessService.IsOwnerAsync(idTaiKhoan))
                    return ForbiddenResult();
                return Ok(await invoiceService.ListAsync(idTaiKhoan));
            }

            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await invoiceService.ListAsync(null));
        }

        [HttpGet("invoices/{idHoaDon}")]
        public async Task<IActionResult> GetInvoice(
            int idHoaDon,
            [FromQuery] int idTaiKhoan,
            [FromQuery] bool ownerOnly,
            [FromServices] InvoiceService invoiceService)
        {
            int? ownerFilter = null;
            if (ownerOnly)
            {
                if (!await _accountAccessService.IsOwnerAsync(idTaiKhoan))
                    return ForbiddenResult();
                ownerFilter = idTaiKhoan;
            }
            else
            {
                if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                    return ForbiddenResult();
            }

            var invoice = await invoiceService.GetByIdAsync(idHoaDon, ownerFilter);
            if (invoice == null)
                return NotFound(new OperationResultDto { Success = false, Message = "Không tìm thấy hóa đơn." });

            return Ok(invoice);
        }

        [HttpGet("invoices/{idHoaDon}/status")]
        public async Task<IActionResult> GetInvoiceStatus(
            int idHoaDon,
            [FromServices] InvoiceService invoiceService)
        {
            var status = await invoiceService.GetStatusAsync(idHoaDon);
            if (status == null)
                return NotFound(new { status = "error" });

            return Ok(status);
        }

        [HttpPost("invoices/{idHoaDon}/mark-paid")]
        public async Task<IActionResult> MarkInvoicePaid(
            int idHoaDon,
            [FromQuery] int idTaiKhoan,
            [FromServices] InvoiceService invoiceService)
        {
            if (!await _accountAccessService.IsAdminAsync(idTaiKhoan))
                return ForbiddenResult();

            var ok = await invoiceService.MarkPaidAsync(idHoaDon);
            if (!ok)
                return BadRequest(new OperationResultDto { Success = false, Message = "Hóa đơn không tồn tại hoặc đã thanh toán." });

            return Ok(new OperationResultDto { Success = true, Message = "Đã đánh dấu hóa đơn đã thanh toán." });
        }
    }
}
