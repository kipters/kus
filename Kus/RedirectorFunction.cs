using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kus
{
    public static class RedirectorFunction
    {
        [FunctionName("Redirector")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "/{*all}")] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var key = req.Path.Value.Substring(1);

            var connString = config["AzureWebJobsStorage"];
            var account = CloudStorageAccount.Parse(connString);
            var tables = account.CreateCloudTableClient();
            var table = tables.GetTableReference("ShortLinks");
            var op = TableOperation.Retrieve<ShortUrl>("shorturl", key);
            var addressResult = await table.ExecuteAsync(op);
            if (addressResult.HttpStatusCode == 404)
            {
                return new NotFoundResult();
            }
            var address = addressResult.Result as ShortUrl;
            return new RedirectResult(address.Address);
        }
    }

    public class ShortUrl : TableEntity
    {
        public string Address { get; set; }
    }
}
