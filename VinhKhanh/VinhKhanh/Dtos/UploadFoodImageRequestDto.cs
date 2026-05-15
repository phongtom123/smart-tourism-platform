using Microsoft.AspNetCore.Http;

namespace VinhKhanh.Dtos
{
    public class UploadFoodImageRequestDto
    {
        public IFormFile? Image { get; set; }
    }
}
