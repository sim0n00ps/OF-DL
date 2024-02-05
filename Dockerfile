FROM mcr.microsoft.com/dotnet/sdk:7.0 as build

# Copy source code
COPY ["OF DL.sln", "/src/OF DL.sln"]
COPY ["OF DL", "/src/OF DL"]

WORKDIR "/src"

# Build release
RUN dotnet publish -c Release -o out


FROM mcr.microsoft.com/dotnet/runtime:7.0

# Install dependencies
RUN apt update -y && apt install -y ffmpeg

# Copy release and entrypoint script
COPY --from=build /src/out /app
ADD docker/entrypoint.sh /app
RUN chmod +x /app/entrypoint.sh

RUN mkdir /data  # For downloaded files
RUN mkdir /config  # For configuration files

WORKDIR /config
CMD /app/entrypoint.sh
