namespace VinhKhanh.Dtos
{
    public class UpsertFoodRequestDto
    {
        public int IdGianHang { get; set; }
        public string Ten { get; set; } = string.Empty;
        public decimal DonGia { get; set; }
        public string TinhTrang { get; set; } = "con_ban";
    }
}
