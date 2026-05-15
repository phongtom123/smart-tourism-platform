using Microsoft.AspNetCore.Http;

namespace VinhKhanh.Dtos
{
    public class UploadStoreImageRequestDto
    {
        public IFormFile? Image { get; set; }
    }
}
