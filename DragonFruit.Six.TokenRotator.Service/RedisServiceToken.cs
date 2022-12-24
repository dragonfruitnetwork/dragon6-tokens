// Dragon6 API Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under Apache-2. Refer to the LICENSE file for more info

using System;
using Redis.OM.Modeling;

namespace DragonFruit.Six.TokenRotator.Service
{
    [Document(IndexName = "dragon6:tokens-idx", Prefixes = new[] { "dragon6:tokens" }, StorageType = StorageType.Json)]
    public class RedisServiceToken : IUbisoftAccountToken
    {
        [RedisIdField]
        public string SessionId { get; set; }

        [Indexed]
        public string AppId { get; set; }

        public string Token { get; set; }

        public DateTime Expiry { get; set; }

        [Indexed]
        public string UbisoftId { get; set; }

        [Indexed(Aggregatable = true, Sortable = true)]
        public int TokenUses { get; set; }
    }
}
