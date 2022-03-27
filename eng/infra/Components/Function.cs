using AzureFunctionSQLBindings.Infra.Roles;
using Pulumi;
using Pulumi.AzureNative.Authorization;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;

class Function : ComponentResource
{
    public Function(string name, FunctionArgs args, ComponentResourceOptions opts = null)
        : base("sqlbindings:web:Function", name, opts)
    {
        var storageAccount = new StorageAccount($"stsqlbindings{Deployment.Instance.StackName}", new StorageAccountArgs
        {
            ResourceGroupName = args.ResourceGroupName,
            Sku = new Pulumi.AzureNative.Storage.Inputs.SkuArgs
            {
                Name = SkuName.Standard_LRS
            },
            Kind = Kind.StorageV2
        }, new CustomResourceOptions { Parent = this });

        var appServicePlan = new AppServicePlan($"plan-sqlbindings-{Deployment.Instance.StackName}", new AppServicePlanArgs
        {
            ResourceGroupName = args.ResourceGroupName,
            Kind = "Windows",
            Sku = new SkuDescriptionArgs
            {
                Tier = "Dynamic",
                Name = "Y1"
            }
        }, new CustomResourceOptions { Parent = this });

        var functionApp = new WebApp($"func-sqlbindings-{Deployment.Instance.StackName}", new WebAppArgs
        {
            Kind = "FunctionApp",
            ResourceGroupName = args.ResourceGroupName,
            ServerFarmId = appServicePlan.Id,
            Identity = new ManagedServiceIdentityArgs
            {
                Type = ManagedServiceIdentityType.SystemAssigned
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
                        Value = args.ApplicationInsightsInstrumentationKey
                    },
                    new NameValuePairArgs
                    {
                        Name = "APPLICATIONINSIGHTS_CONNECTION_STRING",
                        Value = args.ApplicationInsightsConnectionString
                    },
                    new NameValuePairArgs
                    {
                        Name = "VaultUri",
                        Value = args.KeyVaultUri
                    }
                }
            }
        }, new CustomResourceOptions { Parent = this });

        var storageBlobDataOwnerRole = new RoleAssignment("storageBlobDataOwner", new RoleAssignmentArgs
        {
            PrincipalId = functionApp.Identity.Apply(i => i.PrincipalId),
            PrincipalType = PrincipalType.ServicePrincipal,
            RoleDefinitionId = BuiltInRolesIds.StorageBlobDataOwner,
            Scope = storageAccount.Id
        }, new CustomResourceOptions { Parent = this });

        Uri = functionApp.DefaultHostName;
        PrincipalId = functionApp.Identity.Apply(i => i.PrincipalId);

        RegisterOutputs();
    }
    public Output<string> Uri { get; } = default!;
    public Output<string> PrincipalId { get; } = default!;
}

class FunctionArgs
{
    public Input<string> ResourceGroupName { get; set; } = default!;
    public Input<string> ApplicationInsightsInstrumentationKey { get; set; } = default!;
    public Input<string> ApplicationInsightsConnectionString { get; set; } = default!;
    public Input<string> KeyVaultUri { get; set; } = default!;
}