# Follow these instructions to build and run the application in a linux system

### Disclaimer
##### These instructions were tested and worked properly in a Debian distro

### Instructions

- Install the libicu library
`sudo apt-get install libicu-dev`

- Install .NET version 7  
`wget https://dot.net/v1/dotnet-install.sh`
`sudo bash dotnet-install.sh --architecture x64 --install-dir /usr/share/dotnet/ --runtime dotnet --version 7.0.19`

- Clone the repo
`git clone https://github.com/sim0n00ps/OF-DL.git`
`cd 'OF DL'`

- Build the project
`dotnet install`
`cd 'OF DL/bin/Debug/net7.0'`

- Add the .json files like stated in README.md
- Run the application
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 ./'OF DL'`
