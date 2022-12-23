// Dragon6 API Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under Apache-2. Refer to the LICENSE file for more info

using System.Collections.Generic;
using System.Threading.Tasks;
using DragonFruit.Six.Api.Authentication.Entities;

namespace DragonFruit.Six.TokenRotator
{
    public interface ITokenStorageMechanism
    {
        Task AddToken(UbisoftToken token);
        Task RemoveToken(string sessionId);

        Task<IReadOnlyCollection<UbisoftToken>> GetAllTokens();
    }
}
