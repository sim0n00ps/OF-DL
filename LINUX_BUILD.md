# Follow these instructions to build and run the application in a linux system

### Disclaimer
##### These instructions were tested and worked properly in a Debian distro

### Instructions

- Install the libicu library
`sudo apt-get install libicu-dev`

- Install .NET version 8
`wget https://dot.net/v1/dotnet-install.sh`
`sudo bash dotnet-install.sh --architecture x64 --install-dir /usr/share/dotnet/ --runtime dotnet --version 8.0.19`

- Clone the repo
`git clone https://github.com/sim0n00ps/OF-DL.git`
`cd 'OF-DL'`

- Build the project
`dotnet build`
`cd 'OF DL/bin/Debug/net8.0'`

- Add the .json files like stated in README.md
- Run the application
`./'OF DL'`
