﻿// Dragon6 API Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under Apache-2. Refer to the LICENSE file for more info

using System;
using System.Diagnostics;
using System.Threading;
using DragonFruit.Data;
using DragonFruit.Six.Api.Authentication;
using DragonFruit.Six.Api.Authentication.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace DragonFruit.Six.TokenRotator
{
    public class ServiceTokenClient : IDisposable
    {
        private const int TokenRefreshPreempt = 30;

        private readonly Timer _timer;
        private readonly IServiceScopeFactory _ssf;

        public ServiceTokenClient(IServiceScopeFactory ssf, UbisoftServiceCredentials credentials, IUbisoftToken token)
        {
            Credentials = credentials;
            LastTokenSessionId = token.SessionId;

            _ssf = ssf;
            _timer = new Timer(FetchNewToken, null, token.Expiry - DateTime.UtcNow.AddMinutes(TokenRefreshPreempt), Timeout.InfiniteTimeSpan);
        }

        public ServiceTokenClient(IServiceScopeFactory ssf, UbisoftServiceCredentials credentials, TimeSpan delay)
        {
            Credentials = credentials;

            _ssf = ssf;
            _timer = new Timer(FetchNewToken, null, delay, Timeout.InfiniteTimeSpan);
        }

        public UbisoftServiceCredentials Credentials { get; }

        public string LastTokenSessionId { get; private set; }

        private async void FetchNewToken(object state)
        {
            using var scope = _ssf.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<ApiClient>();
            var storage = scope.ServiceProvider.GetRequiredService<ITokenStorageMechanism>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ServiceTokenClient>>();

            var ubisoftToken = await Policy.Handle<Exception>()
                                           .WaitAndRetryForeverAsync(a => TimeSpan.FromSeconds(Math.Max(5 * a, 60)), (exception, timeout) =>
                                           {
                                               logger.LogWarning("Token fetch for {cred} failed (waiting {x} seconds): {ex}", Credentials, timeout.TotalSeconds, exception.Message);
                                           })
                                           .ExecuteAsync(async () =>
                                           {
                                               using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                                               return await client.GetUbiTokenAsync(Credentials.Email, Credentials.Password, Credentials.Service, cancellation.Token);
                                           })
                                           .ConfigureAwait(false);

            Debug.Assert(ubisoftToken != null);

            try
            {
                await Policy.Handle<Exception>()
                            .RetryAsync(5, (e, _) => logger.LogWarning("Data persistence failed: {message}", e.Message))
                            .ExecuteAsync(() => storage.AddToken(ubisoftToken))
                            .ConfigureAwait(false);

                _ = storage.RemoveToken(LastTokenSessionId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError("Writing token to storage failed: {ex}", ex.Message);
            }

            LastTokenSessionId = ubisoftToken.SessionId;
            _timer.Change(ubisoftToken.Expiry - DateTime.UtcNow.AddMinutes(TokenRefreshPreempt), Timeout.InfiniteTimeSpan);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
