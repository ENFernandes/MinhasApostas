# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first (for better layer caching)
COPY DataCollector.sln .
COPY DataCollector.Api/DataCollector.Api.csproj DataCollector.Api/
COPY DataCollector.Core/DataCollector.Core.csproj DataCollector.Core/
COPY DataCollector.Infrastructure/DataCollector.Infrastructure.csproj DataCollector.Infrastructure/

# Restore dependencies
RUN dotnet restore DataCollector.Api/DataCollector.Api.csproj

# Copy everything else
COPY . .

# Build and publish
RUN dotnet publish DataCollector.Api/DataCollector.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published output
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Run as non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "DataCollector.Api.dll"]
