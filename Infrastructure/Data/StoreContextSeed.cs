using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Core.Entities;
using Core.Entities.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data
{
    /// <summary>
    /// Fills the store database from the JSON files in Data/SeedData.
    ///
    /// It is IDEMPOTENT: safe to run on every start, on an empty database or a full one.
    ///  - Brands, types and products are added only when no row has that Name yet.
    ///  - Existing rows are never changed or deleted (the original 18 products stay as they are).
    ///  - Delivery methods are only added when that table is empty (unchanged behaviour).
    /// See docs/agentic/04-database-and-seed.md §5.
    /// </summary>
    public class StoreContextSeed
    {
        private const int BatchSize = 200;

        public static async Task SeedAsync(StoreContext context, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<StoreContextSeed>();

            try
            {
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                var brands = ReadJson<List<ProductBrand>>(path + @"/Data/SeedData/brands.json");
                var addedBrands = await AddMissingByNameAsync(context, context.ProductBrands, brands, b => b.Name);

                var types = ReadJson<List<ProductType>>(path + @"/Data/SeedData/types.json");
                var addedTypes = await AddMissingByNameAsync(context, context.ProductTypes, types, t => t.Name);

                var products = ReadJson<List<ProductSeedItem>>(path + @"/Data/SeedData/products.json");
                var addedProducts = await AddMissingProductsAsync(context, products, logger);

                if (!context.DeliveryMethods.Any())
                {
                    var methods = ReadJson<List<DeliveryMethod>>(path + @"/Data/SeedData/delivery.json");
                    context.DeliveryMethods.AddRange(methods);
                    await context.SaveChangesAsync();
                }

                logger.LogInformation(
                    "Seed finished: added {Brands} brands, {Types} types, {Products} products.",
                    addedBrands, addedTypes, addedProducts);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Seeding the store database failed.");
            }
        }

        /// <summary>
        /// Adds brands or types whose Name is not in the table yet.
        /// The JSON keeps the original ids (1-6 for brands, 1-4 for types); new items have no id,
        /// so we give them the next free number. We set the id ourselves because the original rows
        /// were also inserted with fixed ids, which leaves the database's own counter behind.
        /// </summary>
        private static async Task<int> AddMissingByNameAsync<T>(StoreContext context, DbSet<T> table, List<T> items, Func<T, string> nameOf)
            where T : BaseEntity
        {
            var existing = await table.ToListAsync();
            var names = new HashSet<string>(existing.Select(nameOf), StringComparer.OrdinalIgnoreCase);
            var usedIds = new HashSet<int>(existing.Select(e => e.Id));
            var nextId = existing.Count == 0 ? 1 : existing.Max(e => e.Id) + 1;
            var added = 0;

            foreach (var item in items)
            {
                if (!names.Add(nameOf(item)))
                    continue; // already there: leave it alone

                if (item.Id == 0 || usedIds.Contains(item.Id))
                {
                    while (usedIds.Contains(nextId)) nextId++;
                    item.Id = nextId;
                }

                usedIds.Add(item.Id);
                table.Add(item);
                added++;
            }

            if (added > 0)
                await context.SaveChangesAsync();

            return added;
        }

        /// <summary>
        /// Adds every product whose Name is not in the table yet. Brand and type are found by NAME,
        /// so the JSON does not depend on which ids this database gave them. Saves in batches of 200.
        /// </summary>
        private static async Task<int> AddMissingProductsAsync(StoreContext context, List<ProductSeedItem> items, ILogger logger)
        {
            var existingNames = new HashSet<string>(
                await context.Products.Select(p => p.Name).ToListAsync(), StringComparer.OrdinalIgnoreCase);

            var brandIds = (await context.ProductBrands.ToListAsync())
                .ToDictionary(b => b.Name, b => b.Id, StringComparer.OrdinalIgnoreCase);
            var typeIds = (await context.ProductTypes.ToListAsync())
                .ToDictionary(t => t.Name, t => t.Id, StringComparer.OrdinalIgnoreCase);

            var added = 0;
            var pending = 0;

            foreach (var item in items)
            {
                if (!existingNames.Add(item.Name))
                    continue; // already there: leave it alone

                if (!brandIds.TryGetValue(item.BrandName ?? "", out var brandId) ||
                    !typeIds.TryGetValue(item.TypeName ?? "", out var typeId))
                {
                    logger.LogWarning("Seed skipped product '{Name}': unknown brand '{Brand}' or type '{Type}'.",
                        item.Name, item.BrandName, item.TypeName);
                    continue;
                }

                context.Products.Add(new Product
                {
                    Name = item.Name,
                    Description = item.Description,
                    Price = item.Price,
                    PictureUrl = item.PictureUrl,
                    ProductBrandId = brandId,
                    ProductTypeId = typeId,
                    Color = item.Color,
                    Material = item.Material,
                    StockQuantity = item.StockQuantity,
                    Rating = item.Rating,
                });
                added++;

                if (++pending == BatchSize)
                {
                    await context.SaveChangesAsync();
                    pending = 0;
                }
            }

            if (pending > 0)
                await context.SaveChangesAsync();

            return added;
        }

        private static T ReadJson<T>(string file)
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(file));
        }
    }

    /// <summary>One product as written in products.json (by tools/seed-gen/generate.js).</summary>
    public class ProductSeedItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string PictureUrl { get; set; }
        public string BrandName { get; set; }
        public string TypeName { get; set; }
        public string Color { get; set; }
        public string Material { get; set; }
        public int StockQuantity { get; set; }
        public decimal Rating { get; set; }
    }
}
