# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY src/Orchestrator.csproj src/
RUN dotnet restore src/Orchestrator.csproj

# Copy source code
COPY src/ src/
RUN dotnet build src/Orchestrator.csproj -c Release -o /app/build

# Publish
RUN dotnet publish src/Orchestrator.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create directories for external config
RUN mkdir -p /config/Flows /config/Templates

# Copy published app
COPY --from=build /app/publish .

# Set environment variables
ENV EXTERNAL_FLOWS_PATH=/config/Flows
ENV EXTERNAL_TEMPLATES_PATH=/config/Templates
ENV EXTERNAL_CACHE_SECONDS=30

# Expose port
EXPOSE 8080

ENTRYPOINT ["dotnet", "Orchestrator.dll"]
