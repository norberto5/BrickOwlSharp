using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BrickOwlSharp.Client;
using Xunit;

namespace BrickOwlSharp.Tests;

public class BrickOwlClientApiKeyTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? CapturedUrl { get; private set; }
        public string? CapturedBody { get; private set; }
        private readonly string _responseJson;

        public CapturingHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedUrl = request.RequestUri?.ToString();
            if (request.Content != null)
                CapturedBody = await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson)
            };
        }
    }

    private sealed class TrackingHandler : HttpMessageHandler
    {
        public List<string> CapturedUrls { get; } = new();
        private readonly Func<string, string> _responseSelector;

        public TrackingHandler(Func<string, string> responseSelector)
        {
            _responseSelector = responseSelector;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            CapturedUrls.Add(url);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseSelector(url))
            });
        }
    }

    private static (IBrickOwlClient client, CapturingHandler handler) BuildClient(string responseJson)
    {
        var handler = new CapturingHandler(responseJson);
        var client = BrickOwlClientFactory.Build(new HttpClient(handler));
        return (client, handler);
    }

    [Fact]
    public async Task GetOrdersAsync_WithoutApiKey_UsesInstanceApiKey()
    {
        BrickOwlClientConfiguration.Instance.ApiKey = "instance-key";
        var (client, handler) = BuildClient("[]");

        await client.GetOrdersAsync();

        Assert.Contains("key=instance-key", handler.CapturedUrl);
    }

    [Fact]
    public async Task GetOrdersAsync_WithExplicitApiKey_UsesExplicitKey()
    {
        BrickOwlClientConfiguration.Instance.ApiKey = "instance-key";
        var (client, handler) = BuildClient("[]");

        await client.GetOrdersAsync(apiKey: "explicit-key");

        Assert.Contains("key=explicit-key", handler.CapturedUrl);
        Assert.DoesNotContain("key=instance-key", handler.CapturedUrl);
    }

    [Fact]
    public async Task GetOrderAsync_WithExplicitApiKey_UsesSameKeyForBothRequests()
    {
        BrickOwlClientConfiguration.Instance.ApiKey = "instance-key";
        var trackingHandler = new TrackingHandler(url =>
            url.Contains("order/items") ? "[]" : "{}");
        var client = BrickOwlClientFactory.Build(new HttpClient(trackingHandler));

        await client.GetOrderAsync(42, apiKey: "shop-key");

        Assert.Equal(2, trackingHandler.CapturedUrls.Count);
        foreach (var url in trackingHandler.CapturedUrls)
        {
            Assert.Contains("key=shop-key", url);
            Assert.DoesNotContain("key=instance-key", url);
        }
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_WithoutApiKey_UsesInstanceApiKeyInFormData()
    {
        BrickOwlClientConfiguration.Instance.ApiKey = "instance-key";
        var (client, handler) = BuildClient("{\"status\":\"success\"}");

        await client.UpdateOrderStatusAsync(1, OrderStatus.Processing);

        Assert.Contains("key=instance-key", handler.CapturedBody);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_WithExplicitApiKey_UsesExplicitKeyInFormData()
    {
        BrickOwlClientConfiguration.Instance.ApiKey = "instance-key";
        var (client, handler) = BuildClient("{\"status\":\"success\"}");

        await client.UpdateOrderStatusAsync(1, OrderStatus.Processing, apiKey: "shop2-key");

        Assert.Contains("key=shop2-key", handler.CapturedBody);
        Assert.DoesNotContain("key=instance-key", handler.CapturedBody);
    }

    [Fact]
    public async Task UpdateOrderNoteAsync_WithExplicitApiKey_UsesExplicitKeyInFormData()
    {
        BrickOwlClientConfiguration.Instance.ApiKey = "instance-key";
        var (client, handler) = BuildClient("{\"status\":\"success\"}");

        await client.UpdateOrderNoteAsync(1, "test note", apiKey: "shop3-key");

        Assert.Contains("key=shop3-key", handler.CapturedBody);
        Assert.DoesNotContain("key=instance-key", handler.CapturedBody);
    }

    [Fact]
    public async Task GetOrdersAsync_WithNoKeyAnywhere_ThrowsInvalidOperationException()
    {
        BrickOwlClientConfiguration.Instance.ApiKey = null;
        var (client, _) = BuildClient("[]");

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetOrdersAsync());
    }

    [Fact]
    public async Task Build_WithFactoryApiKey_UsesFactoryKeyWhenNoPerCallKey()
    {
        BrickOwlClientConfiguration.Instance.ApiKey = null;
        var handler = new CapturingHandler("[]");
        var client = BrickOwlClientFactory.Build(new HttpClient(handler), apiKey: "factory-key");

        await client.GetOrdersAsync();

        Assert.Contains("key=factory-key", handler.CapturedUrl);
    }

    [Fact]
    public async Task Build_WithFactoryApiKey_PerCallKeyOverridesFactoryKey()
    {
        BrickOwlClientConfiguration.Instance.ApiKey = null;
        var handler = new CapturingHandler("[]");
        var client = BrickOwlClientFactory.Build(new HttpClient(handler), apiKey: "factory-key");

        await client.GetOrdersAsync(apiKey: "override-key");

        Assert.Contains("key=override-key", handler.CapturedUrl);
        Assert.DoesNotContain("key=factory-key", handler.CapturedUrl);
    }
}
