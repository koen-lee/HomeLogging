#!/usr/bin/python3
import argparse
from PyTapo.devices import P110
import json
parser = argparse.ArgumentParser(description='Poll information from a Tapo P110 or compatible plug, output as json')
parser.add_argument('host', help='the hostname or IP of the plug')
parser.add_argument('username', help='Tapo username/email address')
parser.add_argument('password', help='Tapo password')
args = parser.parse_args()

dev = P110(args.host, args.username, args.password)

print("{ device_info:")
print(json.dumps(dev.get_device_info(), sort_keys=True, indent=2))
print(" energy_usage:")
print(json.dumps(dev.get_energy_usage(), sort_keys=True, indent=2))
print("}")