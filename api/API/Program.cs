using Scalar.AspNetCore;
using BusinessLayer;
using NLog.Extensions.Logging;
using API.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddBusinessLayerServices();

builder.Services.AddMemoryCache();
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.AddNLog(builder.Configuration);
});
builder.Services.Configure<AppSettings>(builder.Configuration);

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseCors(c => c.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod());
}

app.UseCors(c => c.WithOrigins("http://localhost").AllowAnyHeader().AllowAnyMethod());


app.UseAuthorization();

app.MapControllers();

app.Run();
