namespace Core.Entities
{
    public class Product : BaseEntity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string PictureUrl { get; set; }
        public ProductType ProductType { get; set; }
        public int ProductTypeId { get; set; }
        public ProductBrand ProductBrand { get; set; }
        public int ProductBrandId { get; set; }

        // Added for the agent (Phase 2), so it can filter and compare products.

        /// <summary>Main colour, e.g. "Navy". Optional.</summary>
        public string Color { get; set; }

        /// <summary>Main material, e.g. "Gore-Tex". Optional.</summary>
        public string Material { get; set; }

        /// <summary>How many are in stock. 0 means out of stock.</summary>
        public int StockQuantity { get; set; }

        /// <summary>Average customer rating, 0.0 to 5.0.</summary>
        public decimal Rating { get; set; }
    }
}
