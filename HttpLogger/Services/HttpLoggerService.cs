using System;
using System.IO;
using System.Threading.Tasks;
using HttpLogger.Models;
using Microsoft.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace HttpLogger.Services
{
    public interface IHttpLoggerService
    {
        Task LogRequestAsync(string logger, Request request, Stream content);
    }

    public class HttpLoggerService : IHttpLoggerService
    {
        private CloudQueue queue;
        private CloudBlobContainer blobContainer;

        public HttpLoggerService(IConfiguration configuration)
        {
            var storageAccount = CloudStorageAccount.Parse(
                configuration.GetValue<string>("storage:connectionString"));

            var queueClient = storageAccount.CreateCloudQueueClient();
            queue = queueClient.GetQueueReference(configuration.GetValue<string>("storage:queueReference"));
            queue.CreateIfNotExistsAsync();

            var blobClient = storageAccount.CreateCloudBlobClient();
            blobContainer = blobClient.GetContainerReference(configuration.GetValue<string>("storage:blobReference"));
            blobContainer.CreateIfNotExistsAsync();
        }

        public async Task LogRequestAsync(string logger, Request request, Stream content)
        {
            var message = CreateRequestMessage(logger, request);
            await SendMessage(message);
            await SaveBlob(message.Id, content);
        }

        private RequestQueueMessage CreateRequestMessage(string logger, Request request)
        {
            return new RequestQueueMessage
            {
                Logger = logger,
                Id = Guid.NewGuid(),
                Request = request
            };
        }

        private async Task SendMessage(RequestQueueMessage dto)
        {
            var serialized = JsonConvert.SerializeObject(dto);
            var message = new CloudQueueMessage(serialized);
            await queue.AddMessageAsync(message);
        }

        private async Task SaveBlob(Guid id, Stream content)
        {
            var blob = blobContainer.GetBlockBlobReference(id.ToString());
            await blob.UploadFromStreamAsync(content);
        }
    }
}