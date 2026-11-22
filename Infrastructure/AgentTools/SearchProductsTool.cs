using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agent;
using Agent.Tools;
using Core.Entities;
using Core.Interfaces;
using Core.Specifications;

namespace Infrastructure.AgentTools
{
    /// <summary>
    /// search_products: finds products with filters and sorting.
    ///
    /// Product questions are mostly FILTERS ("under $40", "in stock", "Gore-Tex boots"), which SQL
    /// answers exactly, so this tool runs a normal database query (no AI search).
    /// Brand and type names are checked against the database first: if the model sends a name we
    /// don't know, the tool answers with the list of valid names, so the model can try again.
    /// </summary>
    public class SearchProductsTool : ITool
    {
        private const int MaxLimit = 10;
        private const int DefaultLimit = 5;

        private static readonly string[] SortValues =
        {
            ProductSearchParams.SortRelevance,
            ProductSearchParams.SortPriceAsc,
            ProductSearchParams.SortPriceDesc,
            ProductSearchParams.SortRatingDesc,
            ProductSearchParams.SortNameAsc
        };

        private readonly IGenericRepository<Product> _products;
        private readonly IGenericRepository<ProductBrand> _brands;
        private readonly IGenericRepository<ProductType> _types;

        public SearchProductsTool(IGenericRepository<Product> products, IGenericRepository<ProductBrand> brands,
            IGenericRepository<ProductType> types)
        {
            _products = products;
            _brands = brands;
            _types = types;
        }

        public string Name => "search_products";

        public string Description =>
            "Search the shop's products. Use it for any question about what we sell, prices, colours, materials, " +
            "brands, ratings or stock. All filters are optional; combine them as needed. " +
            "Returns the total number of matches and up to 'limit' products. " +
            "Use list_catalog_filters first if you are not sure which brands, colours or materials exist.";

        public JsonObject ParametersSchema => Schema.Object(
            new ToolParameter("query", "Words to find in the product name or description, e.g. 'storm'. Leave out to match everything.", false),
            new ToolParameter("type", "Product type.", false, ToolParameterTypes.String,
                new[] { "Boards", "Hats", "Boots", "Gloves" }),
            new ToolParameter("brand", "Brand name, e.g. 'Polar Path'.", false),
            new ToolParameter("color", "Main colour, e.g. 'Navy'.", false),
            new ToolParameter("material", "Main material, e.g. 'Gore-Tex'.", false),
            new ToolParameter("minPrice", "Lowest price in US dollars.", false, ToolParameterTypes.Number),
            new ToolParameter("maxPrice", "Highest price in US dollars, e.g. 40 for 'under $40'.", false, ToolParameterTypes.Number),
            new ToolParameter("inStockOnly", "True to show only products that are in stock.", false, ToolParameterTypes.Boolean),
            new ToolParameter("minRating", "Lowest customer rating, 0 to 5.", false, ToolParameterTypes.Number),
            new ToolParameter("sort", "Order of results. 'relevance' shows the best rated first.", false, ToolParameterTypes.String, SortValues),
            new ToolParameter("limit", "How many products to return, 1 to 10. Default 5.", false, ToolParameterTypes.Integer));

        public async Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            var search = new ProductSearchParams
            {
                Query = Clean(arguments.GetString("query")),
                Color = Clean(arguments.GetString("color")),
                Material = Clean(arguments.GetString("material")),
                InStockOnly = arguments.GetBool("inStockOnly") ?? false,
                Sort = arguments.GetString("sort"),
                Limit = Math.Min(Math.Max(arguments.GetInt("limit") ?? DefaultLimit, 1), MaxLimit)
            };

            double? minPrice = arguments.GetNumber("minPrice");
            double? maxPrice = arguments.GetNumber("maxPrice");
            double? minRating = arguments.GetNumber("minRating");
            if (minPrice.HasValue) search.MinPrice = (decimal)minPrice.Value;
            if (maxPrice.HasValue) search.MaxPrice = (decimal)maxPrice.Value;
            if (minRating.HasValue) search.MinRating = (decimal)minRating.Value;

            // ---- Brand and type: turn names into IDs, or explain which names exist ----
            string brandName = arguments.GetString("brand");
            if (!string.IsNullOrWhiteSpace(brandName))
            {
                IReadOnlyList<ProductBrand> brands = await _brands.ListAllAsync();
                string match = ToolHelpers.FindName(brands.Select(b => b.Name), brandName);
                if (match == null)
                    return "Error: brand '" + brandName + "' not found. Known brands: " + ToolHelpers.JoinNames(brands.Select(b => b.Name));
                search.BrandId = brands.First(b => b.Name == match).Id;
            }

            string typeName = arguments.GetString("type");
            if (!string.IsNullOrWhiteSpace(typeName))
            {
                IReadOnlyList<ProductType> types = await _types.ListAllAsync();
                string match = ToolHelpers.FindName(types.Select(t => t.Name), typeName);
                if (match == null)
                    return "Error: type '" + typeName + "' not found. Known types: " + ToolHelpers.JoinNames(types.Select(t => t.Name));
                search.TypeId = types.First(t => t.Name == match).Id;
            }

            // ---- Run the query: one count, one page ----
            int total = await _products.CountAsync(new ProductSearchSpecification(search, forCount: true));
            IReadOnlyList<Product> products = await _products.ListAsync(new ProductSearchSpecification(search));

            var items = new List<object>();
            foreach (Product p in products)
            {
                context.AddSource("product", p.Id.ToString(), p.Name);
                items.Add(new
                {
                    id = p.Id,
                    name = p.Name,
                    brand = p.ProductBrand?.Name,
                    type = p.ProductType?.Name,
                    price = p.Price,
                    color = p.Color,
                    material = p.Material,
                    inStock = p.StockQuantity > 0,
                    rating = p.Rating
                });
            }

            return ToolHelpers.ToJson(new { total, returned = items.Count, items });
        }

        /// <summary>Trims and lower-cases a text filter. Empty text means "no filter".</summary>
        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
        }
    }
}
