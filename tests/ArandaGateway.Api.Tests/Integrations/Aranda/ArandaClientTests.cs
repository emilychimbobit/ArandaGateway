using System.Net;
using System.Text;
using ArandaGateway.Api.Integrations.Aranda;
using ArandaGateway.Api.Integrations.Aranda.Models;

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

    [Fact]
    public async Task SearchTicketsAsync_SendsExpectedJsonContract()
    {
        var handler = new RecordingHandler(
            """{"content":[],"totalItems":0,"totalPage":0}""");
        using var httpClient = CreateHttpClient(handler);
        var client = new ArandaClient(httpClient);

        await client.SearchTicketsAsync(
            new()
            {
                Criteria =
                [
                    new()
                    {
                        FieldName = "customerId",
                        FieldValue = "customerId",
                        OperatorName = "equal",
                        OperatorValue = "=",
                        Value = "10",
                        ValueName = "10",
                        Type = 6
                    }
                ],
                Projects = [new(1)],
                Types = [new(1), new(4)]
            },
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "/api/v9/item/search?language=0",
            handler.RequestUri?.PathAndQuery);
        Assert.Contains(
            "\"fieldName\":\"customerId\"",
            handler.RequestBody);
        Assert.Contains("\"type\":6", handler.RequestBody);
    }

    [Fact]
    public async Task CreateTicketAsync_ReadsCreatedTicketContract()
    {
        var handler = new RecordingHandler(
            """{"id":200,"idByProject":"RF-200"}""");
        using var httpClient = CreateHttpClient(handler);
        var client = new ArandaClient(httpClient);
        var request = new ArandaCreateTicketRequest
        {
            CategoryId = 3,
            CustomerId = 10,
            ApplicantId = 10,
            Description = "Description",
            ItemType = 4,
            ImpactId = 5,
            UrgencyId = 6,
            ModelId = 12,
            ProjectId = 1,
            RegistryTypeId = 8,
            ServiceId = 4,
            StateId = 13,
            AuthorId = 2,
            GroupId = 7,
            Subject = "Subject"
        };

        var result = await client.CreateTicketAsync(
            request,
            CancellationToken.None);

        Assert.Equal(200, result.Id);
        Assert.Equal("RF-200", result.IdByProject);
        Assert.Contains("\"consoleType\":2", handler.RequestBody);
    }

    [Fact]
    public async Task UploadAttachmentAsync_SendsArandaMultipartFields()
    {
        var handler = new RecordingHandler(
            """[{"fileName":"evidence.pdf","result":true}]""");
        using var httpClient = CreateHttpClient(handler);
        var client = new ArandaClient(httpClient);
        await using var content = new MemoryStream([1, 2, 3]);

        var result = await client.UploadAttachmentAsync(
            new(
                154,
                4,
                "evidence.pdf",
                "application/pdf",
                content,
                "Evidence"),
            CancellationToken.None);

        Assert.True(Assert.Single(result).Result);
        Assert.StartsWith(
            "multipart/form-data",
            handler.ContentType);
        Assert.Contains("name=FileItemId", handler.RequestBody);
        Assert.Contains("name=FileItemType", handler.RequestBody);
        Assert.Contains("name=Data0", handler.RequestBody);
    }

    [Fact]
    public async Task GetTicketAsync_DoesNotExposeProviderErrorBody()
    {
        var handler = new RecordingHandler(
            """{"sensitive":"provider detail"}""",
            HttpStatusCode.Forbidden);
        using var httpClient = CreateHttpClient(handler);
        var client = new ArandaClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArandaApiException>(
            () => client.GetTicketAsync(154, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.DoesNotContain("provider detail", exception.Message);
    }

    private static HttpClient CreateHttpClient(
        HttpMessageHandler handler) =>
        new(handler)
        {
            BaseAddress = new Uri("https://aranda.example/")
        };

    private sealed class RecordingHandler(string responseBody)
        : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode = HttpStatusCode.OK;

        public RecordingHandler(
            string responseBody,
            HttpStatusCode statusCode)
            : this(responseBody)
        {
            this.statusCode = statusCode;
        }

        public string? AuthorizationValue { get; private set; }

        public bool HasStandardAuthorizationHeader { get; private set; }

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        public string ContentType { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationValue = request.Headers.TryGetValues(
                "X-Authorization",
                out var values)
                ? values.Single()
                : null;
            HasStandardAuthorizationHeader =
                request.Headers.Authorization is not null;
            Method = request.Method;
            RequestUri = request.RequestUri;
            if (request.Content is not null)
            {
                ContentType =
                    request.Content.Headers.ContentType?.ToString() ??
                    string.Empty;
                RequestBody = await request.Content.ReadAsStringAsync(
                    cancellationToken);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
