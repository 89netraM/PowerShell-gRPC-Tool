using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PowerShellGrpcTool.ExampleService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();

app.Run();
