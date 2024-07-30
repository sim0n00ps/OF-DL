---
sidebar_position: 3
---

# Linux

A Linux release of OF-DL is not available at this time, however you can run OF-DL on Linux using Docker.
Please refer to the [Docker](/docs/installation/docker) page for instructions on how to run OF-DL in a Docker container.
If you do not have Docker installed, you can download it from [here](https://docs.docker.com/desktop/install/linux-install/).
If you would like to run OF-DL natively on Linux, you can build it from source by following the instructions below.

## Building from source

- Install the libicu library

```bash
sudo apt-get install libicu-dev
```

- Install .NET version 8

```bash
  wget https://dot.net/v1/dotnet-install.sh
  sudo bash dotnet-install.sh --architecture x64 --install-dir /usr/share/dotnet/ --runtime dotnet --version 8.0.7
```

- Clone the repo

```bash
git clone https://github.com/sim0n00ps/OF-DL.git
cd 'OF-DL'
```

- Build the project. Replace `%VERSION%` with the current version number of OF-DL (e.g. `1.7.68`).

```bash
dotnet publish -p:Version=%VERSION% -c Release
cd 'OF DL/bin/Release/net8.0'
```

- Download the windows release as described on [here](/docs/installation/windows#installation).
- Add the `auth.json`, `config.json`, and `rules.json` files as well as the `cdm` folder to the `OF DL/bin/Release/net8.0` folder.

- Run the application

```bash
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 ./'OF DL'
```
