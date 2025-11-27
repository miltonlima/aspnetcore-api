using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using aspnetcore_api.Contracts;
using aspnetcore_api.Models;
using aspnetcore_api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var allowedOrigins = builder.Configuration.GetSection("Frontend:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Length == 0)
{
    allowedOrigins = new[]
    {
        "http://localhost:5173",
        "https://localhost:5173",
        "http://localhost:5174",
        "https://localhost:5174"
    };
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
builder.Services.AddScoped<EducationUnitService>();
builder.Services.AddScoped<EducationClassService>();
builder.Services.AddScoped<EducationStudentService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<RequestLogService>();
builder.Services.AddScoped<EducationGradeService>();

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

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();

    Stream? originalBodyStream = null;
    MemoryStream? bufferingStream = null;
    var shouldCaptureResponse = context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase);

    if (shouldCaptureResponse)
    {
        originalBodyStream = context.Response.Body;
        bufferingStream = new MemoryStream();
        context.Response.Body = bufferingStream;
    }

    try
    {
        await next();
    }
    finally
    {
        stopwatch.Stop();

        if (bufferingStream is not null && originalBodyStream is not null)
        {
            try
            {
                bufferingStream.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(bufferingStream, Encoding.UTF8, leaveOpen: true);
                var responseText = await reader.ReadToEndAsync();
                context.Items[RequestLogService.ResponseBodyItemKey] = responseText;

                bufferingStream.Seek(0, SeekOrigin.Begin);
                await bufferingStream.CopyToAsync(originalBodyStream, context.RequestAborted);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
                await bufferingStream.DisposeAsync();
            }
        }

        var loggerService = context.RequestServices.GetRequiredService<RequestLogService>();
        await loggerService.LogRequestAsync(context, stopwatch.Elapsed, context.RequestAborted);
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/", () => Results.Redirect("/swagger", permanent: false));

app.MapPost("/api/login", async (LoginRequest request, RegistrationService registrationService, TokenService tokenService, CancellationToken cancellationToken) =>
{
    var identifier = request.Email ?? request.Username;
    if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Email (or username) and password are required." });
    }

    var user = await registrationService.GetUserByEmailAsync(identifier, cancellationToken);

    if (user is null || user.Password == null || !registrationService.VerifyPassword(request.Password, user.Password))
    {
        return Results.Unauthorized();
    }

    var token = tokenService.GenerateToken(user);
    return Results.Ok(new LoginResponse { Token = token });
});

app.MapGet("/api/users/me", async (ClaimsPrincipal user, RegistrationService registrationService, CancellationToken cancellationToken) =>
{
    var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!long.TryParse(userIdValue, out var userId))
    {
        return Results.Unauthorized();
    }

    var entity = await registrationService.GetUserByIdAsync(userId, cancellationToken);
    if (entity is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(UserProfileResponse.FromEntity(entity));
})
.WithName("GetCurrentUser")
.RequireAuthorization()
.Produces<UserProfileResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status404NotFound);

app.MapPut("/api/users/me", async (ClaimsPrincipal user, UpdateUserProfileRequest request, RegistrationService registrationService, CancellationToken cancellationToken) =>
{
    var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!long.TryParse(userIdValue, out var userId))
    {
        return Results.Unauthorized();
    }

    try
    {
        var updated = await registrationService.UpdateUserProfileAsync(userId, request, cancellationToken);
        return updated is null
            ? Results.NotFound()
            : Results.Ok(UserProfileResponse.FromEntity(updated));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("UpdateCurrentUser")
.RequireAuthorization()
.Produces<UserProfileResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/education-units", async (EducationUnitService service, CancellationToken cancellationToken) =>
{
    var units = await service.ListAsync(cancellationToken);
    var responses = units.Select(EducationUnitResponse.FromEntity);
    return Results.Ok(responses);
})
.WithName("ListEducationUnits")
.RequireAuthorization()
.Produces<IEnumerable<EducationUnitResponse>>(StatusCodes.Status200OK);

app.MapPost("/api/education-units", async (CreateEducationUnitRequest request, EducationUnitService service, CancellationToken cancellationToken) =>
{
    try
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/education-units/{created.Id}", EducationUnitResponse.FromEntity(created));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (MySqlException ex) when (ex.Number == 1062)
    {
        return Results.Conflict(new { message = "Código da unidade já cadastrado." });
    }
})
.WithName("CreateEducationUnit")
.RequireAuthorization()
.Produces<EducationUnitResponse>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status409Conflict);

