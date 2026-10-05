var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScrapGoModules(builder.Configuration);
builder.AddScrapGoHosting();

var app = builder.Build();

await app.RunScrapGoAsync(args);
