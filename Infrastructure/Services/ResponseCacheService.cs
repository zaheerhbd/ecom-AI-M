using System;
using System.Text.Json;
using System.Threading.Tasks;
using Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services
{
    public class ResponseCacheService : IResponseCacheService
    {
        private readonly IMemoryCache _cache;

        public ResponseCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task CacheResponseAsync(string cacheKey, object response, TimeSpan timeToLive)
        {
            if (response == null) return;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var serialisedResponse = JsonSerializer.Serialize(response, options);

            _cache.Set(cacheKey, serialisedResponse, timeToLive);
            await Task.CompletedTask;
        }

        public async Task<string> GetCachedResponse(string cacheKey)
        {
            _cache.TryGetValue(cacheKey, out string cachedResponse);
            await Task.CompletedTask;
            return cachedResponse;
        }
    }
}
