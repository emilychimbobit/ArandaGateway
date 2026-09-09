
# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar dependencias del proyecto de la API
COPY src/backend/api/ArandaGateway.Api/ArandaGateway.Api.csproj src/backend/api/ArandaGateway.Api/
RUN dotnet restore src/backend/api/ArandaGateway.Api/ArandaGateway.Api.csproj

# Copiar el resto del código y publicar
COPY . .
RUN dotnet publish src/backend/api/ArandaGateway.Api/ArandaGateway.Api.csproj \
    -c Release -o /app/publish --no-restore

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render inyecta el puerto en la variable PORT; usar 8080 como respaldo local.
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet ArandaGateway.Api.dll"]
