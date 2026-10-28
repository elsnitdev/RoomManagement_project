using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RoomManagement.API.Authentication;
using RoomManagement.API.Infrastructure;
using RoomManagement.API.Infrastructure.Errors;
using RoomManagement.Business;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Data;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
  builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
  builder.Configuration.AddEnvironmentVariables();
}

builder.Services.AddRoomManagementData(builder.Configuration);
builder.Services.AddRoomManagementBusiness();
builder.Services.AddRoomManagementAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentLandlord, AuthenticatedLandlord>();
builder.Services.AddScoped<PropertyService>();
builder.Services.AddScoped<RoomService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
  options.CustomizeProblemDetails = context =>
  {
    var problem = context.ProblemDetails;
    problem.Instance ??= context.HttpContext.Request.Path;
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    if (!problem.Extensions.ContainsKey("code"))
    {
      problem.Extensions["code"] = problem.Status switch
      {
        StatusCodes.Status400BadRequest => "BAD_REQUEST",
        StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
        StatusCodes.Status403Forbidden => "FORBIDDEN",
        StatusCodes.Status404NotFound => "NOT_FOUND",
        StatusCodes.Status405MethodNotAllowed => "METHOD_NOT_ALLOWED",
        StatusCodes.Status409Conflict => "CONFLICT",
        StatusCodes.Status422UnprocessableEntity => "INVALID_OPERATION",
        StatusCodes.Status500InternalServerError => "INTERNAL_ERROR",
        StatusCodes.Status501NotImplemented => "NOT_IMPLEMENTED",
        _ => "HTTP_ERROR"
      };
    }
  };
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
  options.InvalidModelStateResponseFactory = context =>
  {
    var problem = new ValidationProblemDetails(context.ModelState)
    {
      Status = StatusCodes.Status400BadRequest,
      Title = "Dữ liệu yêu cầu không hợp lệ",
      Instance = context.HttpContext.Request.Path
    };
    problem.Extensions["code"] = "VALIDATION_ERROR";
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    return new BadRequestObjectResult(problem)
    {
      ContentTypes = { "application/problem+json" }
    };
  };
});

var app = builder.Build();
_ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

try
{
  using var scope = app.Services.CreateScope();
  var seeder = scope.ServiceProvider.GetRequiredService<RoomManagement.Data.Seeders.AdminUserSeeder>();
  await seeder.SeedAsync();
}
catch (Exception ex)
{
  var logger = app.Services.GetRequiredService<ILogger<Program>>();
  logger.LogError(ex, "Đã xảy ra lỗi trong quá trình khởi tạo dữ liệu ban đầu (Data Seeding).");
  throw;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
