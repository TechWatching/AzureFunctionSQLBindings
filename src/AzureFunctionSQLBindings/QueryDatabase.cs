using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public class QueryDatabase
    {
        [FunctionName(nameof(QueryDatabase))]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            [Sql("select p.principal_id as id from sys.database_principals p where p.name = 'db_owner'",
                 CommandType = System.Data.CommandType.Text,
                 ConnectionStringSetting = "SqlDatabaseConnectionString")] IEnumerable<SqlResult> result)
        {
            return new OkObjectResult($"Pulumi is {result.FirstOrDefault()?.Id} times better than Terraform");
        }

       public record SqlResult(int Id);
    }
}
