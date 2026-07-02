using GenericSolution.API.Policies;
using GenericSolution.DataAccess;
using GenericSolution.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

//To publish as AWS Lambda, we need to add the AWS Lambda hosting package and specify the event source as HttpApi (API Gateway). This will allow the application to run as a Lambda function and handle HTTP requests from API Gateway.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// Register the Swagger generator
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
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
        options.MetadataAddress = "https://login.microsoftonline.com/33444707-653e-4367-bb8b-1ed2685b6b7c/.well-known/openid-configuration?appid=272d2080-cc5a-4d08-88c4-901d7658ab7f";
        options.RequireHttpsMetadata = false; // Set to true in production
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("Exception: " + context.Exception.Message);
                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["AuthorizationServer:Authority"],
            ValidAudience = builder.Configuration["AuthorizationServer:Audience"],
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("WeatherforecastReadAccess", policy => policy.AddRequirements(new ScopeRequirement("Weatherforecast.Read", builder.Configuration["AuthorizationServer:Authority"]??"")));
    options.AddPolicy("ClientesReadAccess", policy => policy.AddRequirements(new ScopeRequirement("Clientes.Read", builder.Configuration["AuthorizationServer:Authority"]??"")));
    options.AddPolicy("ClientesWriteAccess", policy => policy.AddRequirements(new ScopeRequirement("Clientes.Write", builder.Configuration["AuthorizationServer:Authority"]??"")));
    options.AddPolicy("Clientes", policy => policy.AddRequirements(new ScopeRequirement("Clientes", builder.Configuration["AuthorizationServer:Authority"]??"")));
    options.AddPolicy("Categoria Read", policy => policy.AddRequirements(new ScopeRequirement("Read.Categoria", builder.Configuration["AuthorizationServer:Authority"]??"")));
});

var app = builder.Build();

// Enable middleware to serve generated Swagger as a JSON endpoint
app.UseSwagger();
// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.)
app.UseSwaggerUI(options =>
{
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
.RequireAuthorization(["WeatherforecastReadAccess", "Clientes"])
.WithName("GetWeatherForecast");

app.MapGet("/clientes", (DataContext context) =>
{
    var clientes = context.Clientes.ToList();
    return clientes;
})
.RequireAuthorization(["ClientesReadAccess","Clientes"])
.WithName("GetClientes");

app.MapGet("/clientes/{id}", (DataContext context, int id) =>
{
    var cliente = context.Clientes.Find(id);
    return cliente;
})
.RequireAuthorization(["ClientesReadAccess","Clientes"])
.WithName("GetClienteById");

app.MapDelete("/clientes/{id}", (DataContext context, int id) =>
{
    var cliente = context.Clientes.Find(id);
    if (cliente is null)
    {
        return Results.NotFound();
    }

    context.Clientes.Remove(cliente);
    context.SaveChanges();
    return Results.Ok(cliente);
})
.RequireAuthorization(["ClientesWriteAccess","Clientes"])
.WithName("DeleteClienteById");

app.MapPost("/clientes", (DataContext context, Cliente cliente) =>
{
    context.Clientes.Add(cliente);
    context.SaveChanges();
    return cliente;
})
.RequireAuthorization(["ClientesWriteAccess", "Clientes"])
.WithName("CreateCliente");

app.MapPut("/clientes/{id}", async (DataContext context, int id, Cliente cliente) =>
{
    if (cliente.Id != 0 && cliente.Id != id)
    {
        return Results.BadRequest();
    }

    var existing = await context.Clientes.FindAsync(id);
    if (existing is null)
    {
        return Results.NotFound();
    }

    existing.Nombre = cliente.Nombre;
    existing.Email = cliente.Email;
    existing.Domicilio = cliente.Domicilio;
    existing.CodigoPostal = cliente.CodigoPostal;
    existing.RFC = cliente.RFC;

    await context.SaveChangesAsync();
    return Results.Ok(existing);
})
.RequireAuthorization(["ClientesWriteAccess", "Clientes"])
.WithName("UpdateCliente");

app.MapMethods("/clientes/{id}", new[] { "PATCH" }, async (DataContext context, int id, ClientePatch patch) =>
{
    var cliente = await context.Clientes.FindAsync(id);
    if (cliente is null)
    {
        return Results.NotFound();
    }

    if (patch.Nombre is not null)
    {
        cliente.Nombre = patch.Nombre;
    }

    if (patch.Email is not null)
    {
        cliente.Email = patch.Email;
    }

    if (patch.Domicilio is not null)
    {
        cliente.Domicilio = patch.Domicilio;
    }

    if (patch.CodigoPostal is not null)
    {
        cliente.CodigoPostal = patch.CodigoPostal;
    }

    if (patch.RFC is not null)
    {
        cliente.RFC = patch.RFC;
    }

    await context.SaveChangesAsync();
    return Results.Ok(cliente);
})
.RequireAuthorization(["ClientesWriteAccess", "Clientes"])
.WithName("PatchCliente");

app.MapGet("/categorias", (DataContext context) =>
{
    var categorias = context.Categorias.ToList();
    return categorias;
})
.RequireAuthorization(["ClientesReadAccess", "Categoria Read"])
.WithName("GetCategorias");

app.MapGet("/categorias/{id}", (DataContext context, int id) =>
{
    var categoria = context.Categorias.Find(id);
    return categoria;
})
.RequireAuthorization(["ClientesReadAccess", "Clientes"])
.WithName("GetCategoriaById");

app.MapDelete("/categorias/{id}", (DataContext context, int id) =>
{
    var categoria = context.Categorias.Find(id);
    if (categoria is null)
    {
        return Results.NotFound();
    }

    context.Categorias.Remove(categoria);
    context.SaveChanges();
    return Results.Ok(categoria);
})
.RequireAuthorization(["ClientesWriteAccess", "Clientes"])
.WithName("DeleteCategoriaById");

app.MapPost("/categorias", (DataContext context, Categoria categoria) =>
{
    context.Categorias.Add(categoria);
    context.SaveChanges();
    return categoria;
})
.RequireAuthorization(["ClientesWriteAccess", "Clientes"])
.WithName("CreateCategoria");

app.MapPut("/categorias/{id}", async (DataContext context, int id, Categoria categoria) =>
{
    if (categoria.Id != 0 && categoria.Id != id)
    {
        return Results.BadRequest();
    }

    var existing = await context.Categorias.FindAsync(id);
    if (existing is null)
    {
        return Results.NotFound();
    }

    existing.Nombre = categoria.Nombre;

    await context.SaveChangesAsync();
    return Results.Ok(existing);
})
.RequireAuthorization(["ClientesWriteAccess", "Clientes"])
.WithName("UpdateCategoria");

app.MapMethods("/categorias/{id}", new[] { "PATCH" }, async (DataContext context, int id, CategoriaPatch patch) =>
{
    var categoria = await context.Categorias.FindAsync(id);
    if (categoria is null)
    {
        return Results.NotFound();
    }

    if (patch.Nombre is not null)
    {
        categoria.Nombre = patch.Nombre;
    }

    await context.SaveChangesAsync();
    return Results.Ok(categoria);
})
.RequireAuthorization(["ClientesWriteAccess", "Clientes"])
.WithName("PatchCategoria");

app.Run();

record ClientePatch(string? Nombre, string? Email, string? Domicilio, string? CodigoPostal, string? RFC);
record CategoriaPatch(string? Nombre);

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
