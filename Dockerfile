# =========================================================
# Unified Dockerfile for Render Deployment (API & Pre-built SPA)
# Builds the C# .NET API and serves the pre-built client from wwwroot
# =========================================================

# Stage 1: Build the C# .NET API backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for NuGet package restore
COPY ["src/Pharmacy.Core/Pharmacy.Core.csproj", "src/Pharmacy.Core/"]
COPY ["src/Pharmacy.Infrastructure/Pharmacy.Infrastructure.csproj", "src/Pharmacy.Infrastructure/"]
COPY ["src/Pharmacy.Api/Pharmacy.Api.csproj", "src/Pharmacy.Api/"]
RUN dotnet restore "src/Pharmacy.Api/Pharmacy.Api.csproj"

# Copy the entire source code (including the pre-built wwwroot folder)
COPY . .

# Publish the .NET App in Release mode
WORKDIR "/src/src/Pharmacy.Api"
RUN dotnet publish "Pharmacy.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app

# Prevent inotify instance exhaustion on Render's free-tier Linux containers (limit: 128).
# 1. Disable config-file reloadOnChange (the direct cause of the FileSystemWatcher crash)
# 2. Force Production environment (disables development-mode file watchers)
# 3. Suppress the Physical File Provider's file watching for static files
ENV DOTNET_hostBuilder__reloadConfigOnChange=false \
    DOTNET_ENVIRONMENT=Production \
    ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Render dynamically sets the PORT environment variable.
# We run the application with sh -c to resolve the $PORT environment variable correctly at runtime.
EXPOSE 80
ENTRYPOINT ["sh", "-c", "dotnet Pharmacy.Api.dll --urls http://0.0.0.0:${PORT:-80}"]
