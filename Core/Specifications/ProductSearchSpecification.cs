using Core.Entities;

namespace Core.Specifications
{
    /// <summary>
    /// Finds products for the agent's search_products tool.
    ///
    /// Use it twice: once with paging to get the products, once without paging
    /// (forCount: true) to count ALL matches, so the agent can say "I found 37, here are 5".
    ///
    /// Note: SpecificationEvaluator applies OrderBy OR OrderByDescending, so each sort
    /// below sets only one of them.
    /// </summary>
    public class ProductSearchSpecification : BaseSpecifcation<Product>
    {
        public ProductSearchSpecification(ProductSearchParams p, bool forCount = false) : base(x =>
            (string.IsNullOrEmpty(p.Query)
                || x.Name.ToLower().Contains(p.Query)
                || x.Description.ToLower().Contains(p.Query)) &&
            (!p.BrandId.HasValue || x.ProductBrandId == p.BrandId) &&
            (!p.TypeId.HasValue || x.ProductTypeId == p.TypeId) &&
            (string.IsNullOrEmpty(p.Color) || (x.Color != null && x.Color.ToLower() == p.Color)) &&
            (string.IsNullOrEmpty(p.Material) || (x.Material != null && x.Material.ToLower() == p.Material)) &&
            (!p.MinPrice.HasValue || x.Price >= p.MinPrice) &&
            (!p.MaxPrice.HasValue || x.Price <= p.MaxPrice) &&
            (!p.InStockOnly || x.StockQuantity > 0) &&
            (!p.MinRating.HasValue || x.Rating >= p.MinRating)
        )
        {
            if (forCount)
                return; // counting needs no includes, sorting or paging

            AddInclude(x => x.ProductType);
            AddInclude(x => x.ProductBrand);

            switch (p.Sort)
            {
                case ProductSearchParams.SortPriceAsc:
                    AddOrderBy(x => x.Price);
                    break;
                case ProductSearchParams.SortPriceDesc:
                    AddOrderByDescending(x => x.Price);
                    break;
                case ProductSearchParams.SortNameAsc:
                    AddOrderBy(x => x.Name);
                    break;
                default:
                    // "relevance" (and ratingDesc): best rated first is the most useful default for shoppers.
                    AddOrderByDescending(x => x.Rating);
                    break;
            }

            ApplyPaging(0, p.Limit);
        }
    }
}
