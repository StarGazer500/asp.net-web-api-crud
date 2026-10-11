# MyProject: ASP.NET Core Web API (Controllers)

Feature-based architecture with Ports and Adapters, using **controllers** as the routing layer.

## 1. Overview

- **Features** (`Features/`) group code by business capability (Products, Orders, ...). Each feature owns only its controller, service and DTOs.
- **Ports** (`Shared/Abstractions/`) are interfaces for external systems (file storage, email).
- **Adapters** (`Adapters/`) implement the ports (S3, SMTP). Features never reference them.
- **Core** (`Core/`) is app plumbing: config, DbContext, logging, error handling.
- **Shared** (`Shared/`) holds ports, base types and exceptions used by several features.

```
Features --> Shared/Abstractions <-- Adapters
```

## 2. Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 8 or later |
| API style | Controllers with attribute routing |
| Database | PostgreSQL via EF Core (Npgsql) |
| Validation | Data annotations (`[ApiController]` auto-400) |
| File storage | AWS S3 (adapter) |
| Email | SMTP (adapter) |
| API docs | OpenAPI (built in) |

## 3. Prerequisites

- .NET SDK 8+ (`dotnet --version`)
- PostgreSQL running locally (or Docker)
- VS Code with the **C# Dev Kit** extension
- EF Core CLI: `dotnet tool install --global dotnet-ef`

## 4. Create the project

```bash
mkdir MyProject && cd MyProject
dotnet new webapi -n MyProject -o src/MyProject --use-controllers
dotnet new sln -n MyProject
dotnet sln add src/MyProject

cd src/MyProject
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package AWSSDK.S3
dotnet add package AWSSDK.Extensions.NETCore.Setup
```

Create the folders (delete the template's sample `WeatherForecastController` first):

```bash
mkdir -p Adapters/S3 Adapters/Local Adapters/Email \
         Features/Products \
         Core/Data/Entities Core/Errors \
         Shared/Abstractions Shared/Exceptions
```

## 5. Folder structure

```
src/MyProject/
  Adapters/
    S3/                    S3FileStorage.cs, StorageOptions.cs
    Local/                 LocalFileStorage.cs   (used in Development)
    Email/                 SmtpEmailSender.cs
  Features/
    Products/
      ProductsController.cs    (the "router")
      ProductService.cs
      ProductDto.cs
    Orders/
      ...
  Core/
    Data/
      AppDbContext.cs
      Entities/            Product.cs, ProductConfiguration.cs
      Migrations/
    Errors/                GlobalExceptionHandler.cs
  Shared/
    Abstractions/          IFileStorage.cs, IEmailSender.cs
    Exceptions/            AppExceptions.cs
    BaseEntity.cs
  Program.cs               (all DI registrations)
  appsettings.json
```

## 6. Layer responsibilities

| Folder | Contains | Test |
|---|---|---|
| `Features/` | Router, service and DTOs per business capability | "Is this specific to one business capability?" |
| `Shared/` | Ports (interfaces), base entity, exceptions | "Do several features depend on this?" |
| `Adapters/` | Implementations of ports for external systems | "Could I swap this for another provider or a fake?" |
| `Core/` | DbContext, entities and their EF mappings, migrations, error handling | "Does the app need this to run, regardless of vendor?" |

## 7. Dependency rules

1. Features depend on **interfaces in `Shared/Abstractions`**, never on anything in `Adapters/`.
2. Adapters implement ports and never reference features.
3. A feature never touches another feature's tables directly. Cross-feature calls go through the other feature's service (or a small interface it exposes).
4. Routers (controllers/endpoints) only parse the request, call a service, and map the result to HTTP. No business logic, no `DbContext` calls.
5. API responses use **DTOs**, never entities.
6. Interfaces live where the consumer can see them, never next to the adapter implementation.
7. Interfaces describe what the app needs, not what the vendor offers. No SDK types in an interface.

## 8. Implementation guide

### 8.1 Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=learn_dotnet_api;Username=postgres;Password=postgres"
  },
  "Storage": {
    "Bucket": "my-bucket",
    "LocalPath": "uploads"
  },
  "Email": {
    "Host": "localhost",
    "Port": 25,
    "From": "no-reply@myproject.local"
  }
}
```

Keep real secrets out of source control. Use `dotnet user-secrets` locally.

### 8.2 Shared

```csharp
// Shared/BaseEntity.cs
namespace MyProject.Shared;

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
```

```csharp
// Shared/Abstractions/IFileStorage.cs
namespace MyProject.Shared.Abstractions;

