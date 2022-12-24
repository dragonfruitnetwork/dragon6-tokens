// Dragon6 API Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under Apache-2. Refer to the LICENSE file for more info

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DragonFruit.Six.Api.Authentication.Entities;
using Redis.OM;

namespace DragonFruit.Six.TokenRotator.Service
{
    public class RedisTokenStorage : ITokenStorageMechanism
    {
        private readonly IMapper _mapper;
        private readonly RedisConnectionProvider _redis;

        public RedisTokenStorage(IMapper mapper, RedisConnectionProvider redis)
        {
            _mapper = mapper;
            _redis = redis;
        }

        public Task AddToken(UbisoftToken token, CancellationToken cancellation)
        {
            var redisToken = _mapper.Map<RedisServiceToken>(token);
            return _redis.RedisCollection<RedisServiceToken>().InsertAsync(redisToken, WhenKey.Always, token.Expiry - DateTime.UtcNow);
        }

        public async Task RemoveToken(string sessionId, CancellationToken cancellation)
        {
            var collection = _redis.RedisCollection<RedisServiceToken>();
            var token = await collection.FindByIdAsync(sessionId).ConfigureAwait(false);

            if (token != null)
            {
                await collection.DeleteAsync(token).ConfigureAwait(false);
            }
        }

        public async Task<ICollection<IUbisoftAccountToken>> GetAllTokens(CancellationToken cancellation)
        {
            var allItems = await _redis.RedisCollection<RedisServiceToken>().ToListAsync().ConfigureAwait(false);
            return (ICollection<IUbisoftAccountToken>)allItems;
        }
    }
}