app.MapPut("/api/education-units/{id:long}", async (long id, UpdateEducationUnitRequest request, EducationUnitService service, CancellationToken cancellationToken) =>
{
    try
    {
        var updated = await service.UpdateAsync(id, request, cancellationToken);
        return updated is null
            ? Results.NotFound()
            : Results.Ok(EducationUnitResponse.FromEntity(updated));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (MySqlException ex) when (ex.Number == 1062)
    {
        return Results.Conflict(new { message = "Código da unidade já cadastrado." });
    }
})
.WithName("UpdateEducationUnit")
.RequireAuthorization()
.Produces<EducationUnitResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status409Conflict);

app.MapDelete("/api/education-units/{id:long}", async (long id, EducationUnitService service, CancellationToken cancellationToken) =>
{
    var deleted = await service.DeleteAsync(id, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteEducationUnit")
.RequireAuthorization()
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/education-classes", async (EducationClassService service, CancellationToken cancellationToken) =>
{
    var classes = await service.ListAsync(cancellationToken);
    var responses = classes.Select(EducationClassResponse.FromEntity);
    return Results.Ok(responses);
})
.WithName("ListEducationClasses")
.RequireAuthorization()
.Produces<IEnumerable<EducationClassResponse>>(StatusCodes.Status200OK);

app.MapPost("/api/education-classes", async (CreateEducationClassRequest request, EducationClassService service, CancellationToken cancellationToken) =>
{
    try
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/education-classes/{created.Id}", EducationClassResponse.FromEntity(created));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (MySqlException ex) when (ex.Number == 1062)
    {
        return Results.Conflict(new { message = "Código da turma já cadastrado." });
    }
})
.WithName("CreateEducationClass")
.RequireAuthorization()
.Produces<EducationClassResponse>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status409Conflict);

app.MapPut("/api/education-classes/{id:long}", async (long id, UpdateEducationClassRequest request, EducationClassService service, CancellationToken cancellationToken) =>
{
    try
    {
        var updated = await service.UpdateAsync(id, request, cancellationToken);
        return updated is null
            ? Results.NotFound()
            : Results.Ok(EducationClassResponse.FromEntity(updated));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (MySqlException ex) when (ex.Number == 1062)
    {
        return Results.Conflict(new { message = "Código da turma já cadastrado." });
    }
})
.WithName("UpdateEducationClass")
.RequireAuthorization()
.Produces<EducationClassResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status409Conflict);

app.MapDelete("/api/education-classes/{id:long}", async (long id, EducationClassService service, CancellationToken cancellationToken) =>
{
    var deleted = await service.DeleteAsync(id, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteEducationClass")
.RequireAuthorization()
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/education-classes/{classId:long}/grades", async (long classId, EducationGradeService service, CancellationToken cancellationToken) =>
{
    try
    {
        var grades = await service.GetGradesForClassAsync(classId, cancellationToken);
        return Results.Ok(grades);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("GetEducationClassGrades")
.RequireAuthorization()
.Produces<IEnumerable<EducationStudentGradeResponse>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapPut("/api/education-classes/{classId:long}/grades/{studentId:long}", async (long classId, long studentId, UpdateEducationStudentGradeRequest request, EducationGradeService service, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await service.UpsertGradeAsync(classId, studentId, request, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("UpsertEducationStudentGrade")
.RequireAuthorization()
.Produces<EducationStudentGradeResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/education-students", async (EducationStudentService service, CancellationToken cancellationToken) =>
{
    var students = await service.ListAsync(cancellationToken);
    var responses = students.Select(EducationStudentResponse.FromEntity);
    return Results.Ok(responses);
})
.WithName("ListEducationStudents")
.RequireAuthorization()
.Produces<IEnumerable<EducationStudentResponse>>(StatusCodes.Status200OK);

app.MapGet("/api/education-students/{id:long}", async (long id, EducationStudentService service, CancellationToken cancellationToken) =>
{
    var student = await service.GetByIdAsync(id, cancellationToken);
    return student is null
        ? Results.NotFound()
        : Results.Ok(EducationStudentResponse.FromEntity(student));
})
.WithName("GetEducationStudent")
.RequireAuthorization()
.Produces<EducationStudentResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

app.MapPost("/api/education-students", async (CreateEducationStudentRequest request, EducationStudentService service, CancellationToken cancellationToken) =>
{
    try
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/education-students/{created.Id}", EducationStudentResponse.FromEntity(created));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (MySqlException ex) when (ex.Number == 1062)
    {
        var message = ex.Message.Contains("uq_education_students_cpf", System.StringComparison.OrdinalIgnoreCase)
            ? "CPF já cadastrado."
            : "Código de matrícula já cadastrado.";
        return Results.Conflict(new { message });
    }
})
.WithName("CreateEducationStudent")
.RequireAuthorization()
.Produces<EducationStudentResponse>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status409Conflict);

app.MapPut("/api/education-students/{id:long}", async (long id, UpdateEducationStudentRequest request, EducationStudentService service, CancellationToken cancellationToken) =>
{
    try
    {
        var updated = await service.UpdateAsync(id, request, cancellationToken);
        return updated is null
            ? Results.NotFound()
            : Results.Ok(EducationStudentResponse.FromEntity(updated));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (MySqlException ex) when (ex.Number == 1062)
    {
        var message = ex.Message.Contains("uq_education_students_cpf", System.StringComparison.OrdinalIgnoreCase)
            ? "CPF já cadastrado."
            : "Código de matrícula já cadastrado.";
        return Results.Conflict(new { message });
    }
})
.WithName("UpdateEducationStudent")
.RequireAuthorization()
.Produces<EducationStudentResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status409Conflict);

app.MapDelete("/api/education-students/{id:long}", async (long id, EducationStudentService service, CancellationToken cancellationToken) =>
{
    var deleted = await service.DeleteAsync(id, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteEducationStudent")
.RequireAuthorization()
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

app.MapPost("/api/education-students/{id:long}/enrollments", async (long id, CreateEducationStudentEnrollmentRequest request, EducationStudentService service, CancellationToken cancellationToken) =>
{
    if (request.EducationClassId <= 0)
    {
        return Results.BadRequest(new { message = "Turma é obrigatória." });
    }

    try
    {
        var student = await service.EnrollAsync(id, request.EducationClassId, cancellationToken);
        return student is null
            ? Results.NotFound()
            : Results.Ok(EducationStudentResponse.FromEntity(student));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
})
.WithName("EnrollEducationStudent")
.RequireAuthorization()
.Produces<EducationStudentResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status409Conflict);

app.MapDelete("/api/education-students/{studentId:long}/enrollments/{classId:long}", async (long studentId, long classId, EducationStudentService service, CancellationToken cancellationToken) =>
{
    var student = await service.UnenrollAsync(studentId, classId, cancellationToken);
    return student is null
        ? Results.NotFound()
        : Results.Ok(EducationStudentResponse.FromEntity(student));
})
.WithName("UnenrollEducationStudent")
.RequireAuthorization()
.Produces<EducationStudentResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

app.MapPut("/api/users/me/password", async (ClaimsPrincipal user, UpdatePasswordRequest request, RegistrationService registrationService, CancellationToken cancellationToken) =>
{
    var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!long.TryParse(userIdValue, out var userId))
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
    {
        return Results.BadRequest(new { message = "Senha atual e nova senha são obrigatórias." });
    }

    try
    {
        var (success, invalidPassword) = await registrationService.UpdateUserPasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
        if (!success)
        {
            return invalidPassword
                ? Results.BadRequest(new { message = "Senha atual inválida." })
                : Results.NotFound();
        }

        return Results.NoContent();
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("UpdateCurrentUserPassword")
.RequireAuthorization()
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/registrations", async (RegistrationService service, CancellationToken cancellationToken) =>
{
    var entities = await service.ListAsync(cancellationToken);
    var responses = entities.Select(RegistrationResponse.FromEntity);
    return Results.Ok(responses);
})
.WithName("ListRegistrations")
.RequireAuthorization()
.Produces<IEnumerable<RegistrationResponse>>(StatusCodes.Status200OK);

app.MapPost("/api/registrations", async (RegistrationRequest request, RegistrationService service, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    logger.LogInformation("Received registration request: {Request}", System.Text.Json.JsonSerializer.Serialize(request));
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

app.MapGet("/api/dashboard", async (DashboardService service, CancellationToken cancellationToken) =>
{
    var summary = await service.GetEnrollmentSummaryAsync(cancellationToken);
    return Results.Ok(summary);
})
.WithName("GetDashboardSummary")
.RequireAuthorization()
.Produces<DashboardEnrollmentSummaryResponse>(StatusCodes.Status200OK);

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