using GenericSolution.API.Policies;
using GenericSolution.DataAccess;
using GenericSolution.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<DataContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null
            )));
//Se inyecta el ScopeHandler de JwtBearer
builder.Services.AddSingleton<IAuthorizationHandler, ScopeHandler>();

// Register the Swagger generator
builder.Services.AddSwaggerGen(options => {
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows {
            AuthorizationCode = new OpenApiOAuthFlow {
                AuthorizationUrl = new Uri($"https://login.microsoftonline.com/33444707-653e-4367-bb8b-1ed2685b6b7c/oauth2/v2.0/authorize"),
                TokenUrl = new Uri($"https://login.microsoftonline.com/33444707-653e-4367-bb8b-1ed2685b6b7c/oauth2/v2.0/token"),
                Scopes = new Dictionary<string, string> {
                    { $"api://272d2080-cc5a-4d08-88c4-901d7658ab7f/Weatherforecast.Read", "Read Weatherforecast from the API" },
                    {"api://272d2080-cc5a-4d08-88c4-901d7658ab7f/Clientes.Read", "Read Clientes from the API" },
                    { $"api://272d2080-cc5a-4d08-88c4-901d7658ab7f/Clientes.Write", "Write Clientes to the API" }
                }
            }
        }
    });
    options.AddSecurityRequirement(document => new() { [new OpenApiSecuritySchemeReference("oauth2", document)] = [] });
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Authority = builder.Configuration["AuthorizationServer:Authority"];
        options.Audience = builder.Configuration["AuthorizationServer:Audience"];
        options.RequireHttpsMetadata = false; // Set to true in production
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("WeatherforecastReadAccess", policy => policy.AddRequirements(new ScopeRequirement("Weatherforecast.Read", builder.Configuration["AuthorizationServer:Authority"])));
    options.AddPolicy("ClientesReadAccess", policy => policy.AddRequirements(new ScopeRequirement("Clientes.Read", builder.Configuration["AuthorizationServer:Authority"])));
    options.AddPolicy("ClientesWriteAccess", policy => policy.AddRequirements(new ScopeRequirement("Clientes.Write", builder.Configuration["AuthorizationServer:Authority"])));
});

var app = builder.Build();

// Enable middleware to serve generated Swagger as a JSON endpoint
app.UseSwagger();
// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.)
app.UseSwaggerUI(options => {
    options.OAuthClientId(builder.Configuration["AzureAd:ClientId"]);
    options.OAuthAppName("Swagger UI");
    options.OAuthUsePkce(); // Recommended for security
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.RequireAuthorization(["WeatherforecastReadAccess"])
.WithName("GetWeatherForecast");

app.MapGet("/clientes", (DataContext context) =>
{
    var clientes = context.Clientes.ToList();
    return clientes;
})
.RequireAuthorization(["ClientesReadAccess"])
.WithName("GetClientes");

app.MapGet("/clientes/{id}", (DataContext context, int id) =>
{
    var cliente = context.Clientes.Find(id);
    return cliente;
})
.RequireAuthorization(["ClientesReadAccess"])
.WithName("GetClienteById");

app.MapPost("/clientes", (DataContext context, Cliente cliente) =>
{
    context.Clientes.Add(cliente);
    context.SaveChanges();
    return cliente;
})
.RequireAuthorization(["ClientesWriteAccess"])
.WithName("CreateCliente");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
