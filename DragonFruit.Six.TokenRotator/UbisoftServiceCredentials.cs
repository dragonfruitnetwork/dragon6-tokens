// Dragon6 API Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under Apache-2. Refer to the LICENSE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using DragonFruit.Six.Api.Enums;
using Microsoft.Extensions.Configuration;

namespace DragonFruit.Six.TokenRotator
{
    /// <summary>
    /// Represents a ubisoft login for a targeted <see cref="UbisoftService"/>
    /// </summary>
    public class UbisoftServiceCredentials
    {
        private UbisoftServiceCredentials()
        {
        }

        public string Id { get; private init; }
        public string Email { get; private init; }
        public string Password { get; private init; }

        public UbisoftService Service { get; private init; }

        public static IEnumerable<UbisoftServiceCredentials> FromConfiguration(IConfigurationSection config)
        {
            var requestedServices = config["Services"]?.Split(',', StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
            var enabledServices = Enum.GetValues<UbisoftService>().Where(x => requestedServices.Contains(x.ToString(), StringComparer.OrdinalIgnoreCase));

            foreach (var service in enabledServices)
            {
                yield return new UbisoftServiceCredentials
                {
                    Id = config.Key,
                    Service = service,
                    Email = config["Email"],
                    Password = config["Password"]
                };
            }
        }

        public override string ToString() => $"{Id}@{Service}";
    }
}
