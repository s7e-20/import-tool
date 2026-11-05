using AnnuityImport.Server.Data;
using AnnuityImport.Server.Excel;
using AnnuityImport.Server.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 52_428_800);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 52_428_800);
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CaseTransfert")));
builder.Services.AddSingleton<ExcelDatasetReader>();
builder.Services.AddSingleton<UploadStore>();
builder.Services.AddScoped<ImportService>();

var app = builder.Build();

try
{
    SchemaBootstrap.Ensure(app.Configuration);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "SQL Server schema was not verified. Mapping can still be created; execute will fail until the database is reachable.");
}

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { message = feature?.Error.Message ?? "Unexpected error." });
}));

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();
