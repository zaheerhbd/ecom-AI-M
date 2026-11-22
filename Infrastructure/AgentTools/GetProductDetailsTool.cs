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
    /// get_product_details: everything about ONE product, including its description.
    /// search_products leaves the description out to keep its results small.
    /// </summary>
    public class GetProductDetailsTool : ITool
    {
        private const int MaxDescriptionLength = 800;

        private readonly IGenericRepository<Product> _products;

        public GetProductDetailsTool(IGenericRepository<Product> products)
        {
            _products = products;
        }

        public string Name => "get_product_details";

        public string Description =>
            "Get the full details of one product, including its description and stock level. " +
            "Use it when the user asks about a specific product you found with search_products.";

        public JsonObject ParametersSchema => Schema.Object(
            new ToolParameter("productId", "The product's id, from search_products.", true, ToolParameterTypes.Integer));

        public async Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            int? id = arguments.GetInt("productId");
            if (!id.HasValue || id.Value < 1)
                return "Error: productId must be a whole number, e.g. 12. Use search_products to find ids.";

            Product p = await _products.GetEntityWithSpec(new ProductsWithTypesAndBrandsSpecification(id.Value));
            if (p == null)
                return "Error: there is no product with id " + id.Value + ".";

            context.AddSource("product", p.Id.ToString(), p.Name);

            return ToolHelpers.ToJson(new
            {
                id = p.Id,
                name = p.Name,
                description = ToolHelpers.Shorten(p.Description, MaxDescriptionLength),
                brand = p.ProductBrand?.Name,
                type = p.ProductType?.Name,
                price = p.Price,
                color = p.Color,
                material = p.Material,
                stockQuantity = p.StockQuantity,
                inStock = p.StockQuantity > 0,
                rating = p.Rating
            });
        }
    }
}
