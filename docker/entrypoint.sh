#!/bin/bash

mkdir -p /config/cdm/devices/chrome_1610

if [ ! -f /config/config.json ]; then
	cp /default-config/config.json /config/config.json
fi

if [ ! -f /config/rules.json ]; then
	cp /default-config/rules.json /config/rules.json
fi

{
  supervisord -c /etc/supervisor/conf.d/supervisord.conf &
} &> /dev/null

/app/OF\ DL
