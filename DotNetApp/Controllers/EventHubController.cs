using System.Text;

using Microsoft.AspNetCore.Mvc;

using Azure.Messaging.EventHubs.Producer;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;

namespace DotNetApp.Controllers
{
    [Route("api/[action]")]
    [ApiController]
    public class EventHubController : ControllerBase
    {
        private static readonly string _eventHubConnectionString =
            "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

        private static readonly string _eventHubName = "eh1";

        private static string _checkpointBlobContainer = Guid.NewGuid().ToString();

        private static string _blobConnectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://localhost:10000/devstoreaccount1;";

        private readonly SemaphoreSlim _mutex = new(1, 1);

        private static List<string> _messages = new();

        [HttpPost]
        public async Task<IActionResult> SendMessages([FromBody] List<string> eventDatas)
        {
            EventHubProducerClient producerClient = new EventHubProducerClient(_eventHubConnectionString, _eventHubName);

            var batch = await producerClient.CreateBatchAsync();

            foreach (var eventData in eventDatas)
            {
                var eventObj = new EventData(Encoding.UTF8.GetBytes(eventData));

                if (!batch.TryAdd(eventObj))
                {
                    // Handle the case where the batch is full
                    await producerClient.SendAsync(batch);
                    batch = await producerClient.CreateBatchAsync();
                    batch.TryAdd(eventObj);
                }
            }

            if (batch.Count > 0)
            {
                await producerClient.SendAsync(batch);
            }

            await producerClient.DisposeAsync();

            return Ok();
        }

        [HttpGet]
        public async Task<List<string>> ReceiveMessages()
        {
            await _mutex.WaitAsync();
            try
            {
                _messages.Clear();
                await ReceiveWithEventProcessor();
                return _messages;
            }
            finally
            {
                _mutex.Release();
            }
        }

        private static async Task ReceiveWithEventProcessor()
        {
            BlobContainerClient blobContainerClient = new BlobContainerClient(_blobConnectionString, _checkpointBlobContainer);

            // Create an event processor client to process events in the event hub
            // TODO: Replace the <EVENT_HUBS_NAMESPACE> and <HUB_NAME> placeholder values
            var processor = new EventProcessorClient(blobContainerClient, EventHubConsumerClient.DefaultConsumerGroupName, _eventHubConnectionString, _eventHubName);

            // Register handlers for processing events and handling errors
            processor.ProcessEventAsync += ProcessEventHandler;
            processor.ProcessErrorAsync += ProcessErrorHandler;

            // Start the processing
            await processor.StartProcessingAsync();

            // Wait for 3 seconds for the events to be processed
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Stop the processing
            await processor.StopProcessingAsync();


        }

        private static Task ProcessEventHandler(ProcessEventArgs eventArgs)
        {
            // Write the body of the event to the console window
            Console.WriteLine("Received event using Event Processor: {0}", Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray()));
            _messages.Add(Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray()));
            return eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);
        }

        private static Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
        {
            // Write details about the error to the console window
            Console.WriteLine($"\tPartition '{eventArgs.PartitionId}': an unhandled exception was encountered. This was not expected to happen.");
            Console.WriteLine(eventArgs.Exception.Message);
            return Task.CompletedTask;
        }


        public static async Task CreateCheckpointBlobForTests()
        {
            // With connection string
            var client = new BlobContainerClient(_blobConnectionString, _checkpointBlobContainer);
            try
            {
                await client.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
