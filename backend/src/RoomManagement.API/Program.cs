using RoomManagement.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRoomManagementData(builder.Configuration);
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();
app.Run();
