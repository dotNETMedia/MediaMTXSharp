using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MediaMtx.Net.HttpApi.Tests;

public class RootEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RootEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Root_ReturnsConfiguredMessage()
    {
        var response = await _client.GetFromJsonAsync<ApiResponse>("/");
        response.Should().NotBeNull();
        response!.Message.Should().Be("MediaMTX.Net API");
    }

    private sealed record ApiResponse(string Message);
}
