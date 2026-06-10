using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using Core.Entities;
using Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Data
{
    public class BasketRepository : IBasketRepository
    {
        private readonly IMemoryCache _cache;
        private static readonly ConcurrentDictionary<string, byte> BasketKeys = new ConcurrentDictionary<string, byte>();

        public BasketRepository(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<bool> DeleteBasketAsync(string basketId)
        {
            BasketKeys.TryRemove(basketId, out _);
            _cache.Remove(GetCacheKey(basketId));
            return await Task.FromResult(true);
        }

        public async Task<CustomerBasket> GetBasketAsync(string basketId)
        {
            _cache.TryGetValue(GetCacheKey(basketId), out string data);

            return data.IsNullOrEmpty() ? null : JsonSerializer.Deserialize<CustomerBasket>(data);
        }

        public async Task<CustomerBasket> UpdateBasketAsync(CustomerBasket basket)
        {
            BasketKeys[basket.Id] = 0;
            _cache.Set(GetCacheKey(basket.Id), JsonSerializer.Serialize(basket), TimeSpan.FromDays(30));

            return await GetBasketAsync(basket.Id);
        }

        private static string GetCacheKey(string basketId)
        {
            return $"basket:{basketId}";
        }
    }
}
