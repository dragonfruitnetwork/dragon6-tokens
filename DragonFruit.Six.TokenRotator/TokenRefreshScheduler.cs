// Dragon6 API Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under Apache-2. Refer to the LICENSE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.Six.Api;
using Humanizer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DragonFruit.Six.TokenRotator
{
    public class TokenRefreshScheduler : IHostedService
    {
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _ssf;
        private readonly ITokenStorageMechanism _storage;
        private readonly ILogger<TokenRefreshScheduler> _logger;

        private readonly List<ServiceTokenClient> _clients = new();

        public TokenRefreshScheduler(IConfiguration config, IServiceScopeFactory ssf, ITokenStorageMechanism storage, ILogger<TokenRefreshScheduler> logger)
        {
            _ssf = ssf;
            _config = config;
            _storage = storage;
            _logger = logger;
        }

        async Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            var logins = _config.GetSection("UbisoftAccount")
                                .GetChildren()
                                .SelectMany(UbisoftServiceCredentials.FromConfiguration)
                                .ToList();

            var scheduledCredentials = new List<UbisoftServiceCredentials>();
            var existingTokens = await _storage.GetAllTokens(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Discovered {count} logins and {number} active tokens", logins.Count, existingTokens.Count);

            // for all existing tokens, set them up to refresh as normal
            foreach (var token in existingTokens)
            {
                // locate the service credentials used
                var associatedCredentials = logins.SingleOrDefault(x => token.UbisoftId == x.Id && x.Service.AppId() == token.AppId);

                if (associatedCredentials == null)
                {
                    _logger.LogWarning("Token {id} (from {user}) could not be matched to an account. It will not be refreshed", token.SessionId, token.UbisoftId);
                    continue;
                }

                scheduledCredentials.Add(associatedCredentials);
                var nextRefreshIn = token.Expiry.AddMinutes(-ServiceTokenClient.TokenRefreshPreempt) - DateTime.UtcNow;

                _clients.Add(new ServiceTokenClient(_ssf, associatedCredentials, token));
                _logger.LogInformation("{credential}: Existing token {sessionId} found, next refresh in {x}", associatedCredentials, token.SessionId, nextRefreshIn.Humanize());
            }

            foreach (var serviceGroup in logins.Except(scheduledCredentials).GroupBy(x => x.Service))
            {
                // schedule all remaining credentials at 2~3 minute intervals
                // if there's no pre-existing token for a specific service it gets run instantly
                var interval = scheduledCredentials.Any(x => x.Service == serviceGroup.Key) ? 1 : 0;

                foreach (var credential in serviceGroup)
                {
                    var delay = TimeSpan.FromMinutes(2 * interval++);

                    if (delay > TimeSpan.Zero)
                    {
                        // add a random number of seconds to help space out concurrent requests
                        delay += TimeSpan.FromSeconds(Random.Shared.Next(0, 55));
                    }

                    _clients.Add(new ServiceTokenClient(_ssf, credential, delay));
                    _logger.LogInformation("{name}: first token to be fetched in {number}", credential, delay.Humanize());
                }
            }
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service stop request received. Disabling all active refresh clients.");

            _clients.ForEach(x => x.Dispose());
            _clients.Clear();

            return Task.CompletedTask;
        }
    }
}
