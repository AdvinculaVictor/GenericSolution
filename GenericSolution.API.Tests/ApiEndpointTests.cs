using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GenericSolution.DataAccess;
using GenericSolution.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace GenericSolution.API.Tests;

public sealed class ApiEndpointTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = CreateClient();
        _client.DefaultRequestHeaders.Add("X-Test-Auth", "authenticated");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task DatabaseHealth_ReturnsHealthyWhenDatabaseIsAvailable()
    {
        using var response = await _client.GetAsync("/health/database");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body);
        Assert.Contains("Connected", body);
    }

    [Fact]
    public async Task DatabaseHealth_IsAnonymous()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/health/database");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WeatherForecast_ReturnsFiveForecasts()
    {
        using var response = await _client.GetAsync("/weatherforecast");
        var forecasts = await response.Content.ReadFromJsonAsync<List<WeatherForecastResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, forecasts?.Count);
        Assert.All(forecasts!, forecast => Assert.NotNull(forecast.Summary));
    }

    [Fact]
    public async Task ProtectedEndpoints_RequireAuthentication()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/clientes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Clientes_GetAll_ReturnsCustomers()
    {
        await SeedClienteAsync("Customer A");

        using var response = await _client.GetAsync("/clientes");
        var clientes = await response.Content.ReadFromJsonAsync<List<Cliente>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(clientes!, cliente => cliente.Nombre == "Customer A");
    }

    [Fact]
    public async Task Clientes_GetById_ReturnsCustomer()
    {
        var id = await SeedClienteAsync("Customer A");

        using var response = await _client.GetAsync($"/clientes/{id}");
        var cliente = await response.Content.ReadFromJsonAsync<Cliente>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Customer A", cliente?.Nombre);
    }

    [Fact]
    public async Task Clientes_CreateReturnsCustomer()
    {
        using var response = await _client.PostAsJsonAsync("/clientes", NewCliente("Customer A"));
        var cliente = await response.Content.ReadFromJsonAsync<Cliente>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(cliente);
        Assert.True(cliente.Id > 0);
        Assert.Equal("Customer A", cliente.Nombre);
    }

    [Fact]
    public async Task Clientes_UpdateChangesCustomer()
    {
        var id = await SeedClienteAsync("Before update");

        using var response = await _client.PutAsJsonAsync($"/clientes/{id}", NewCliente("After update", id));
        var cliente = await response.Content.ReadFromJsonAsync<Cliente>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("After update", cliente?.Nombre);
    }

    [Fact]
    public async Task Clientes_UpdateRejectsMismatchedId()
    {
        var id = await SeedClienteAsync("Customer A");

        using var response = await _client.PutAsJsonAsync($"/clientes/{id}", NewCliente("Customer B", id + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Clientes_UpdateReturnsNotFoundForMissingCustomer()
    {
        using var response = await _client.PutAsJsonAsync("/clientes/999", NewCliente("Missing", 999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Clientes_PatchChangesOnlyProvidedFields()
    {
        var id = await SeedClienteAsync("Before patch");

        using var response = await _client.PatchAsJsonAsync($"/clientes/{id}", new { Nombre = "After patch" });
        var cliente = await response.Content.ReadFromJsonAsync<Cliente>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("After patch", cliente?.Nombre);
        Assert.Equal("customer@example.com", cliente?.Email);
    }

    [Fact]
    public async Task Clientes_PatchReturnsNotFoundForMissingCustomer()
    {
        using var response = await _client.PatchAsJsonAsync("/clientes/999", new { Nombre = "Missing" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Clientes_DeleteRemovesCustomer()
    {
        var id = await SeedClienteAsync("Customer A");

        using var response = await _client.DeleteAsync($"/clientes/{id}");
        using var getResponse = await _client.GetAsync($"/clientes/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("null", await getResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Clientes_DeleteReturnsNotFoundForMissingCustomer()
    {
        using var response = await _client.DeleteAsync("/clientes/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Categorias_GetAll_ReturnsCategories()
    {
        await SeedCategoriaAsync("Category A");

        using var response = await _client.GetAsync("/categorias");
        var categorias = await response.Content.ReadFromJsonAsync<List<Categoria>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(categorias!, categoria => categoria.Nombre == "Category A");
    }

    [Fact]
    public async Task Categorias_GetById_ReturnsCategory()
    {
        var id = await SeedCategoriaAsync("Category A");

        using var response = await _client.GetAsync($"/categorias/{id}");
        var categoria = await response.Content.ReadFromJsonAsync<Categoria>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Category A", categoria?.Nombre);
    }

    [Fact]
    public async Task Categorias_CreateReturnsCategory()
    {
        using var response = await _client.PostAsJsonAsync("/categorias", new Categoria
        {
            Nombre = "Category A",
            Descripcion = "Initial description"
        });
        var categoria = await response.Content.ReadFromJsonAsync<Categoria>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(categoria);
        Assert.True(categoria.Id > 0);
        Assert.Equal("Category A", categoria.Nombre);
    }

    [Fact]
    public async Task Categorias_UpdateChangesCategory()
    {
        var id = await SeedCategoriaAsync("Before update");

        using var response = await _client.PutAsJsonAsync($"/categorias/{id}", new Categoria
        {
            Id = id,
            Nombre = "After update",
            Descripcion = "Updated description"
        });
        var categoria = await response.Content.ReadFromJsonAsync<Categoria>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("After update", categoria?.Nombre);
        Assert.Equal("Updated description", categoria?.Descripcion);
    }

    [Fact]
    public async Task Categorias_UpdateRejectsMismatchedId()
    {
        var id = await SeedCategoriaAsync("Category A");

        using var response = await _client.PutAsJsonAsync($"/categorias/{id}", new Categoria
        {
            Id = id + 1,
            Nombre = "Category B"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Categorias_UpdateReturnsNotFoundForMissingCategory()
    {
        using var response = await _client.PutAsJsonAsync("/categorias/999", new Categoria
        {
            Id = 999,
            Nombre = "Missing"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Categorias_PatchChangesOnlyProvidedFields()
    {
        var id = await SeedCategoriaAsync("Before patch");

        using var response = await _client.PatchAsJsonAsync($"/categorias/{id}", new { Nombre = "After patch" });
        var categoria = await response.Content.ReadFromJsonAsync<Categoria>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("After patch", categoria?.Nombre);
        Assert.Equal("Existing description", categoria?.Descripcion);
    }

    [Fact]
    public async Task Categorias_PatchReturnsNotFoundForMissingCategory()
    {
        using var response = await _client.PatchAsJsonAsync("/categorias/999", new { Nombre = "Missing" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Categorias_DeleteRemovesCategory()
    {
        var id = await SeedCategoriaAsync("Category A");

        using var response = await _client.DeleteAsync($"/categorias/{id}");
        using var getResponse = await _client.GetAsync($"/categorias/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("null", await getResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Categorias_DeleteReturnsNotFoundForMissingCategory()
    {
        using var response = await _client.DeleteAsync("/categorias/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });

    private async Task<int> SeedClienteAsync(string nombre)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var cliente = NewCliente(nombre);
        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();
        return cliente.Id;
    }

    private async Task<int> SeedCategoriaAsync(string nombre)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var categoria = new Categoria { Nombre = nombre, Descripcion = "Existing description" };
        context.Categorias.Add(categoria);
        await context.SaveChangesAsync();
        return categoria.Id;
    }

    private static Cliente NewCliente(string nombre, int id = 0) => new()
    {
        Id = id,
        Nombre = nombre,
        Email = "customer@example.com",
        Domicilio = "1 Main Street",
        CodigoPostal = "12345",
        RFC = "TEST123456"
    };

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var databaseName = Guid.NewGuid().ToString();
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AuthorizationServer:Authority"] = "https://test-issuer",
                    ["AuthorizationServer:Audience"] = "test-audience"
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DataContext>();
                services.RemoveAll<DbContextOptions<DataContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<DataContext>>();
                services.AddDbContext<DataContext>(options => options.UseInMemoryDatabase(databaseName));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-Auth"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var issuer = configuration["AuthorizationServer:Authority"]!;
            var claims = new[]
            {
                new Claim("scp", "Weatherforecast.Read Clientes.Read Clientes.Write Clientes Read.Categoria", ClaimValueTypes.String, issuer)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed record WeatherForecastResponse(DateOnly Date, int TemperatureC, string? Summary, int TemperatureF);
}