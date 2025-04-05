FROM alpine:3.20 AS build

ARG VERSION

RUN apk --no-cache --repository community add \
      dotnet8-sdk

# Copy source code
COPY ["OF DL.sln", "/src/OF DL.sln"]
COPY ["OF DL", "/src/OF DL"]

WORKDIR "/src"

# Build release
RUN dotnet publish -p:WarningLevel=0 -p:Version=$VERSION -c Release --self-contained true -p:PublishSingleFile=true -o out

# Generate default config.conf files
RUN /src/out/OF\ DL --non-interactive || true && \
# Set download path in default config.conf to /data
      sed -e 's/DownloadPath = ""/DownloadPath = "\/data"/' /src/config.conf > /src/updated_config.conf && \
      mv /src/updated_config.conf /src/config.conf


FROM alpine:3.20 AS final

# Install dependencies
RUN apk --no-cache --repository community add \
      bash \
      tini \
      dotnet8-runtime \
      ffmpeg \
      udev \
      ttf-freefont \
      chromium \
      supervisor \
      xvfb \
      x11vnc \
      novnc

# Redirect webroot to vnc.html instead of displaying directory listing
RUN echo "<!DOCTYPE html><html><head><meta http-equiv=\"Refresh\" content=\"0; url='vnc.html'\" /></head><body></body></html>" > /usr/share/novnc/index.html

# Create directories for configuration and downloaded files
RUN mkdir /data /config /config/logs /default-config

# Copy release
COPY --from=build /src/out /app

# Copy default configuration files
COPY --from=build /src/config.conf /default-config
COPY --from=build ["/src/OF DL/rules.json", "/default-config"]

COPY docker/supervisord.conf /etc/supervisor/conf.d/supervisord.conf
COPY docker/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

ENV DISPLAY=:0.0 \
	DISPLAY_WIDTH=1024 \
	DISPLAY_HEIGHT=768 \
	OFDL_PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium \
	OFDL_DOCKER=true

EXPOSE 8080
WORKDIR /config
ENTRYPOINT ["/sbin/tini", "--"]
CMD ["/app/entrypoint.sh"]
