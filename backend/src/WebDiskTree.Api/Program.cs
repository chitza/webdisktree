using Microsoft.EntityFrameworkCore;
using Serilog;
using WebDiskTree.Api.Hubs;
using WebDiskTree.Core.Abstractions;
using WebDiskTree.Infrastructure.Compression;
using WebDiskTree.Infrastructure.Data;
using WebDiskTree.Infrastructure.Scanning;
using WebDiskTree.Infrastructure.Scheduling;
using WebDiskTree.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

var dataDirectory = Path.GetFullPath(builder.Configuration["Storage:DataDirectory"] ?? "./data");
Directory.CreateDirectory(dataDirectory);
var blobDirectory = Path.Combine(dataDirectory, "blobs");
Directory.CreateDirectory(blobDirectory);
var dbPath = Path.Combine(dataDirectory, "webdisktree.db");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddDbContext<WebDiskTreeDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.Configure<AllowedRootsOptions>(builder.Configuration.GetSection("AllowedRoots"));
builder.Services.Configure<ScanStorageOptions>(o => o.BlobDirectory = blobDirectory);

builder.Services.AddSingleton<ScanQueue>();
builder.Services.AddSingleton<ScanCancellationRegistry>();
builder.Services.AddSingleton<IScanEngine, DirectoryScanEngine>();
builder.Services.AddSingleton<TreeBlobSerializer>();
builder.Services.AddSingleton<IScanProgressReporter, SignalRScanProgressReporter>();

builder.Services.AddScoped<FileEntryBulkWriter>();
builder.Services.AddScoped<IPathSafetyValidator, PathSafetyValidator>();
builder.Services.AddSingleton<AllowedRootsService>();

builder.Services.AddHostedService<ScanBackgroundService>();
builder.Services.AddHostedService<ScheduleEvaluatorService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WebDiskTreeDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<ScanProgressHub>("/hubs/scan-progress");

app.MapFallbackToFile("index.html");

app.Run();
