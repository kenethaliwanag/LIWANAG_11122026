FROM mcr.microsoft.com/dotnet/sdk:10.0.404 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/CsvProcessing.Api/CsvProcessing.Api.csproj src/CsvProcessing.Api/
RUN dotnet restore src/CsvProcessing.Api/CsvProcessing.Api.csproj

COPY src/ src/
RUN dotnet publish src/CsvProcessing.Api/CsvProcessing.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15 AS runtime
WORKDIR /app

RUN mkdir -p /app/data && chown -R app:app /app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/csvprocessing.db" \
    Database__ApplyMigrationsOnStartup=true

VOLUME ["/app/data"]

EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "CsvProcessing.Api.dll"]
