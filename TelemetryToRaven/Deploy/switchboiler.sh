#!/bin/bash
result=$(curl -s 'http://tinkerboard:8080/databases/Eiland17Logging/queries?addTimeSeriesNames=true' \
  -H 'Accept: application/json, text/javascript, */*; q=0.01' \
  -H 'Content-Type: application/json; charset=UTF-8' \
  --data-raw $'{"Query":"from \'Meters\'\\r\\nwhere id() == \'meters/ISK5\\\\\\\\2M550T-1013\'\\r\\nselect timeseries(from \'Power\' last 5 minutes select percentile(50))","Start":0,"PageSize":101,"QueryParameters":{}}' \
)
pin='/sys/class/gpio/gpio14/direction'
power=$(echo $result | jq .Results[0].Results[0].Percentile[0])
echo Power: $power
[ -f $pin ] || echo 14 > /sys/class/gpio/export
if (( $( bc <<< "$power < -300" ) ))
then
        echo "in" > $pin
fi
if (( $( bc <<< "$power > 30" ) ))
then
        echo "out" > $pin
fi

timestamp="$(date -u +%FT%TZ)"
pvMode=$([[ $(cat $pin) == 'in' ]] && echo "1" || echo "0")

echo $timestamp Boiler PV mode: $pvMode Power: $power

curl 'http://tinkerboard:8080/databases/Eiland17Logging/timeseries?docId=meters%2FHeatPumpBoiler' \
  --data-raw '{"Name":"PVMode","Appends":[{"Tag":null,"Timestamp":"'"$timestamp"'","Values":['"$pvMode"']}]}'