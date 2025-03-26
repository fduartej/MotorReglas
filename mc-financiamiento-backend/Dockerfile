# Usa la imagen base de .NET 8.0 para compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copia los archivos del proyecto y restaura las dependencias
COPY mc.csproj ./
RUN dotnet restore

# Publica la aplicación en modo Release
RUN dotnet publish -c Release -o /out $(ls *.csproj)

# Usa la imagen base de .NET 8.0 para ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copia los archivos publicados desde la etapa de compilación
COPY --from=build /out .

# Expone el puerto en el que la aplicación escucha (por defecto 80)
EXPOSE 5000

# Agrega un health check para verificar el estado del servicio
#HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
#    CMD curl --fail http://localhost/health || exit 1

# Configura el punto de entrada para ejecutar la aplicación
ENTRYPOINT ["dotnet", "$(ls *.dll)"]