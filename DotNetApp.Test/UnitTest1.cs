using System.Net.Http.Json;
using System.Text.Json;

namespace DotNetApp.Test
{
    public class Tests
    {
        private string prefix = "default";
        private const string httpEndpoint = "http://localhost:8081";
        private HttpClient httpClient;

        [OneTimeSetUp]
        public void Setup()
        {
            prefix = $"EH-{Guid.NewGuid()}-message";
            httpClient = new HttpClient() { BaseAddress = new Uri(httpEndpoint) };
        }

        [Test, Order(1)]
        public async Task Send()
        {
            List<string> messages = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                messages.Add($"{prefix}-{i}");
            }

            var response = await httpClient.PostAsJsonAsync("/api/SendMessages", messages);

            Console.WriteLine(response);
            Console.WriteLine(response.StatusCode);

            Assert.IsTrue(response.IsSuccessStatusCode);

        }

        [Test, Order(2)]
        public async Task Receive()
        {
            var response = httpClient.GetAsync("/api/ReceiveMessages").Result;

            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();

            List<string> messages = JsonSerializer.Deserialize<List<string>>(responseBody);

            Assert.IsNotNull(messages);
            Assert.AreEqual(10, messages.Count);

            HashSet<string> messageSet = new HashSet<string>();

            for (int i = 0; i < 10; i++)
            {
                messageSet.Add($"{prefix}-{i}");
            }

            messages.ForEach(m =>
            {
                if (messageSet.Contains(m))
                {
                    messageSet.Remove(m);
                }

            });

            Assert.IsTrue(messageSet.Count == 0, "Not all messages were received");
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            httpClient.Dispose();
        }
    }
}