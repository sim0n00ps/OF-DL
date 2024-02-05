FROM mcr.microsoft.com/dotnet/sdk:7.0 as build
COPY ["OF DL.sln", "/src/OF DL.sln"]
COPY ["OF DL", "/src/OF DL"]
WORKDIR "/src"
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:7.0
RUN apt update -y && apt install -y ffmpeg
COPY --from=build /src/out /app
ADD docker/entrypoint.sh /app
RUN chmod +x /app/entrypoint.sh
RUN mkdir /data
RUN mkdir /config
WORKDIR /config
CMD /app/entrypoint.sh

