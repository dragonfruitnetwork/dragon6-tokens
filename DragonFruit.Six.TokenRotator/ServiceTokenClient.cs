// Dragon6 API Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under Apache-2. Refer to the LICENSE file for more info

using System;
using System.Diagnostics;
using System.Threading;
using DragonFruit.Data;
using DragonFruit.Six.Api.Authentication;
using DragonFruit.Six.Api.Authentication.Entities;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace DragonFruit.Six.TokenRotator
{
    public class ServiceTokenClient : IDisposable
    {
        private readonly Timer _timer;
        private readonly IServiceScopeFactory _ssf;
        private readonly CancellationTokenSource _cancellation = new();

        internal static TimeSpan TokenRefreshPreempt => TimeSpan.FromMinutes(1800 + Random.Shared.Next(0, 180));

        public ServiceTokenClient(IServiceScopeFactory ssf, UbisoftServiceCredentials credentials, IUbisoftToken token)
        {
            Credentials = credentials;
            LastTokenSessionId = token.SessionId;

            // ensure that the due timespan is not negative
            var nextUpdateDue = token.Expiry - DateTime.UtcNow.Add(TokenRefreshPreempt);

            if (nextUpdateDue < TimeSpan.Zero)
            {
                nextUpdateDue = TimeSpan.Zero;
            }

            _ssf = ssf;
            _timer = new Timer(FetchNewToken, null, nextUpdateDue, Timeout.InfiniteTimeSpan);
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

            logger.LogInformation("{id} token refresh started (replacing {oldTokenId})", Credentials, LastTokenSessionId);

            var ubisoftToken = await Policy.Handle<Exception>()
                                           .WaitAndRetryForeverAsync(a => TimeSpan.FromSeconds(Math.Min(5 * a, 60)), (exception, timeout) => logger.LogWarning("Token fetch for {cred} failed (waiting {x} seconds): {ex}", Credentials, timeout.TotalSeconds, exception.Message))
                                           .ExecuteAsync(async () =>
                                           {
                                               using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                                               using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, _cancellation.Token);
                                               return await client.GetUbiTokenAsync(Credentials.Email, Credentials.Password, Credentials.Service, linkedCancellation.Token).ConfigureAwait(false);
                                           })
                                           .ConfigureAwait(false);

            Debug.Assert(ubisoftToken != null);
            logger.LogInformation("New token acquired for {id} (session id {sid}, expiry {exp})", Credentials, ubisoftToken.SessionId, ubisoftToken.Expiry.ToString("f"));

            try
            {
                await Policy.Handle<Exception>()
                            .RetryAsync(5, (e, _) => logger.LogWarning("Data persistence failed: {message}", e.Message))
                            .ExecuteAsync(() => storage.AddToken(ubisoftToken, _cancellation.Token))
                            .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError("Writing token to storage failed: {ex}", ex.Message);
            }

            LastTokenSessionId = ubisoftToken.SessionId;
            var nextRefreshDue = ubisoftToken.Expiry - DateTime.UtcNow.Add(TokenRefreshPreempt);

            _timer.Change(nextRefreshDue, Timeout.InfiniteTimeSpan);
            logger.LogInformation("{id} token refresh date changed. Next reset in {in}", Credentials, nextRefreshDue.Humanize());
        }

        public void Dispose()
        {
            _timer?.Dispose();

            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }
}
