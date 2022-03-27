using AzureFunctionSQLBindings.Infra.Roles;
using Pulumi;
using Pulumi.AzureNative.Authorization;
using Pulumi.AzureNative.KeyVault;
using Pulumi.AzureNative.KeyVault.Inputs;

class Security : ComponentResource
{
    public Security(string name, SecurityArgs args, ComponentResourceOptions opts = null)
        : base("sqlbindings:keyvault:Security", name, opts)
    {
        new Secret("SqlDatabaseConnectionString", new SecretArgs
        {
            SecretName = "SqlDatabaseConnectionString",
            VaultName = args.KeyVaultName,
            Properties = new SecretPropertiesArgs
            {
                Value = args.SqlDatabaseConnectionString
            },
            ResourceGroupName = args.ResourceGroupName
        }, new CustomResourceOptions { Parent = this });

        var keyVaultvSecretUserRole = new RoleAssignment("keyVaultSecretUser", new RoleAssignmentArgs
        {
            PrincipalId = args.FunctionAppId,
            PrincipalType = PrincipalType.ServicePrincipal,
            RoleDefinitionId = BuiltInRolesIds.KeyVaultSecretsUser,
            Scope = args.KeyVaultId
        }, new CustomResourceOptions { Parent = this });

        RegisterOutputs();
    }
    public Output<string> InstrumentationKey { get; } = default!;
    public Output<string> ConnectionString { get; } = default!;
}

class SecurityArgs
{
    public Input<string> ResourceGroupName { get; set; } = default!;
    public Input<string> FunctionAppId { get; set; } = default!;
    public Input<string> SqlDatabaseConnectionString { get; set; } = default!;
    public Input<string> KeyVaultId { get; set; } = default!;
    public Input<string> KeyVaultName { get; set; } = default!;
}