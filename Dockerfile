FROM alpine:3.20 AS build

ARG VERSION

RUN apk --repository community add dotnet8-sdk jq

# Copy source code
COPY ["OF DL.sln", "/src/OF DL.sln"]
COPY ["OF DL", "/src/OF DL"]

WORKDIR "/src"

# Build release
RUN dotnet publish -p:Version=$VERSION -c Release --self-contained true -p:PublishSingleFile=true -o out

# Generate default auth.json and config.json files
RUN /src/out/OF\ DL --non-interactive || true
RUN /src/out/OF\ DL --non-interactive || true

# Remove FFMPEG_PATH (deprecated) from default auth.json
RUN jq 'del(.FFMPEG_PATH)' /src/auth.json > /src/updated_auth.json
RUN mv /src/updated_auth.json /src/auth.json

# Set download path in default config.json to /data
RUN jq '.DownloadPath = "/data"' /src/config.json > /src/updated_config.json
RUN mv /src/updated_config.json /src/config.json


FROM alpine:3.20 AS final

# Install dependencies
RUN apk --repository community add ffmpeg bash dotnet8-runtime bash

# Copy release and entrypoint script
COPY --from=build /src/out /app
ADD docker/entrypoint.sh /app
RUN chmod +x /app/entrypoint.sh

RUN mkdir /data  # For downloaded files
RUN mkdir /config  # For configuration files
RUN mkdir /default-config  # For default configuration files

# Copy default configuration files
COPY --from=build /src/config.json /default-config
COPY --from=build /src/auth.json /default-config

WORKDIR /config
CMD /app/entrypoint.sh
CMD ["/app/entrypoint.sh"]
