using System.Text.Json.Serialization;
using RoomManagement.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRoomManagementData(builder.Configuration);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false)));

var app = builder.Build();

app.MapControllers();
app.Run();
