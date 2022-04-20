#!/bin/bash

curl http://localhost:8889/data/720/z1RoomTemp?required=true > /dev/null
curl http://localhost:8889/data/720/z1ActualRoomTempDesired?required=true > /dev/null
curl http://raspberrypi:8889/data/hmu?required=true
curl http://localhost:8889/data