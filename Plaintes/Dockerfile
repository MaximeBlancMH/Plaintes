FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      libreoffice-writer fontconfig fonts-liberation fonts-dejavu-core \
      fonts-crosextra-carlito fonts-crosextra-caladea tzdata curl \
 && rm -rf /var/lib/apt/lists/*

ENV TZ=Europe/Paris \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    Export__SofficePath=soffice \
    Export__DossierTemporaire=/data/tmp \
    Storage__Racine=/data/pieces \
    Storage__FichierTemoin=.montage_ok \
    DataProtection__Dossier=/data/keys

WORKDIR /app

RUN mkdir -p /data/pieces /data/tmp /data/keys /app/Logs \
 && chown -R $APP_UID:$APP_UID /data /app/Logs
USER $APP_UID
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Plaintes/Plaintes.csproj", "Plaintes/"]
RUN dotnet restore "Plaintes/Plaintes.csproj"
COPY Plaintes/ Plaintes/
WORKDIR /src/Plaintes

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Plaintes.csproj" -c $BUILD_CONFIGURATION -o /app/publish -p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -fsS http://localhost:8080/Account/Deconnecte || exit 1
ENTRYPOINT ["dotnet", "Plaintes.dll"]
