using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agent;
using Agent.Tools;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.AgentTools
{
    /// <summary>
    /// list_catalog_filters: which brands, types, colours and materials exist, and the price
    /// range of each type. The model uses it to build a correct search_products call, or to
    /// answer "what brands do you have?".
    ///
    /// It reads StoreContext directly because it needs GROUP BY queries, which the generic
    /// repository doesn't offer.
    /// </summary>
    public class ListCatalogFiltersTool : ITool
    {
        private readonly StoreContext _db;

        public ListCatalogFiltersTool(StoreContext db)
        {
            _db = db;
        }

        public string Name => "list_catalog_filters";

        public string Description =>
            "List the brands, product types, colours and materials the shop has, plus the price range of each type. " +
            "Use it to answer 'what do you sell?' questions, or to check a filter value before calling search_products.";

        public JsonObject ParametersSchema => Schema.Object();

        public async Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            var brands = await _db.ProductBrands.AsNoTracking()
                .OrderBy(b => b.Name).Select(b => b.Name).ToListAsync(cancellationToken);

            var colors = await _db.Products.AsNoTracking()
                .Where(p => p.Color != null).Select(p => p.Color).Distinct().OrderBy(c => c).ToListAsync(cancellationToken);

            var materials = await _db.Products.AsNoTracking()
                .Where(p => p.Material != null).Select(p => p.Material).Distinct().OrderBy(m => m).ToListAsync(cancellationToken);

            var types = await _db.Products.AsNoTracking()
                .GroupBy(p => p.ProductType.Name)
                .Select(g => new
                {
                    name = g.Key,
                    productCount = g.Count(),
                    minPrice = g.Min(p => p.Price),
                    maxPrice = g.Max(p => p.Price)
                })
                .OrderBy(t => t.name)
                .ToListAsync(cancellationToken);

            return ToolHelpers.ToJson(new { types, brands, colors, materials });
        }
    }
}
