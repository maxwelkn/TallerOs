FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/TallerOs.Domain/TallerOs.Domain.csproj src/TallerOs.Domain/
COPY src/TallerOs.Application/TallerOs.Application.csproj src/TallerOs.Application/
COPY src/TallerOs.Infrastructure/TallerOs.Infrastructure.csproj src/TallerOs.Infrastructure/
COPY src/TallerOs.Api/TallerOs.Api.csproj src/TallerOs.Api/
RUN dotnet restore src/TallerOs.Api/TallerOs.Api.csproj
COPY src/ src/
RUN dotnet publish src/TallerOs.Api/TallerOs.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN mkdir -p /app/data /app/documents && chown -R app:app /app
COPY --from=build --chown=app:app /out/ .
ENV ASPNETCORE_HTTP_PORTS=8080
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "TallerOs.Api.dll"]
