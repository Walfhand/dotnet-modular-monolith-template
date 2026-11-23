using Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddEngineModules(typeof(Program).Assembly);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration["ClientOrigin"] ?? "http://127.0.0.1:3000")
    .AllowAnyHeader()
    .AllowAnyMethod()));
var app = builder.Build();

app.UseCors();
app.MapGet("/alive", () => Results.Ok()).ExcludeFromDescription();
app.UseApplication();
app.Run();
