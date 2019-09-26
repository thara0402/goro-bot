using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

[assembly: FunctionsStartup(typeof(GoroBotApp.Startup))]

namespace GoroBotApp
{
    public class Startup : FunctionsStartup
    {
        public IConfiguration Configuration { get; }

        public Startup()
        {
            // Prepare for deployment
            // 1.Enable Identity on function apps
            // 2.Add access policy on key vault(Secret:Get and List, Select principal:function app)
            // 3.Add Application settings on function apps(KeyVault:Endpoint, AppConfig:Endpoint)

            var config = new ConfigurationBuilder().AddEnvironmentVariables();
            var settings = config.Build();
            //config.AddAzureAppConfiguration(options =>
            //    options.ConnectWithManagedIdentity(settings["AppConfig:Endpoint"]));
            config.AddAzureKeyVault(settings["KeyVault:Endpoint"]);
            Configuration = config.Build();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();
            builder.Services.Configure<MyOptions>(Configuration);
        }
    }
}
