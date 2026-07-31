using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using TbcaTest.Application.DTOs.Auth;
using Xunit;

namespace TbcaTest.Tests.Integrations;

public class AuthApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-KEY", "test-key");
        _client.DefaultRequestHeaders.Add("X-API-SECRET", "test-secret");
    }

    [Fact]
    public async Task RegisterAndLogin_ShouldReturnToken()
    {
        // 1. Register
        var registerRequest = new RegisterRequest
        {
            Name = "John Doe",
            Email = "john.doe@test.com",
            Password = "SecurePassword123!"
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        
        var registerWrapper = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var registerData = registerWrapper.GetProperty("data").GetRawText();
        var registerResult = JsonSerializer.Deserialize<LoginResponse>(registerData, options);

        registerResult.Should().NotBeNull();
        registerResult!.Token.Should().NotBeNullOrEmpty();
        registerResult.Name.Should().Be("John Doe");

        // 2. Login
        var loginRequest = new LoginRequest
        {
            Email = "john.doe@test.com",
            Password = "SecurePassword123!"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginWrapper = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var loginData = loginWrapper.GetProperty("data").GetRawText();
        var loginResult = JsonSerializer.Deserialize<LoginResponse>(loginData, options);
        
        loginResult.Should().NotBeNull();
        loginResult!.Token.Should().NotBeNullOrEmpty();
    }
}
