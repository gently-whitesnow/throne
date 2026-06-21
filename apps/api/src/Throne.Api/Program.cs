using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Hosting;
using Throne.Api.Intents;
using Throne.Api.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddThroneApiCore(builder.Configuration);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddControllers(o =>
    o.ModelBinderProviders.Insert(0, new FileParameterModelBinderProvider())
);

var app = builder.Build();

app.UseExceptionHandler(_ => { });

app.MapControllers();
app.MapThroneEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
