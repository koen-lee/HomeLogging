#!/bin/bash
result=$(curl 'http://localhost:8080/databases/Eiland17Logging/queries?addTimeSeriesNames=true' \
  -H 'Accept: application/json, text/javascript, */*; q=0.01' \
  -H 'Content-Type: application/json; charset=UTF-8' \
  --data-raw $'{"Query":"from \'Meters\'\\r\\nwhere id() == \'meters/ISK5\\\\\\\\2M550T-1013\'\\r\\nselect timeseries(from \'Power\' last 15 minutes select avg())","Start":0,"PageSize":101,"QueryParameters":{}}' \
)

power=$(echo $result | jq .Results[0].Results[0].Average[0])
echo Power: $power
if (( $( bc <<< "$power < -300" ) ))
then
        echo $(date) Boiler PV mode: $power | tee -a ~/lastboilerswitch.txt
        echo "in" > /sys/class/gpio/gpio14/direction
else
        echo $(date) Boiler Night mode: $power | tee -a ~/lastboilerswitch.txt
        echo "out" > /sys/class/gpio/gpio14/direction
fi