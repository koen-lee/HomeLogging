#!/bin/bash

curl -f http://localhost:8889/data/720/z1RoomTemp?maxage=50 > /dev/null 2>&1
curl -f http://localhost:8889/data/720/z1ActualRoomTempDesired?maxage=50 > /dev/null 2>&1
curl -f http://localhost:8889/data/720/Hc1MinFlowTempDesired?maxage=50 > /dev/null 2>&1
curl -f http://localhost:8889/data/hmu/Status01?maxage=50 > /dev/null 2>&1
curl -f http://localhost:8889/data/hmu/SetMode?maxage=50 > /dev/null 2>&1
curl -f http://localhost:8889/data/hmu/State?maxage=50 > /dev/null 2>&1
curl -f http://localhost:8889/data 2>\dev\null