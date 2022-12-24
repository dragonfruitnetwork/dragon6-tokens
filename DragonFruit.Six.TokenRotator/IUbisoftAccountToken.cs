// Dragon6 API Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under Apache-2. Refer to the LICENSE file for more info

using DragonFruit.Six.Api.Authentication.Entities;

namespace DragonFruit.Six.TokenRotator
{
    public interface IUbisoftAccountToken : IUbisoftToken
    {
        public string UbisoftId { get; }
    }
}
