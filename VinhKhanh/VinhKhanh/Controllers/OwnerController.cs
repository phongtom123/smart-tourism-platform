using Microsoft.AspNetCore.Mvc;
using VinhKhanh.Dtos;
using VinhKhanh.Services;

namespace VinhKhanh.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OwnerController : ControllerBase
    {
        private readonly OwnerService _ownerService;
        private readonly AccountAccessService _accountAccessService;

        public OwnerController(OwnerService ownerService, AccountAccessService accountAccessService)
        {
            _ownerService = ownerService;
            _accountAccessService = accountAccessService;
        }

        private IActionResult ForbiddenResult()
        {
            return StatusCode(StatusCodes.Status403Forbidden, new OperationResultDto
            {
                Success = false,
                Message = "Tai khoan khong co quyen chu cua hang."
            });
        }

        [HttpGet("stores")]
        public async Task<IActionResult> GetMyStores([FromQuery] int idTaiKhoan)
        {
            if (!await _accountAccessService.IsOwnerAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await _ownerService.GetStoresByAccountAsync(idTaiKhoan));
        }

        [HttpGet("stores/{idGianHang}")]
        public async Task<IActionResult> GetStore(int idGianHang, [FromQuery] int idTaiKhoan, [FromServices] StoreManagementService storeManagementService, [FromQuery] string lang = "vi")
        {
            if (!await _accountAccessService.IsStoreOwnedByAccountAsync(idTaiKhoan, idGianHang))
                return ForbiddenResult();

            var result = await storeManagementService.GetStoreByIdAsync(idGianHang, lang);
            if (result == null)
                return NotFound(new OperationResultDto { Success = false, Message = "Khong tim thay gian hang." });

            return Ok(result);
        }

        [HttpPost("stores")]
        public async Task<IActionResult> CreateStore([FromQuery] int idTaiKhoan, [FromBody] UpsertStoreRequestDto request, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsOwnerAsync(idTaiKhoan))
                return ForbiddenResult();

            try
            {
                var result = await _ownerService.CreateStoreAsync(idTaiKhoan, request, storeManagementService);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new OperationResultDto { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("store-requests")]
        public async Task<IActionResult> GetStoreRequests([FromQuery] int idTaiKhoan, [FromServices] StoreRequestService storeRequestService)
        {
            if (!await _accountAccessService.IsOwnerAsync(idTaiKhoan))
                return ForbiddenResult();

            return Ok(await storeRequestService.GetRequestsByOwnerAsync(idTaiKhoan));
        }

        [HttpPost("store-requests")]
        public async Task<IActionResult> CreateStoreRequest([FromQuery] int idTaiKhoan, [FromBody] CreateStoreRequestDto request, [FromServices] StoreRequestService storeRequestService)
        {
            if (!await _accountAccessService.IsOwnerAsync(idTaiKhoan))
                return ForbiddenResult();

            try
            {
                return Ok(await storeRequestService.CreateRequestAsync(idTaiKhoan, request));
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

        [HttpPut("stores/{idGianHang}")]
        public async Task<IActionResult> UpdateStore(int idGianHang, [FromQuery] int idTaiKhoan, [FromBody] UpsertStoreRequestDto request, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsStoreOwnedByAccountAsync(idTaiKhoan, idGianHang))
                return ForbiddenResult();

            OwnerStoreDto? result;
            try
            {
                result = await storeManagementService.UpdateStoreByOwnerAsync(idGianHang, idTaiKhoan, request);
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
            if (!await _accountAccessService.IsStoreOwnedByAccountAsync(idTaiKhoan, idGianHang))
                return ForbiddenResult();

            var result = await storeManagementService.UpdateStoreStatusAsync(idGianHang, request.TinhTrang);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        [HttpGet("stores/{idGianHang}/foods")]
        public async Task<IActionResult> GetStoreFoods(int idGianHang, [FromQuery] int idTaiKhoan, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsStoreOwnedByAccountAsync(idTaiKhoan, idGianHang))
                return ForbiddenResult();

            return Ok(await storeManagementService.GetFoodsByStoreAsync(idGianHang));
        }

        [HttpPost("stores/{idGianHang}/image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadStoreImage(int idGianHang, [FromQuery] int idTaiKhoan, [FromForm] UploadStoreImageRequestDto request, [FromServices] StoreManagementService storeManagementService, [FromServices] IWebHostEnvironment env)
        {
            if (!await _accountAccessService.IsStoreOwnedByAccountAsync(idTaiKhoan, idGianHang))
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

        [HttpPost("invoices/{idHoaDon}/bypass-payment")]
        public async Task<IActionResult> BypassInvoicePayment(int idHoaDon, [FromQuery] int idTaiKhoan, [FromServices] InvoiceService invoiceService)
        {
            if (!await _accountAccessService.IsOwnerAsync(idTaiKhoan))
                return ForbiddenResult();

            var invoice = await invoiceService.GetByIdAsync(idHoaDon, idTaiKhoan);
            if (invoice == null)
                return NotFound(new OperationResultDto { Success = false, Message = "Khong tim thay hoa don hoac ban khong co quyen thanh toan hoa don nay." });

            if (!string.Equals(invoice.TrangThai, "chua_thanh_toan", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(invoice.TrangThai, "qua_han", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new OperationResultDto { Success = false, Message = "Hoa don nay khong o trang thai cho thanh toan." });
            }

            var ok = await invoiceService.MarkPaidAsync(idHoaDon);
            if (!ok)
                return BadRequest(new OperationResultDto { Success = false, Message = "Khong the bypass hoa don nay." });

            return Ok(new OperationResultDto { Success = true, Message = "Da bypass thanh toan va kich hoat hoa don gian hang." });
        }

        [HttpPost("foods")]
        public async Task<IActionResult> CreateFood([FromQuery] int idTaiKhoan, [FromBody] UpsertFoodRequestDto request, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsStoreOwnedByAccountAsync(idTaiKhoan, request.IdGianHang))
                return ForbiddenResult();

            return Ok(await storeManagementService.CreateFoodAsync(request));
        }

        [HttpPut("foods/{idMonAn}")]
        public async Task<IActionResult> UpdateFood(int idMonAn, [FromQuery] int idTaiKhoan, [FromBody] UpsertFoodRequestDto request, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsFoodOwnedByAccountAsync(idTaiKhoan, idMonAn))
                return ForbiddenResult();
            if (!await _accountAccessService.IsStoreOwnedByAccountAsync(idTaiKhoan, request.IdGianHang))
                return ForbiddenResult();

            var result = await storeManagementService.UpdateFoodAsync(idMonAn, request);
            if (result == null)
                return NotFound(new OperationResultDto { Success = false, Message = "Khong tim thay mon an." });

            return Ok(result);
        }

        [HttpPatch("foods/{idMonAn}/status")]
        public async Task<IActionResult> UpdateFoodStatus(int idMonAn, [FromQuery] int idTaiKhoan, [FromBody] UpdateFoodStatusRequestDto request, [FromServices] StoreManagementService storeManagementService)
        {
            if (!await _accountAccessService.IsFoodOwnedByAccountAsync(idTaiKhoan, idMonAn))
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
            if (!await _accountAccessService.IsFoodOwnedByAccountAsync(idTaiKhoan, idMonAn))
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
    }
}
