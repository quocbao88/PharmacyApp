# Base stage: official ASP.NET Core runtime image for Linux
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

# Build stage: official .NET SDK image
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy project files first to restore NuGet packages (enables caching)
COPY ["src/Pharmacy.Core/Pharmacy.Core.csproj", "src/Pharmacy.Core/"]
COPY ["src/Pharmacy.Infrastructure/Pharmacy.Infrastructure.csproj", "src/Pharmacy.Infrastructure/"]
COPY ["src/Pharmacy.Api/Pharmacy.Api.csproj", "src/Pharmacy.Api/"]

RUN dotnet restore "src/Pharmacy.Api/Pharmacy.Api.csproj"

# Copy the entire source files and build
COPY . .
WORKDIR "/src/src/Pharmacy.Api"
RUN dotnet build "Pharmacy.Api.csproj" -c Release -o /app/build

# Publish stage: compile to optimized output
FROM build AS publish
RUN dotnet publish "Pharmacy.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage: copy published app and run
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Pharmacy.Api.dll"]