public interface IFileStorage
{
    Task<string> UploadAsync(Stream file, string name, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string name, CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
}
```

```csharp
// Shared/Abstractions/IEmailSender.cs
namespace MyProject.Shared.Abstractions;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
```

```csharp
// Shared/Exceptions/AppExceptions.cs
namespace MyProject.Shared.Exceptions;

public abstract class AppException(string message) : Exception(message);
public class NotFoundException(string message) : AppException(message);
public class ConflictException(string message) : AppException(message);
public class StorageException(string message) : AppException(message);
```

### 8.3 Core

```csharp
// Core/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using MyProject.Core.Data.Entities;

namespace MyProject.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

Entities and their EF mappings live in `Core/Data/Entities/`. `ApplyConfigurationsFromAssembly` picks up every `IEntityTypeConfiguration<T>` automatically.

```csharp
// Core/Data/Entities/Product.cs
using MyProject.Shared;

namespace MyProject.Core.Data.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string? ImageKey { get; set; }
}
```

```csharp
// Core/Data/Entities/ProductConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyProject.Core.Data.Entities;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products");
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.Price).HasPrecision(18, 2);
    }
}
```

```csharp
// Core/Errors/GlobalExceptionHandler.cs
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyProject.Shared.Exceptions;

namespace MyProject.Core.Errors;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Server error")
        };

        if (status == 500) logger.LogError(exception, "Unhandled exception");

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == 500 ? null : exception.Message
        }, ct);

        return true;
    }
}
```

### 8.4 Adapters

```csharp
// Adapters/S3/StorageOptions.cs
namespace MyProject.Adapters.S3;

public class StorageOptions
{
    public string Bucket { get; set; } = "";
    public string LocalPath { get; set; } = "uploads";
}
```

```csharp
// Adapters/S3/S3FileStorage.cs
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using MyProject.Shared.Abstractions;
using MyProject.Shared.Exceptions;

namespace MyProject.Adapters.S3;

internal class S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _bucket = options.Value.Bucket;

    public async Task<string> UploadAsync(Stream file, string name, CancellationToken ct = default)
    {
        try
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket, Key = name, InputStream = file
            }, ct);
            return name;
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageException($"Upload failed: {ex.Message}");   // translate vendor errors
        }
    }

    public async Task<Stream?> DownloadAsync(string name, CancellationToken ct = default)
    {
        try
        {
            var res = await s3.GetObjectAsync(_bucket, name, ct);
            return res.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default) =>
        await s3.DeleteObjectAsync(_bucket, name, ct);
}
```

```csharp
// Adapters/Email/SmtpEmailSender.cs
using System.Net.Mail;
using MyProject.Shared.Abstractions;

namespace MyProject.Adapters.Email;

internal class SmtpEmailSender(IConfiguration config) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        using var client = new SmtpClient(config["Email:Host"], config.GetValue<int>("Email:Port"));
        var message = new MailMessage(config["Email:From"]!, to, subject, body);
        await client.SendMailAsync(message, ct);
    }
}
```

```csharp
// Adapters/Local/LocalFileStorage.cs
using Microsoft.Extensions.Options;
using MyProject.Adapters.S3;
using MyProject.Shared.Abstractions;

namespace MyProject.Adapters.Local;

// Disk-based storage for local development, so no AWS account is needed.
internal class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.LocalPath);

    public async Task<string> UploadAsync(Stream file, string name, CancellationToken ct = default)
    {
        var path = ResolvePath(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var target = File.Create(path);
        await file.CopyToAsync(target, ct);
        return name;
    }

    public Task<Stream?> DownloadAsync(string name, CancellationToken ct = default)
    {
        var path = ResolvePath(name);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        File.Delete(ResolvePath(name));
        return Task.CompletedTask;
    }

    private string ResolvePath(string name)
    {
        var path = Path.GetFullPath(Path.Combine(_root, name));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar))
            throw new ArgumentException($"Invalid file name: {name}");
        return path;
    }
}
```

The adapters are registered in `Program.cs` (see 8.7).

### 8.5 Feature: Products (service)

```csharp
// Features/Products/ProductService.cs
using Microsoft.EntityFrameworkCore;
using MyProject.Core.Data;
using MyProject.Core.Data.Entities;
using MyProject.Shared.Abstractions;
using MyProject.Shared.Exceptions;

namespace MyProject.Features.Products;

public class ProductService(AppDbContext db, IFileStorage storage)
{
    public async Task<ProductDto> GetAsync(int id, CancellationToken ct = default) =>
        ToDto(await FindAsync(id, ct));

    public async Task<List<ProductDto>> ListAsync(CancellationToken ct = default) =>
        (await db.Products.AsNoTracking().ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        var product = new Product { Name = dto.Name, Price = dto.Price };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task<ProductDto> UploadImageAsync(int id, Stream image, string fileName, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);

        product.ImageKey = await storage.UploadAsync(image, $"products/{id}/{Path.GetFileName(fileName)}", ct);
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Product> FindAsync(int id, CancellationToken ct) =>
        await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException($"Product {id} was not found.");

    private static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Price, p.ImageKey);
}
```

The service works with `AppDbContext` directly. EF Core's `DbContext` is already a repository and unit of work, so a separate repository layer would mostly forward calls. If a query gets complex or is reused by several services, add a query method or extension method on the `DbSet` instead.

### 8.6 Feature: Products (DTOs, router)

```csharp
// Features/Products/ProductDto.cs
using System.ComponentModel.DataAnnotations;

namespace MyProject.Features.Products;

public record ProductDto(int Id, string Name, decimal Price, string? ImageKey);

public class CreateProductDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }
}
```

`[ApiController]` validates these automatically and returns a 400 `ValidationProblemDetails` before your action runs.

```csharp
// Features/Products/ProductsController.cs   <- the "router"
using Microsoft.AspNetCore.Mvc;

namespace MyProject.Features.Products;

[ApiController]
[Route("api/products")]
public class ProductsController(ProductService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> List(CancellationToken ct) =>
        Ok(await service.ListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> Get(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/image")]
    public async Task<ActionResult<ProductDto>> UploadImage(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadImageAsync(id, stream, file.FileName, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

The service throws `NotFoundException`, and the global handler turns it into a 404, so actions need no null checks or try/catch.

### 8.7 Program.cs

```csharp
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
```

Dependency injection uses the built-in container directly: every registration is one line on `builder.Services`, grouped by layer. There are no `*Module.cs` wrapper files. Each new feature adds its service registration under `// Features`. `MapControllers()` discovers every controller automatically.

In Development, `IFileStorage` resolves to `LocalFileStorage` (files under `uploads/`), so you can run the API without AWS credentials. Swapping a provider is a one-line change here (for example `AddScoped<IFileStorage, AzureBlobFileStorage>()`).

### 8.8 Controller specific tips

- Inherit from `ControllerBase`, not `Controller`. There are no views.
- Keep `[ApiController]` on every controller for automatic model validation and binding inference.
- Use route constraints (`{id:int}`) to reject bad input early.
- Controllers must be `public`; keep adapters `internal`.
- Apply auth with `[Authorize]` on the controller class, and `[AllowAnonymous]` on exceptions.
- Add API versioning (`Asp.Versioning.Mvc`) early if external clients will consume the API.
- Prefer FluentValidation over data annotations only if rules get complex. Call validators from the service layer or a filter.

## 9. Database migrations

Run from `src/MyProject`:

```bash
dotnet ef migrations add InitialCreate --output-dir Core/Data/Migrations
dotnet ef database update
```

After changing any entity or configuration:

```bash
dotnet ef migrations add <DescriptiveName> --output-dir Core/Data/Migrations
dotnet ef database update
```

## 10. Run and test the API

```bash
dotnet watch run
```

Quick check (adjust the port from the console output):

```bash
curl -X POST http://localhost:5073/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","price":49.99}'

curl http://localhost:5073/api/products
```

In VS Code, install the **REST Client** extension and keep requests in a `requests.http` file, or use the built-in OpenAPI document at `/openapi/v1.json`.

## 11. Adding a new feature (checklist)

1. Create `Features/<Name>/`.
2. Add the entity `<Name>.cs` (inheriting `BaseEntity`) and its `IEntityTypeConfiguration<T>` in `Core/Data/Entities/`, plus a `DbSet` on `AppDbContext`.
3. Add `<Name>Service` with the business logic. It uses `AppDbContext` directly. Throw exceptions from `Shared/Exceptions`.
4. Add DTOs. Never return entities.
5. Add the router file (controller or endpoints).
6. Register the service in `Program.cs` (`builder.Services.AddScoped<<Name>Service>()`).
7. Add a migration and update the database.
8. Add service unit tests.

## 12. Testing

Services depend on `AppDbContext` and on port interfaces. Ports get hand-written fakes, so no mocking library or network is needed:

```csharp
class InMemoryFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Files = new();

    public async Task<string> UploadAsync(Stream file, string name, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        Files[name] = ms.ToArray();
        return name;
    }

    public Task<Stream?> DownloadAsync(string name, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(Files.TryGetValue(name, out var b) ? new MemoryStream(b) : null);

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        Files.Remove(name);
        return Task.CompletedTask;
    }
}
```

Suggested test projects:

```bash
dotnet new xunit -n MyProject.Tests -o tests/MyProject.Tests
dotnet sln add tests/MyProject.Tests
dotnet add tests/MyProject.Tests reference src/MyProject
```

- **Unit tests:** services with the EF Core SQLite in-memory provider and fake ports.
- **Integration tests:** `WebApplicationFactory<Program>` with a Testcontainers PostgreSQL instance. Add `public partial class Program { }` at the bottom of `Program.cs` so the test project can see it.

## 13. Enforcing the architecture

In one project these rules are conventions. Enforce them with an architecture test (`NetArchTest.Rules`):

```csharp
[Fact]
public void Features_do_not_depend_on_adapters_or_vendor_sdks()
{
    var result = Types.InAssembly(typeof(Program).Assembly)
        .That().ResideInNamespace("MyProject.Features")
        .ShouldNot().HaveDependencyOnAny("MyProject.Adapters", "Amazon.S3")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

When the project grows, promote `Adapters`, `Core` and `Shared` to class libraries. `Adapters` references only `Shared`, so the compiler blocks features from touching vendor code. Later, any feature can become its own module project with a contracts library.

## 14. Conventions

- Folders and namespaces mirror each other (`MyProject.Features.Products`).
- Async methods end in `Async` and accept a `CancellationToken`.
- One public type per file, named after the file.
- Adapters are `internal`; services and routers are `public`.
- No business logic in routers, no HTTP concerns in services.
- Do not wrap things that are already abstractions (`ILogger`) or that you will never swap.
