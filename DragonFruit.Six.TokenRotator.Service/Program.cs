using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Threading.Tasks;
using DragonFruit.Data;
using DragonFruit.Data.Serializers.Newtonsoft;
using DragonFruit.Six.Api.Authentication.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Redis.OM;
using Redis.OM.Modeling;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using StackExchange.Redis;

namespace DragonFruit.Six.TokenRotator.Service
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                           .ConfigureAppConfiguration(cfg => cfg.AddIniFile("d6accounts.ini"))
                           .ConfigureLogging((opts, logging) =>
                           {
                               logging.ClearProviders();
                               logging.AddSerilog(GetLogger(opts.Configuration));
                           })
                           .ConfigureServices(ConfigureServices)
                           .Build();

            using (var scope = host.Services.CreateScope())
            {
                // perform redis index updates
                var redis = scope.ServiceProvider.GetRequiredService<RedisConnectionProvider>();
                var targetTypes = Assembly.GetExecutingAssembly().ExportedTypes.Where(x => x.GetCustomAttribute<DocumentAttribute>() != null);

                foreach (var documentType in targetTypes)
                {
#if !DEBUG
                    if (args.Contains("--update-redis-index"))
#endif
                    {
                        await redis.Connection.DropIndexAsync(documentType).ConfigureAwait(false);
                    }

                    await redis.Connection.CreateIndexAsync(documentType).ConfigureAwait(false);
                }
            }

            await host.RunAsync().ConfigureAwait(false);
        }

        private static void ConfigureServices(HostBuilderContext ctx, IServiceCollection services)
        {
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(GetRedisConfig(ctx.Configuration)));
            services.AddSingleton(s => new RedisConnectionProvider(s.GetRequiredService<IConnectionMultiplexer>()));

            services.AddAutoMapper(mapper => mapper.CreateMap<UbisoftToken, RedisServiceToken>());
            services.AddScoped<ITokenStorageMechanism, RedisTokenStorage>();

            services.AddSingleton<ApiClient>(new ApiClient<ApiJsonSerializer>
            {
                UserAgent = "UbiServices_SDK_2020.Release.58_PC64_ansi_static",
                Headers = { ["Ubi-localeCode"] = "en-US" },
                Handler = () => new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.All
                }
            });

            services.AddHostedService<TokenRefreshScheduler>();
        }

        private static ConfigurationOptions GetRedisConfig(IConfiguration config)
        {
            var redisConfig = new ConfigurationOptions
            {
                SocketManager = new SocketManager(options: SocketManager.SocketManagerOptions.UseThreadPool)
            };

#if DEBUG
            redisConfig.EndPoints.Add(System.Net.IPAddress.Loopback, 6370);
#else
            redisConfig.User = config["Redis:User"];
            redisConfig.Password = config["Redis:Pass"];
            redisConfig.Ssl = config["Redis:Ssl"] != "false";

            redisConfig.EndPoints.Add(config["Redis:Host"], int.TryParse(config["Redis:Port"], out var p) ? p : 6379);
#endif

            redisConfig.CertificateValidation += (sender, certificate, chain, errors) =>
            {
                var certFingerprint = config["Redis:IssuerCertificateFingerprint"];

                if (string.IsNullOrEmpty(certFingerprint))
                {
                    return errors == SslPolicyErrors.None;
                }

                return chain?.ChainElements.Any(x => x.Certificate.Thumbprint.Equals(certFingerprint)) == true;
            };

            return redisConfig;
        }

        private static Logger GetLogger(IConfiguration config)
        {
            var sentryDsn = config["SentryDsn"];
            var loggerBuilder = new LoggerConfiguration().WriteTo.Console(theme: AnsiConsoleTheme.Literate);

#if !DEBUG
            loggerBuilder.MinimumLevel.Information();
#endif

            if (!string.IsNullOrEmpty(sentryDsn))
            {
                loggerBuilder.WriteTo.Sentry(o =>
                {
                    o.Dsn = sentryDsn;
                    o.MaxBreadcrumbs = 25;
                    o.MinimumEventLevel = LogEventLevel.Error;
                    o.Release = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
                });
            }

            return loggerBuilder.CreateLogger();
        }
    }
}
