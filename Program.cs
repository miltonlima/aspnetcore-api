using System.Text;
using aspnetcore_api.Contracts;
using aspnetcore_api.Models;
using aspnetcore_api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Frontend:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Length == 0)
{
    allowedOrigins = new[] { "http://localhost:5173", "https://localhost:5173" };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddScoped<MySqlConnection>(_ => 
    new MySqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TokenService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtKey = builder.Configuration["Jwt:Key"];
    if (string.IsNullOrEmpty(jwtKey))
    {
        throw new InvalidOperationException("Jwt:Key configuration is missing.");
    }
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var httpsPort = builder.Configuration.GetValue<int?>("ASPNETCORE_HTTPS_PORT");
if (httpsPort.HasValue)
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/", () => Results.Redirect("/swagger", permanent: false));

app.MapPost("/api/login", async (LoginRequest request, RegistrationService registrationService, TokenService tokenService, CancellationToken cancellationToken) =>
{
    if (request.Username == null || request.Password == null)
    {
        return Results.BadRequest("Username and password are required.");
    }
    var user = await registrationService.GetUserByEmailAsync(request.Username, cancellationToken);

    if (user is null || user.Password == null || !registrationService.VerifyPassword(request.Password, user.Password))
    {
        return Results.Unauthorized();
    }

    var token = tokenService.GenerateToken(user);
    return Results.Ok(new LoginResponse { Token = token });
});

app.MapGet("/api/registrations", async (RegistrationService service, CancellationToken cancellationToken) =>
{
    var entities = await service.ListAsync(cancellationToken);
    var responses = entities.Select(RegistrationResponse.FromEntity);
    return Results.Ok(responses);
})
.WithName("ListRegistrations")
.RequireAuthorization()
.Produces<IEnumerable<RegistrationResponse>>(StatusCodes.Status200OK);

app.MapPost("/api/registrations", async Task<IResult> (RegistrationRequest request, RegistrationService service, CancellationToken cancellationToken) =>
{
    try
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/registrations/{created.Id}", RegistrationResponse.FromEntity(created));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (MySqlException ex) when (ex.Number == 1062)
    {
        return Results.Conflict(new { message = "CPF ou e-mail já cadastrado." });
    }
})
.WithName("CreateRegistration")
.Produces<RegistrationResponse>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status409Conflict);

app.MapPut("/api/registrations/{id:long}", async Task<IResult> (long id, UpdateRegistrationRequest request, RegistrationService service, CancellationToken cancellationToken) =>
{
    try
    {
        var updated = await service.UpdateAsync(id, request, cancellationToken);
        return updated is null
            ? Results.NotFound()
            : Results.Ok(RegistrationResponse.FromEntity(updated));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (MySqlException ex) when (ex.Number == 1062)
    {
        return Results.Conflict(new { message = "CPF ou e-mail já cadastrado." });
    }
})
.WithName("UpdateRegistration")
.RequireAuthorization()
.Produces<RegistrationResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound);

app.MapDelete("/api/registrations/{id:long}", async Task<IResult> (long id, CancellationToken cancellationToken) =>
{
    try
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

    await using var cmd = conn.CreateCommand();
    // use the actual table name present in the database
    cmd.CommandText = "DELETE FROM person_registrations WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? Results.NoContent() : Results.NotFound();
    }
    catch (MySqlException ex)
    {
        // log/return generic bad request for DB issues
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("DeleteRegistration")
.RequireAuthorization()
.Produces(Microsoft.AspNetCore.Http.StatusCodes.Status204NoContent)
.Produces(Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest)
.Produces(Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound);

app.MapGet("/api/dashboard", () => Results.Ok(new { Message = "Bem-vindo ao seu novo painel!" }))
   .WithName("GetDashboardMessage")
   .RequireAuthorization()
   .Produces<object>(StatusCodes.Status200OK);

app.MapGet("/api/profile", () =>
{
    var userProfile = new
    {
        Name = "Usuário de Exemplo",
        Email = "usuario@exemplo.com",
        JoinDate = "2025-01-15"
    };
    return Results.Ok(userProfile);
}).WithName("GetUserProfile")
.RequireAuthorization()
.Produces<object>(StatusCodes.Status200OK);

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapFallbackToFile("index.html");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}