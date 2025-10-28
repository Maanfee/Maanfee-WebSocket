using Maanfee.WebSocket;
using WebAPIClient.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ********************************
// Register WebSocketClient as singleton
builder.Services.AddSingleton<WebSocketClient>(provider =>
{
    // آدرس سرور WebSocket اصلی
    var client = new WebSocketClient(new WebSocketOption { Host = "127.0.0.1", Port = 5000 });
    return client;
});

// Register hosted service for automatic connection
builder.Services.AddHostedService<WebSocketClientService>();
// ********************************

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
