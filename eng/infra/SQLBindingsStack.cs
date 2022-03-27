using Pulumi;
using Pulumi.AzureNative.Authorization;
using Pulumi.AzureNative.KeyVault;
using Pulumi.AzureNative.KeyVault.Inputs;
using Pulumi.AzureNative.Resources;
using System.Threading.Tasks;
using Deployment = Pulumi.Deployment;

class SQLBindingsStack : Stack
{
    public SQLBindingsStack()
    {
        var resourceGroup = new ResourceGroup($"rg-sqlbindings-{Deployment.Instance.StackName}");

        var monitoring = new Monitoring("monitoring", new MonitoringArgs
        {
            ResourceGroupName = resourceGroup.Name
        });

        var keyvault = new Vault($"kv-sqlb-{Deployment.Instance.StackName}", new VaultArgs
        {
            Properties = new VaultPropertiesArgs
            {
                EnabledForTemplateDeployment = true,
                Sku = new SkuArgs
                {
                    Family = SkuFamily.A,
                    Name = SkuName.Standard,
                },
                EnableRbacAuthorization = true,
                TenantId = Output.Create(GetTenantId())
            },
            ResourceGroupName = resourceGroup.Name,
        });

        var function = new Function("function", new FunctionArgs
        {
            ResourceGroupName = resourceGroup.Name,
            ApplicationInsightsConnectionString = monitoring.ConnectionString,
            ApplicationInsightsInstrumentationKey = monitoring.InstrumentationKey,
            KeyVaultUri = keyvault.Properties.Apply(v => v.VaultUri)
        });

        var config = new Config();
        var sqlAdminLogin = config.Require("sqlAdminLogin");
        var sqlAdminPassword = config.RequireSecret("sqlAdminPassword");

        var sqlDatabase = new SqlDatabase("sqlDatabase", new SqlDatabaseArgs
        {
            AdminLogin = sqlAdminLogin,
            AdminPassword = sqlAdminPassword,
            ResourceGroupName = resourceGroup.Name
        });

        var security = new Security("security", new SecurityArgs
        {
            ResourceGroupName = resourceGroup.Name,
            KeyVaultId = keyvault.Id,
            KeyVaultName = keyvault.Name,
            SqlDatabaseConnectionString = sqlDatabase.ConnectionString,
            FunctionAppId = function.PrincipalId
        });

        SqlDatabaseConnectionString = sqlDatabase.ConnectionString;
        QueryDatabaseFunctionUrl = Output.Format($"https://{function.Uri}/api/QueryDatabase");
    }

    [Output]
    public Output<string> SqlDatabaseConnectionString { get; set; }

    [Output]
    public Output<string> QueryDatabaseFunctionUrl { get; set; }

    public async Task<string> GetTenantId()
    {
        var clientConfig = await GetClientConfig.InvokeAsync();
        var tenantId = clientConfig.TenantId;
        return tenantId;
    }

}