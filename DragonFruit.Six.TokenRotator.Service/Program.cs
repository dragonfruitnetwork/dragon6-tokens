using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DragonFruit.Data;
using DragonFruit.Data.Serializers.Newtonsoft;
using DragonFruit.Six.Api.Authentication.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Redis.OM;
using Redis.OM.Modeling;
using StackExchange.Redis;

namespace DragonFruit.Six.TokenRotator.Service
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                           .ConfigureAppConfiguration(cfg => cfg.AddIniFile("d6tokens.ini"))
                           .ConfigureServices(ConfigureServices)
                           .Build();

            using (var scope = host.Services.CreateScope())
            {
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
            services.AddAutoMapper(mapper => mapper.CreateMap<UbisoftToken, RedisServiceToken>().ReverseMap());

            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(GetRedisConfig(ctx.Configuration)));
            services.AddSingleton(s => new RedisConnectionProvider(s.GetRequiredService<IConnectionMultiplexer>()));

            services.AddSingleton<ApiClient, ApiClient<ApiJsonSerializer>>();
            services.AddScoped<ITokenStorageMechanism, RedisTokenStorage>();

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

            redisConfig.CertificateValidation += delegate { return true; };
            return redisConfig;
        }
    }
}
