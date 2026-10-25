using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using RoomManagement.API.Infrastructure.Errors;
using RoomManagement.Data;
using RoomManagement.API.Infrastructure;
using RoomManagement.Business.Interfaces;
using RoomManagement.Business.Services;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRoomManagementData(builder.Configuration);
builder.Services.AddScoped<PropertyService>();
builder.Services.AddScoped<RoomService>();
if (builder.Environment.IsDevelopment())
{
  builder.Services.AddScoped<ICurrentLandlord, DevelopmentLandlord>();
}
else
{
  throw new InvalidOperationException(
      "Chưa cấu hình xác thực chủ trọ cho môi trường ngoài Development.");
}
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
if (app.Environment.IsDevelopment())
{
  await DevelopmentLandlordSeeder.SeedAsync(
      app.Services,
      app.Configuration);
}
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapControllers();
app.Run();
