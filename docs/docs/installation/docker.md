---
sidebar_position: 2
---

# Docker

## Running OF-DL

To run OF-DL in a docker container, follow the folling steps:

1. Install Docker Desktop (Windows, macOS) or Docker Engine (Linux) and launch it
2. Open your terminal application of choice (macOS Terminal, GNOME Terminal, etc.)
3. Create two directories, one called `config` and one called `data`.
    - An example might be:
        ```bash
        mkdir -p $HOME/ofdl/config $HOME/ofdl/data
        ```
        Adjust `$HOME/ofdl` as desired (including in the commands below) if you want the files stored elsewhere.
4. Run the following command to start the docker container:
    ```bash
    docker run --rm -it -v $HOME/ofdl/data/:/data -v $HOME/ofdl/config/:/config -p 8080:8080 ghcr.io/sim0n00ps/of-dl:latest
    ```
    If `config.json` and/or `rules.json` don't exist in the `config` directory, files with default values will be created when you run the docker container.
    If you have your own Widevine keys, those files should be placed under `$HOME/ofdl/config/cdm/devices/chrome_1610/`.
5. OF-DL to be authenticated with your OnlyFans account. When prompted, open [http://localhost:8080](http://localhost:3000) in a web browser to log in to your OnlyFans account.

## Updating OF-DL

When a new version of OF-DL is released, you can download the latest docker image by executing:

```bash
docker pull ghcr.io/sim0n00ps/of-dl:latest
```

You can then run the new version of OF-DL by executing the `docker run` command in the [Running OF-DL](#running-of-dl) section above.

## Building the Docker Image (Optional)

Since official docker images are provided for OF-DL through GitHub Container Registry (ghcr.io), you do not need to build the docker image yourself.
If you would like to build the docker image yourself, however, start by cloning the OF-DL repository and opening a terminal in the root directory of the repository.
Then, execute the following command while replacing `x.x.x` with the current version of OF-DL:

```bash
VERSION="x.x.x" docker build --build-arg VERSION=$VERSION -t of-dl .
```

You can then run a container using the image you just built by executing the `docker run` command in the
[Running OF-DL](#running-of-dl) section above while replacing `ghcr.io/sim0n00ps/of-dl:latest` with `of-dl`.
