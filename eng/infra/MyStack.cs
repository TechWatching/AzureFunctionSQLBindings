using System.Threading.Tasks;
using Pulumi;
using Pulumi.AzureNative.Authorization;
using Pulumi.AzureNative.KeyVault;
using Pulumi.AzureNative.KeyVault.Inputs;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using Pulumi.AzureNative.OperationalInsights;
using Deployment = Pulumi.Deployment;
using Sql = Pulumi.AzureNative.Sql;
// Use specific versions for Application Insights
using Component = Pulumi.AzureNative.Insights.V20200202.Component;
using ComponentArgs = Pulumi.AzureNative.Insights.V20200202.ComponentArgs;
using ApplicationType = Pulumi.AzureNative.Insights.V20200202.ApplicationType;

class MyStack : Stack
{
    public MyStack()
    {
        var resourceGroup = new ResourceGroup($"rg-sqlbindings-{Deployment.Instance.StackName}");

        var storageAccount = new StorageAccount($"stsqlbindings{Deployment.Instance.StackName}", new StorageAccountArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Sku = new Pulumi.AzureNative.Storage.Inputs.SkuArgs
            {
                Name = Pulumi.AzureNative.Storage.SkuName.Standard_LRS
            },
            Kind = Kind.StorageV2
        });

        var keyvault = new Vault($"kv-sqlb-{Deployment.Instance.StackName}", new VaultArgs
        {
            Properties = new VaultPropertiesArgs
            {
                EnabledForTemplateDeployment = true,
                Sku = new SkuArgs
                {
                    Family = SkuFamily.A,
                    Name = Pulumi.AzureNative.KeyVault.SkuName.Standard,
                },
                EnableRbacAuthorization = true,
                TenantId = Output.Create(GetTenantId())
            },
            ResourceGroupName = resourceGroup.Name,
        });

        var logAnalyticsWorkspace = new Workspace($"log-sqlbindings-{Deployment.Instance.StackName}", new WorkspaceArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Sku = new Pulumi.AzureNative.OperationalInsights.Inputs.WorkspaceSkuArgs
            {
                
                Name = WorkspaceSkuNameEnum.PerGB2018
            }
        });

        var applicationInsights = new Component($"appi-sqlbindings-{Deployment.Instance.StackName}", new ComponentArgs
        {
            ApplicationType = ApplicationType.Web,
            Kind = "web",
            WorkspaceResourceId = logAnalyticsWorkspace.Id,
            ResourceGroupName = resourceGroup.Name,
        });

        var appServicePlan = new AppServicePlan($"plan-sqlbindings-{Deployment.Instance.StackName}", new AppServicePlanArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Kind = "Windows",
            Sku = new SkuDescriptionArgs
            {
                Tier = "Dynamic",
                Name = "Y1"
            }
        });

        var functionApp = new WebApp($"func-sqlbindings-{Deployment.Instance.StackName}", new WebAppArgs
        {
            Kind = "FunctionApp",
            ResourceGroupName = resourceGroup.Name,
            ServerFarmId = appServicePlan.Id,
            Identity = new ManagedServiceIdentityArgs
            {
                Type = Pulumi.AzureNative.Web.ManagedServiceIdentityType.SystemAssigned
            },
            SiteConfig = new SiteConfigArgs
            {
                AppSettings = new[]
                {
                    new NameValuePairArgs
                    {
                        Name = "runtime",
                        Value = "dotnet",
                    },
                    new NameValuePairArgs
                    {
                        Name = "FUNCTIONS_WORKER_RUNTIME",
                        Value = "dotnet",
                    },
                    new NameValuePairArgs
                    {
                        Name = "FUNCTIONS_EXTENSION_VERSION",
                        Value = "~4"
                    },
                    new NameValuePairArgs
                    {
                        Name = "AzureWebJobsStorage__accountName",
                        Value = storageAccount.Name
                    },
                    new NameValuePairArgs
                    {
                        Name = "APPINSIGHTS_INSTRUMENTATIONKEY",
                        Value = applicationInsights.InstrumentationKey
                    },
                    new NameValuePairArgs
                    {
                        Name = "APPLICATIONINSIGHTS_CONNECTION_STRING",
                        Value = applicationInsights.ConnectionString
                    },
                    new NameValuePairArgs
                    {
                        Name = "VaultUri",
                        Value = keyvault.Properties.Apply(v => v.VaultUri)
                    }
                }
            }
        });

        FunctionAppUri = functionApp.DefaultHostName;

        var storageBlobDataOwnerRole = new RoleAssignment("storageBlobDataOwner", new RoleAssignmentArgs
        {
            PrincipalId = functionApp.Identity.Apply(i => i.PrincipalId),
            PrincipalType = PrincipalType.ServicePrincipal,
            RoleDefinitionId = "/providers/Microsoft.Authorization/roleDefinitions/b7e6dc6d-f1e8-4753-8033-0f276bb0955b",
            Scope = storageAccount.Id
        });

        var config = new Config();
        var sqlAdminLogin = config.Require("sqlAdminLogin");
        var sqlAdminPassword = config.RequireSecret("sqlAdminPassword");

        var sqlServer = new Sql.Server($"sql-sqlbindings-{Deployment.Instance.StackName}", new Sql.ServerArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AdministratorLogin = sqlAdminLogin,
            AdministratorLoginPassword = sqlAdminPassword,
        });

        // "Allow Azure services and resources to access this server"
        var sqlFwRuleAllowAll = new Sql.FirewallRule("sqlFwRuleAllowAll", new Sql.FirewallRuleArgs
        {
            EndIpAddress = "0.0.0.0",
            FirewallRuleName = "AllowAllWindowsAzureIps",
            ResourceGroupName = resourceGroup.Name,
            ServerName = sqlServer.Name,
            StartIpAddress = "0.0.0.0",
        });

        var database = new Sql.Database("sqldb-sqlbindings-Main", new Sql.DatabaseArgs
        {
            ResourceGroupName = resourceGroup.Name,
            ServerName = sqlServer.Name,
            Sku = new Sql.Inputs.SkuArgs
            {
                Name = "Basic"
            }
        });

        SqlDatabaseConnectionString = Output.Format($"Server={sqlServer.Name}.database.windows.net; User ID={sqlAdminLogin};Password={sqlAdminPassword}; Database={database.Name}");

        new Secret("SqlDatabaseConnectionString", new Pulumi.AzureNative.KeyVault.SecretArgs
        {
            SecretName = "SqlDatabaseConnectionString",
            VaultName = keyvault.Name,
            Properties = new SecretPropertiesArgs
            {
                Value = SqlDatabaseConnectionString
            },
            ResourceGroupName = resourceGroup.Name
        });

        var keyVaultvSecretUserRole = new RoleAssignment("keyVaultSecretUser", new RoleAssignmentArgs
        {
            PrincipalId = functionApp.Identity.Apply(i => i.PrincipalId),
            PrincipalType = PrincipalType.ServicePrincipal,
            RoleDefinitionId = "/providers/Microsoft.Authorization/roleDefinitions/4633458b-17de-408a-b874-0445c86b69e6",
            Scope = keyvault.Id
        });
    }

    public async Task<string> GetTenantId()
    {
        var clientConfig = await GetClientConfig.InvokeAsync();
        var tenantId = clientConfig.TenantId;
        return tenantId;
    }

    [Output]
    public Output<string> SqlDatabaseConnectionString { get; set; }

    [Output]
    public Output<string> FunctionAppUri { get; set; }
}
