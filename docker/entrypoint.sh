#!/bin/bash

mkdir -p /config/cdm/devices/chrome_1610
mkdir -p /config/logs/

if [ ! -f /config/config.conf ] && [ ! -f /config/config.json ]; then
  cp /default-config/config.conf /config/config.conf
fi

if [ ! -f /config/rules.json ]; then
  cp /default-config/rules.json /config/rules.json
fi

{
  supervisord -c /etc/supervisor/conf.d/supervisord.conf &
} &> /dev/null

# Wait for the 3 supervisor programs to start: X11 (Xvfb), X11vnc, and noVNC
NUM_RUNNING_SERVICES=$(supervisorctl -c /etc/supervisor/conf.d/supervisord.conf status | grep RUNNING | wc -l)
while [ $NUM_RUNNING_SERVICES != "3" ]; do
  sleep 1
  NUM_RUNNING_SERVICES=$(supervisorctl -c /etc/supervisor/conf.d/supervisord.conf status | grep RUNNING | wc -l)
done

/app/OF\ DL
