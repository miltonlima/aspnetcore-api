using aspnetcore_api.Contracts;
using aspnetcore_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Frontend:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Length == 0)
{
    allowedOrigins = new[] { "http://localhost:5173" };
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

builder.Services.AddScoped<RegistrationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.MapGet("/", () => Results.Redirect("/swagger", permanent: false));

app.MapGet("/api/registrations", async (RegistrationService service, CancellationToken cancellationToken) =>
{
    var entities = await service.ListAsync(cancellationToken);
    var responses = entities.Select(RegistrationResponse.FromEntity);
    return Results.Ok(responses);
})
.WithName("ListRegistrations")
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

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
