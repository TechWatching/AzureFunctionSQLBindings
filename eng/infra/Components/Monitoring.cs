using Pulumi;
using Pulumi.AzureNative.OperationalInsights;
using Pulumi.AzureNative.OperationalInsights.Inputs;
// Use specific versions for Application Insights
using Pulumi.AzureNative.Insights.V20200202;
using System.Collections.Generic;

class Monitoring : ComponentResource
{
    public Monitoring(string name, MonitoringArgs args, ComponentResourceOptions opts = null)
        : base("sqlbindings:insights:Monitoring", name, opts)
    {
        var logAnalyticsWorkspace = new Workspace($"log-sqlbindings-{Deployment.Instance.StackName}", new WorkspaceArgs
        {
            ResourceGroupName = args.ResourceGroupName,
            Sku = new WorkspaceSkuArgs
            {
                Name = WorkspaceSkuNameEnum.PerGB2018
            }
        }, new CustomResourceOptions { Parent = this });

        var applicationInsights = new Component($"appi-sqlbindings-{Deployment.Instance.StackName}", new ComponentArgs
        {
            ApplicationType = ApplicationType.Web,
            Kind = "web",
            WorkspaceResourceId = logAnalyticsWorkspace.Id,
            ResourceGroupName = args.ResourceGroupName,
        }, new CustomResourceOptions { Parent = this });

        InstrumentationKey = applicationInsights.InstrumentationKey;
        ConnectionString = applicationInsights.ConnectionString;

        RegisterOutputs();
    }
    public Output<string> InstrumentationKey { get; } = default!;
    public Output<string> ConnectionString { get; } = default!;

}

class MonitoringArgs
{
    public Input<string> ResourceGroupName { get; set; } = default!;
}