FROM alpine:3.20 AS build

ARG VERSION

RUN apk --no-cache --repository community add \
      dotnet8-sdk==8.0.108-r0 \
      jq==1.7.1-r0

# Copy source code
COPY ["OF DL.sln", "/src/OF DL.sln"]
COPY ["OF DL", "/src/OF DL"]

WORKDIR "/src"

# Build release
RUN dotnet publish -p:WarningLevel=0 -p:Version=$VERSION -c Release --self-contained true -p:PublishSingleFile=true -o out

# Generate default auth.json and config.json files
RUN /src/out/OF\ DL --non-interactive || true && \
      /src/out/OF\ DL --non-interactive || true && \
# Remove FFMPEG_PATH (deprecated) from default auth.json
      jq 'del(.FFMPEG_PATH)' /src/auth.json > /src/updated_auth.json && \
      mv /src/updated_auth.json /src/auth.json && \
# Set download path in default config.json to /data
      jq '.DownloadPath = "/data"' /src/config.json > /src/updated_config.json && \
      mv /src/updated_config.json /src/config.json


FROM alpine:3.20 AS final

# Install dependencies
RUN apk --no-cache --repository community add \
      bash==5.2.26-r0 \
      tini==0.19.0-r3 \
      dotnet8-runtime==8.0.8-r0 \
      ffmpeg==6.1.1-r8

# Copy release and entrypoint script
COPY --from=build /src/out /app

# Create directories for configuration and downloaded files
RUN mkdir /data /config /default-config

# Copy default configuration files
COPY --from=build /src/config.json /default-config
COPY --from=build /src/auth.json /default-config
COPY --from=build ["/src/OF DL/rules.json", "/default-config"]

COPY docker/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

WORKDIR /config
ENTRYPOINT ["/sbin/tini", "--"]
CMD ["/app/entrypoint.sh"]
