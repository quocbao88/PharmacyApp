# =========================================================
# Unified Dockerfile for Render Deployment
# Builds both Angular Frontend and .NET Backend into one container
# =========================================================

# Stage 1: Build the Angular SPA frontend
FROM node:20-alpine AS client-build
WORKDIR /app

# Copy dependency configs first to leverage Docker caching
COPY src/Pharmacy.Client/package*.json ./src/Pharmacy.Client/
WORKDIR /app/src/Pharmacy.Client
RUN npm ci

# Copy client source code and build for production
COPY src/Pharmacy.Client/ ./
RUN npm run build -- --configuration production

# Stage 2: Build the C# .NET API backend
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS api-build
WORKDIR /src

# Copy project files for NuGet package restore
COPY ["src/Pharmacy.Core/Pharmacy.Core.csproj", "src/Pharmacy.Core/"]
COPY ["src/Pharmacy.Infrastructure/Pharmacy.Infrastructure.csproj", "src/Pharmacy.Infrastructure/"]
COPY ["src/Pharmacy.Api/Pharmacy.Api.csproj", "src/Pharmacy.Api/"]
RUN dotnet restore "src/Pharmacy.Api/Pharmacy.Api.csproj"

# Copy the rest of the source code
COPY . .

# Copy the built Angular frontend files directly into the API's wwwroot folder
RUN rm -rf src/Pharmacy.Api/wwwroot/*
COPY --from=client-build /app/src/Pharmacy.Client/dist/pharmacy.client/browser/ src/Pharmacy.Api/wwwroot/

# Publish the .NET App in Release mode
WORKDIR "/src/src/Pharmacy.Api"
RUN dotnet publish "Pharmacy.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Final runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app
COPY --from=api-build /app/publish .

# Render dynamically sets the PORT environment variable.
# We run the application with sh -c to resolve the $PORT environment variable correctly at runtime.
EXPOSE 80
ENTRYPOINT ["sh", "-c", "dotnet Pharmacy.Api.dll --urls http://0.0.0.0:${PORT:-80}"]
