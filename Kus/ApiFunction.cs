using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kus
{
    public static class ApiFunction
    {
        [FunctionName("ApiFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "api")] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            using var bodyReader = new StreamReader(req.Body);
            var requestJson = await bodyReader.ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CreationRequest>(requestJson);

            if (string.IsNullOrWhiteSpace(request.Payload))
            {
                return new BadRequestErrorMessageResult("Missing payload");
            }

            var slug = string.IsNullOrWhiteSpace(request.Slug)
                ? GenerateSlug(config.GetValue("SlugLength", 5))
                : request.Slug;

            var connString = config["AzureWebJobsStorage"];
            var account = CloudStorageAccount.Parse(connString);
            var tables = account.CreateCloudTableClient();
            var table = tables.GetTableReference("ShortLinks");

            var address = request.Payload.StartsWith("http://") || request.Payload.StartsWith("https://")
                ? request.Payload
                : "http://" + request.Payload;

            var entity = new ShortUrl
            {
                Address = address,
                PartitionKey = "shorturl",
                RowKey = slug
            };
            var op = TableOperation.InsertOrReplace(entity);
            var result = await table.ExecuteAsync(op);

            request.Slug = slug;
            request.Payload = address;
            return new OkObjectResult(request);
        }

        private static string GenerateSlug(int length)
        {
            var chars = new[]
            {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u',
                'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
                'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
            };

            var rnd = new Random();
            var buffer = new char[length];
            for (var i = 0; i < length; i++)
            {
                var charIndex = rnd.Next(0, chars.Length);
                buffer[i] = chars[charIndex];
            }

            return new string(buffer);
        }

        public class CreationRequest
        {
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public RequestType RequestType { get; set; }
            public string? Slug { get; set; }
            public string? Payload { get; set; }
        }
    }

    public enum RequestType
    {
        Url,
        WellKnown
    }
}