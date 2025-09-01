# Usa la imagen base de .NET 8.0 para compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copia los archivos del proyecto y restaura las dependencias
COPY mc.csproj ./
RUN dotnet restore

# Copia el resto del código fuente y publica la aplicación en modo Release
COPY . ./
RUN dotnet publish -c Release -o /out

# Usa la imagen base de .NET 8.0 para ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copia los archivos publicados desde la etapa de compilación
COPY --from=build /out .

# Copia el archivo local.settings.json al contenedor
COPY local.settings.json ./local.settings.json
COPY app.db ./app.db

# Expone el puerto en el que la aplicación escucha
EXPOSE 8080

# Configura el punto de entrada para ejecutar la aplicación
ENTRYPOINT ["dotnet", "mc.dll"]