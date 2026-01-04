using Maanfee.WebSocket;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ********************************
builder.Services.AddMaanfeeWebSocket();

// اضافه کردن CORS برای اجازه دسترسی به API کلاینت
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
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

// ********************************
// 🔥 WebSocket Middleware
app.UseMaanfeeWebSocket();
// ********************************

app.Run();
