using Azure.Identity;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System;

[assembly: FunctionsStartup(typeof(AzureFunctionSQLBindings.Startup))]
namespace AzureFunctionSQLBindings
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
        }

        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            var vaultUri = new Uri(Environment.GetEnvironmentVariable("VaultUri"));
            builder.ConfigurationBuilder.AddAzureKeyVault(vaultUri, new DefaultAzureCredential());
        }
    }
}
