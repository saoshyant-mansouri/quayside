FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY Quayside.slnx ./
COPY src/Quayside.Core/Quayside.Core.csproj src/Quayside.Core/
COPY src/Quayside.Infrastructure/Quayside.Infrastructure.csproj src/Quayside.Infrastructure/
COPY src/Quayside.Api/Quayside.Api.csproj src/Quayside.Api/
COPY src/Quayside.Ingest/Quayside.Ingest.csproj src/Quayside.Ingest/

RUN case "$TARGETARCH" in \
      arm64) echo linux-arm64 > /rid ;; \
      *) echo linux-x64 > /rid ;; \
    esac \
 && dotnet restore src/Quayside.Api/Quayside.Api.csproj -r "$(cat /rid)" -p:PublishReadyToRun=true \
 && dotnet restore src/Quayside.Ingest/Quayside.Ingest.csproj -r "$(cat /rid)" -p:PublishReadyToRun=true

COPY src/ src/

RUN dotnet publish src/Quayside.Api/Quayside.Api.csproj \
      -c Release -r "$(cat /rid)" --self-contained false --no-restore \
      -p:PublishReadyToRun=true -p:UseAppHost=false -o /out/api \
 && dotnet publish src/Quayside.Ingest/Quayside.Ingest.csproj \
      -c Release -r "$(cat /rid)" --self-contained false --no-restore \
      -p:PublishReadyToRun=true -p:UseAppHost=false -o /out/ingest

FROM busybox:1.37.0-musl AS shell

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app

COPY --from=shell /bin/busybox /usr/local/bin/busybox
COPY --chmod=755 <<'EOF' /app/entrypoint.sh
set -eu
case "${1:-}" in
  ingest)
    shift
    exec dotnet /app/ingest/Quayside.Ingest.dll "$@"
    ;;
  api)
    shift
    exec dotnet /app/api/Quayside.Api.dll "$@"
    ;;
  *)
    exec dotnet /app/api/Quayside.Api.dll "$@"
    ;;
esac
EOF

COPY --from=build /out/api /app/api
COPY --from=build /out/ingest /app/ingest
COPY data/ /app/data/

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["/usr/local/bin/busybox", "sh", "/app/entrypoint.sh"]
