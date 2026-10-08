# ScrapGo Core API container image. Build context: backend/.
#   docker build -t scrapgo-core-api .

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Solution-wide build settings and central package versions first, then only
# the project files, so `dotnet restore` stays cached until a dependency changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Shared/ScrapGo.Core.Shared.Kernel/*.csproj src/Shared/ScrapGo.Core.Shared.Kernel/
COPY src/Shared/ScrapGo.Core.Shared.Infrastructure/*.csproj src/Shared/ScrapGo.Core.Shared.Infrastructure/
COPY src/Modules/Identity/ScrapGo.Core.Modules.Identity.Domain/*.csproj src/Modules/Identity/ScrapGo.Core.Modules.Identity.Domain/
COPY src/Modules/Identity/ScrapGo.Core.Modules.Identity.Application/*.csproj src/Modules/Identity/ScrapGo.Core.Modules.Identity.Application/
COPY src/Modules/Identity/ScrapGo.Core.Modules.Identity.Infrastructure/*.csproj src/Modules/Identity/ScrapGo.Core.Modules.Identity.Infrastructure/
COPY src/Modules/Identity/ScrapGo.Core.Modules.Identity.Api/*.csproj src/Modules/Identity/ScrapGo.Core.Modules.Identity.Api/
COPY src/Modules/QuickbaseEngine/ScrapGo.Core.Modules.QuickbaseEngine.Domain/*.csproj src/Modules/QuickbaseEngine/ScrapGo.Core.Modules.QuickbaseEngine.Domain/
COPY src/Modules/QuickbaseEngine/ScrapGo.Core.Modules.QuickbaseEngine.Application/*.csproj src/Modules/QuickbaseEngine/ScrapGo.Core.Modules.QuickbaseEngine.Application/
COPY src/Modules/QuickbaseEngine/ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure/*.csproj src/Modules/QuickbaseEngine/ScrapGo.Core.Modules.QuickbaseEngine.Infrastructure/
COPY src/Modules/QuickbaseEngine/ScrapGo.Core.Modules.QuickbaseEngine.Api/*.csproj src/Modules/QuickbaseEngine/ScrapGo.Core.Modules.QuickbaseEngine.Api/
COPY src/Modules/Suppliers/ScrapGo.Core.Modules.Suppliers.Application/*.csproj src/Modules/Suppliers/ScrapGo.Core.Modules.Suppliers.Application/
COPY src/Modules/Suppliers/ScrapGo.Core.Modules.Suppliers.Infrastructure/*.csproj src/Modules/Suppliers/ScrapGo.Core.Modules.Suppliers.Infrastructure/
COPY src/Modules/Suppliers/ScrapGo.Core.Modules.Suppliers.Api/*.csproj src/Modules/Suppliers/ScrapGo.Core.Modules.Suppliers.Api/
COPY src/ScrapGo.Core.Api/*.csproj src/ScrapGo.Core.Api/
RUN dotnet restore src/ScrapGo.Core.Api/ScrapGo.Core.Api.csproj

COPY src/ src/
RUN dotnet publish src/ScrapGo.Core.Api/ScrapGo.Core.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Cloud Run sends traffic to $PORT (8080); Program.cs binds to it when set,
# and ASPNETCORE_HTTP_PORTS covers a plain `docker run`.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# The .NET images ship a non-root "app" user; never run as root.
USER $APP_UID

ENTRYPOINT ["dotnet", "ScrapGo.Core.Api.dll"]
