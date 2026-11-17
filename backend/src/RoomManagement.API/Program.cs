using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RoomManagement.API.Authentication;
using RoomManagement.API.Filters;
using RoomManagement.API.Infrastructure;
using RoomManagement.API.Infrastructure.Errors;
using RoomManagement.Business;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
using RoomManagement.Data;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
  // Local runs use console/debug output. The default Windows Event Log provider can be
  // disposed while a background worker is still reporting a shutdown error.
  builder.Logging.ClearProviders();
  builder.Logging.AddConsole();
  builder.Logging.AddDebug();
  builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
  var localEnvPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
  if (File.Exists(localEnvPath))
  {
    var sendGridSettings = new Dictionary<string, string?>();
    foreach (var line in File.ReadLines(localEnvPath))
    {
      var trimmed = line.Trim();
      if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
      var separator = trimmed.IndexOf('=');
      if (separator < 0) continue;
      var key = trimmed[..separator].Trim();
      if (key is not ("SendGrid__ApiKey" or "SendGrid__FromEmail" or "SendGrid__FromName")) continue;
      var value = trimmed[(separator + 1)..].Trim();
      if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') ||
                                (value[0] == '\'' && value[^1] == '\'')))
        value = value[1..^1];
      if (!string.IsNullOrWhiteSpace(value))
        sendGridSettings[key.Replace("__", ":")] = value;
    }
    builder.Configuration.AddInMemoryCollection(sendGridSettings);
  }
  builder.Configuration.AddEnvironmentVariables();
}

builder.Services.AddRoomManagementData(builder.Configuration);
if (builder.Environment.IsDevelopment())
{
  builder.Services.AddScoped<RoomManagement.Business.Interfaces.ISmsOtpSender, RoomManagement.Data.DevelopmentSmsOtpSender>();
}
builder.Services.AddRoomManagementBusiness();
builder.Services.AddRoomManagementAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentLandlord, AuthenticatedLandlord>();
builder.Services.AddScoped<PropertyService>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<RoomImageService>();
builder.Services.AddScoped<ContractDocumentService>();
builder.Services.AddHostedService<BlobCleanupWorker>();
builder.Services.AddScoped<RoomBatchService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<ContractRulesService>();
builder.Services.AddScoped<ContractRulesEvidenceService>();
builder.Services.AddScoped<BillingInputService>();
builder.Services.AddScoped<MonthlyInvoiceCalculator>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<SettlementOverviewService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<LandlordPaymentService>();
builder.Services.AddScoped<PaymentEvidenceService>();
builder.Services.AddScoped<SettlementRefundEvidenceService>();
builder.Services.AddHostedService<ContractExpiryWorker>();
builder.Services.AddHttpClient("NotificationEmail", client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHostedService<NotificationEmailWorker>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<GlobalExceptionFilter>();
    options.Filters.Add<MustChangePasswordFilter>();
})
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
