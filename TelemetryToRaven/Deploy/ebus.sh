#!/bin/bash

curl -f http://localhost:8889/data/720/z1RoomTemp?required=true > /dev/null 2>&1
curl -f http://localhost:8889/data/720/z1ActualRoomTempDesired?required=true > /dev/null 2>&1
curl -f http://localhost:8889/data/hmu?required=true > /dev/null 2>&1
curl -f http://localhost:8889/data 2>\dev\null