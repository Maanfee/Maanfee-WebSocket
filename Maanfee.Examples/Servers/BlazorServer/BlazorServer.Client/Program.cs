using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// فقط سرویس‌های ضروری برای WebAssembly
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddMudServices();

// ********************************
//builder.Services.AddSingleton<WebSocketServer>();
// ********************************

await builder.Build().RunAsync();
