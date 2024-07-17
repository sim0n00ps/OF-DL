#!/bin/bash

mkdir -p /config/cdm/devices/chrome_1610

if [ ! -f /config/auth.json ]; then
	cp /default-config/auth.json /config/auth.json
fi

if [ ! -f /config/config.json ]; then
	cp /default-config/config.json /config/config.json
fi

if [ ! -f /config/rules.json ]; then
	cp /default-config/rules.json /config/rules.json
fi

/app/OF\ DL
