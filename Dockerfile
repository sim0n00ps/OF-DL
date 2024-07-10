FROM alpine as build

ARG VERSION

RUN apk --repository community add dotnet8-sdk

# Copy source code
COPY ["OF DL.sln", "/src/OF DL.sln"]
COPY ["OF DL", "/src/OF DL"]

WORKDIR "/src"

# Build release
RUN dotnet publish -p:Version=$VERSION -c Release --self-contained true -p:PublishSingleFile=true -o out

FROM alpine as final

# Install dependencies
RUN apk --repository community add ffmpeg bash dotnet8-runtime bash

# Copy release and entrypoint script
COPY --from=build /src/out /app
ADD docker/entrypoint.sh /app
RUN chmod +x /app/entrypoint.sh

RUN mkdir /data  # For downloaded files
RUN mkdir /config  # For configuration files

WORKDIR /config
CMD /app/entrypoint.sh
