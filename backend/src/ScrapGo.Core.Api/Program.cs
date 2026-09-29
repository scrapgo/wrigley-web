var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScrapGoModules(builder.Configuration);
builder.AddScrapGoHosting();

var app = builder.Build();

app.UseScrapGoPipeline();

app.Run();
