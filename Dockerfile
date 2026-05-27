# Etapa 1: Build
# Usamos la imagen oficial de .NET 8 SDK para compilar la aplicación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos primero el archivo de proyecto para aprovechar el cache de Docker
COPY CHRONIQ.csproj .

# Restauramos las dependencias (NuGet packages)
RUN dotnet restore CHRONIQ.csproj

# Copiamos el resto del código fuente
COPY . .

# Compilamos y publicamos en modo Release
# El output va a la carpeta /app/publish
RUN dotnet publish CHRONIQ.csproj -c Release -o /app/publish --no-restore

# Etapa 2: Runtime
# Usamos la imagen runtime de ASP.NET Core 8.0 (más liviana que el SDK)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Creamos directorio para logs (tu app escribe logs en archivos)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/logs

# Copiamos los archivos publicados desde la etapa de build
COPY --from=build /app/publish .

# Exponemos el puerto 8080 (puerto por defecto de ASP.NET Core en contenedores)
EXPOSE 8080

# Variable de entorno para que escuche en todas las interfaces
ENV ASPNETCORE_URLS=http://+:8080

# Health check para verificar que la API está funcionando
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

# Comando para ejecutar la aplicación
ENTRYPOINT ["dotnet", "CHRONIQ.dll"]
