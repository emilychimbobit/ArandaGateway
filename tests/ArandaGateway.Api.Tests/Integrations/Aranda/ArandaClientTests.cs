using System.Net;
using System.Text;
using ArandaGateway.Api.Integrations.Aranda;

namespace ArandaGateway.Api.Tests.Integrations.Aranda;

public sealed class ArandaClientTests
{
    [Fact]
    public async Task GetTicketAsync_SendsConfiguredApiKeyUnchanged()
    {
        const string apiKey = "Bearer configured-value";
        var handler = new RecordingHandler(
            """
            {
              "id": 154,
              "idByProject": "CASE-154",
              "customerId": 10,
              "customerUserName": "user",
              "subject": "Subject",
              "stateId": 1,
              "stateName": "Open",
              "openedDate": 1,
              "modifiedDate": 2,
              "groupName": "Support",
              "isClosed": false,
              "itemVersion": 1,
              "modelId": 1,
              "projectId": 1,
              "serviceId": 1,
              "categoryId": 1,
              "itemType": 4
            }
            """);

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://aranda.example/")
        };
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Authorization",
            apiKey);
        var client = new ArandaClient(httpClient);

        await client.GetTicketAsync(154, CancellationToken.None);

        Assert.Equal(apiKey, handler.AuthorizationValue);
        Assert.False(handler.HasStandardAuthorizationHeader);
    }

    private sealed class RecordingHandler(string responseBody)
        : HttpMessageHandler
    {
        public string? AuthorizationValue { get; private set; }

        public bool HasStandardAuthorizationHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationValue = request.Headers
                .GetValues("X-Authorization")
                .Single();
            HasStandardAuthorizationHeader =
                request.Headers.Authorization is not null;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
