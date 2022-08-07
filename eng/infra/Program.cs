using AzureFunctionSQLBindings.Infra;
using Deployment = Pulumi.Deployment;

return await Deployment.RunAsync<SQLBindingsStack>();