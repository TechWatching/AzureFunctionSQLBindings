using Pulumi;
using Pulumi.AzureNative.Sql;
using Pulumi.AzureNative.Sql.Inputs;

class SqlDatabase : ComponentResource
{
    public SqlDatabase(string name, SqlDatabaseArgs args, ComponentResourceOptions opts = null)
        : base("sqlbindings:sql:SqlDatabase", name, opts)
    {
        var sqlServer = new Server($"sql-sqlbindings-{Deployment.Instance.StackName}", new ServerArgs
        {
            ResourceGroupName = args.ResourceGroupName,
            AdministratorLogin = args.AdminLogin,
            AdministratorLoginPassword = args.AdminPassword,
        }, new CustomResourceOptions { Parent = this });

        // "Allow Azure services and resources to access this server"
        var sqlFwRuleAllowAll = new FirewallRule("sqlFwRuleAllowAll", new FirewallRuleArgs
        {
            EndIpAddress = "0.0.0.0",
            FirewallRuleName = "AllowAllWindowsAzureIps",
            ResourceGroupName = args.ResourceGroupName,
            ServerName = sqlServer.Name,
            StartIpAddress = "0.0.0.0",
        }, new CustomResourceOptions { Parent = this });

        var database = new Database("sqldb-sqlbindings-Main", new DatabaseArgs
        {
            ResourceGroupName = args.ResourceGroupName,
            ServerName = sqlServer.Name,
            Sku = new SkuArgs
            {
                Name = "Basic"
            }
        }, new CustomResourceOptions { Parent = this });

        ConnectionString = Output.Format($"Server={sqlServer.Name}.database.windows.net; User ID={args.AdminLogin};Password={args.AdminPassword}; Database={database.Name}");

        RegisterOutputs();
    }

    public Output<string> ConnectionString { get; }
}

class SqlDatabaseArgs
{
    public Input<string> ResourceGroupName { get; set; } = default!;
    public Input<string> AdminLogin { get; set; } = default!;
    public Input<string> AdminPassword { get; set; } = default!;
}