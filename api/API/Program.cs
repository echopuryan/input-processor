using API.Authentication;
using API.HostedServices;
using BusinessLayer;
using Common.Settings;
using Microsoft.AspNetCore.Authentication;
using NLog.Extensions.Logging;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddBusinessLayerServices();

// Add background hosted service for processing user input jobs
builder.Services.AddHostedService<DataProcessorBackgroundService>();

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

// authentication and authorization
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(BasicAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(
        BasicAuthHandler.SchemeName, null);
}
else
{
    builder.Services.AddAuthentication(BasicAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>(
        BasicAuthHandler.SchemeName, null);
}

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseCors(c => c.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod());
}

app.UseCors(c => c.WithOrigins("http://localhost").AllowAnyHeader().AllowAnyMethod());

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
