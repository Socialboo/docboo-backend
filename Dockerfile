# syntax=docker/dockerfile:1

# ---------- shared: LibreOffice (Writer, headless) + metric-compatible fonts ----------
# Liberation Serif/Sans are metric-compatible with Times New Roman/Arial, so page breaks match Word.
ARG LO_PACKAGES="libreoffice-writer-nogui python3-uno fonts-liberation fonts-liberation2 fontconfig curl"

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:latest AS build
WORKDIR /src
COPY Directory.Build.props docboo-backend.slnx ./
COPY Backend/Backend.csproj Backend/
COPY DTOs/DTOs.csproj DTOs/
COPY Domain/Domain.csproj Domain/
COPY Application/Application.csproj Application/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Exporter/Exporter.csproj Exporter/
RUN dotnet restore Backend/Backend.csproj
COPY . .
RUN dotnet publish Backend/Backend.csproj -c Release -o /app --no-restore \
      -p:OpenApiGenerateDocuments=false -p:UseAppHost=false

# ---------- dev (dotnet watch + LibreOffice, used by docker-compose.dev.yml) ----------
FROM mcr.microsoft.com/dotnet/sdk:latest AS dev
ARG LO_PACKAGES
RUN export DEBIAN_FRONTEND=noninteractive && apt-get update \
 && apt-get install -y --no-install-recommends ${LO_PACKAGES} \
 && rm -rf /var/lib/apt/lists/* \
 && fc-cache -f
ENV DOCBOO_CONTAINER_BUILD=true \
    DOTNET_USE_POLLING_FILE_WATCHER=1 \
    ASPNETCORE_URLS=http://+:8080
WORKDIR /src
EXPOSE 8080
CMD ["dotnet", "watch", "--project", "Backend/Backend.csproj", "run", "--no-launch-profile"]

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:latest AS final
ARG LO_PACKAGES
RUN export DEBIAN_FRONTEND=noninteractive && apt-get update \
 && apt-get install -y --no-install-recommends ${LO_PACKAGES} \
 && rm -rf /var/lib/apt/lists/* \
 && fc-cache -f \
 && mkdir -p /data/exports \
 && chown -R app:app /data
ENV ASPNETCORE_URLS=http://+:8080 \
    HOME=/tmp \
    Storage__Root=/data/exports
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=5 \
  CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Backend.dll"]
