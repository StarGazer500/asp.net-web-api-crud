using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using MyProject.Adapters.Email;
using MyProject.Adapters.Local;
using MyProject.Adapters.S3;
using MyProject.Core.Data;
using MyProject.Core.Errors;
using MyProject.Features.Products;
using MyProject.Shared.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Core: database and error handling
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Adapters: swap a provider by changing its registration here
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
}
else
{
    builder.Services.AddAWSService<IAmazonS3>();
    builder.Services.AddScoped<IFileStorage, S3FileStorage>();
}
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// Features
builder.Services.AddScoped<ProductService>();

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program { }
