#!/usr/bin/python3
import argparse
from PyP100 import PyP110 # PyP100v2 pip package
import json
parser = argparse.ArgumentParser(description='Poll information from a Tapo P110 or compatible plug, output as json')
parser.add_argument('host', help='the hostname or IP of the plug')
parser.add_argument('username', help='Tapo username/email address')
parser.add_argument('password', help='Tapo password')
args = parser.parse_args()

dev = PyP110.P110(args.host, args.username, args.password)
dev.handshake() #Creates the cookies required for further methods
dev.login() #Sends credentials to the plug and creates AES Key and IV for further methods

print("{ \"device_info\":")
print(json.dumps(dev.getDeviceInfo(), sort_keys=True, indent=2))
print(", \"energy_usage\":")
print(json.dumps(dev.getEnergyUsage(), sort_keys=True, indent=2))
print("}")