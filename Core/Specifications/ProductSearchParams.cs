namespace Core.Specifications
{
    /// <summary>
    /// The filters the agent's search_products tool can use (Phase 3).
    /// Every filter is optional: leave it null and it is not applied.
    /// Brand and type are IDs here: the tool turns names like "Polar Path" into IDs first.
    /// </summary>
    public class ProductSearchParams
    {
        /// <summary>Allowed values for Sort.</summary>
        public const string SortRelevance = "relevance";
        public const string SortPriceAsc = "priceAsc";
        public const string SortPriceDesc = "priceDesc";
        public const string SortRatingDesc = "ratingDesc";
        public const string SortNameAsc = "nameAsc";

        /// <summary>Words to find in the name or description, e.g. "storm". Lower case.</summary>
        public string Query { get; set; }

        public int? BrandId { get; set; }
        public int? TypeId { get; set; }

        /// <summary>Colour, lower case, e.g. "navy".</summary>
        public string Color { get; set; }

        /// <summary>Material, lower case, e.g. "gore-tex".</summary>
        public string Material { get; set; }

        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        /// <summary>True = only products with stock left.</summary>
        public bool InStockOnly { get; set; }

        public decimal? MinRating { get; set; }

        /// <summary>One of the Sort... values above. Anything else means "relevance".</summary>
        public string Sort { get; set; }

        /// <summary>How many products to return (the tool keeps it between 1 and 10).</summary>
        public int Limit { get; set; } = 5;
    }
}
