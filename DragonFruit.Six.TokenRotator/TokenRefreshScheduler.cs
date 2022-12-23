// Dragon6 API Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under Apache-2. Refer to the LICENSE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.Six.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DragonFruit.Six.TokenRotator
{
    public class TokenRefreshScheduler : IHostedService
    {
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _ssf;
        private readonly ITokenStorageMechanism _storage;
        private readonly List<ServiceTokenClient> _clients = new();

        public TokenRefreshScheduler(IConfiguration config, IServiceScopeFactory ssf, ITokenStorageMechanism storage)
        {
            _ssf = ssf;
            _config = config;
            _storage = storage;
        }

        async Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            var logins = _config.GetSection("UbisoftAccount")
                                .GetChildren()
                                .SelectMany(UbisoftServiceCredentials.FromConfiguration)
                                .ToList();

            var scheduledCredentials = new List<UbisoftServiceCredentials>();
            var existingTokens = await _storage.GetAllTokens().ConfigureAwait(false);

            // for all existing tokens, set them up to refresh as normal
            foreach (var token in existingTokens)
            {
                // locate the service credentials used
                var associatedCredentials = logins.SingleOrDefault(x => token.UbisoftId == x.Id && x.Service.AppId() == token.AppId);

                if (associatedCredentials == null)
                {
                    continue;
                }

                scheduledCredentials.Add(associatedCredentials);
                _clients.Add(new ServiceTokenClient(_ssf, associatedCredentials, token));
            }

            // schedule all remaining credentials at 2 minute intervals
            // if there's no pre-existing token for a specific service it gets run instantly
            var interval = 1;

            foreach (var credential in logins.Except(scheduledCredentials))
            {
                var isPreExistingToken = existingTokens.Any(x => x.AppId == credential.Service.AppId());
                var delay = isPreExistingToken ? TimeSpan.FromMinutes(2 * interval++) : TimeSpan.Zero;

                _clients.Add(new ServiceTokenClient(_ssf, credential, delay));
            }
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            _clients.ForEach(x => x.Dispose());
            _clients.Clear();

            return Task.CompletedTask;
        }
    }
}
