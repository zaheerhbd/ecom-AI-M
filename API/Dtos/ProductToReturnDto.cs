namespace API.Dtos
{
    public class ProductToReturnDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string PictureUrl { get; set; }
        public string ProductType { get; set; }
        public string ProductBrand { get; set; }
        public string Color { get; set; }
        public string Material { get; set; }
        public int StockQuantity { get; set; }
        public decimal Rating { get; set; }
    }
}