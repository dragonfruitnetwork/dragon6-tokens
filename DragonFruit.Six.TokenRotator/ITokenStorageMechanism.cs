// Dragon6 API Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under Apache-2. Refer to the LICENSE file for more info

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.Six.Api.Authentication.Entities;

namespace DragonFruit.Six.TokenRotator
{
    public interface ITokenStorageMechanism
    {
        Task AddToken(UbisoftToken token, CancellationToken cancellation);
        Task RemoveToken(string sessionId, CancellationToken cancellation);

        Task<ICollection<IUbisoftAccountToken>> GetTokens(IEnumerable<string> ubisoftIds, CancellationToken cancellation);
    }
}
