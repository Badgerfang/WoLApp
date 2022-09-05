# WoLApp
Wake on LAN for local computers, other networks and networks behind firewalls

WoLApp is written in C# and compiles using .net V5.

It can be uses in several modes.

## Scenario 1

You want to wake up a computer on your local network with the MAC address 11:11:11:11:11:11

WolApp -w 11:11:11:11:11:11

## Scenario 2

You want to wake up a computer on your local network but want to keep the MAC addresses centralised. To do this create a text file with the name of the coumpters and their MAC address.

For example create a file called computers.txt with the following content

~~~~~~
* A test computer that will never wake up
TestComputer 11:11:11:11:11:11
Computer01 00:FF:21:95:FF:DB
~~~~~~

WolApp -ml computers.txt -w Computer01

## Scenario 3

You want to wake up the computer "Coumpter02" on another network but can only communicate with "Computer01" on that network. For this to work "Computer01" needs to run WoLApp in server mode.

The following tells WoLApp to run in server mode and listen on port 6000.

WoLApp -sm -sp 6000 -ml computers.txt

As a client you need to know the IP address of "Computer01".

WoLApp -rs 192.168.50.1:6000 -w Computer01,Computer02

You may not want to use the computer's actual name in WoLApp so this can be changed using the -sn option.

WoLApp -sm -sn OFFICE-PC -sp 6000 -ml computers.txt

The client's parameters also need to change so a network client is created to talk to the server running on Computer01

WoLApp -rs 192.168.50.1:6000 -w OFFICE-PC,Computer02

## Scenario 3

The Office's computers are not accesible due to firewalls etc. However, WoLApp can still be used by configuring bridges so a computer on the office network connects to another computer, maybe at some one's home location that is running WoLApp on a Rasperry PI.
The computer on the office network needs to create a bridge configuration file. 

For example create a file call Bridges.txt containing the following content, where the IP address is the IP address assigned to someone's home.

~~~~~~
Office 80.1.1.1:6000
~~~~~~

The computer in the office needs to run WoLApp defining the bridges

WoLApp -sm -sn OFFICE-PC -br Bridges.txt -ml Computers.txt

The router in someone's home needs to be updated to route port 6000 to the Raspyberry PI running WoLApp.

WoLApp -sm -sn PI -sp 6000

As a client you now need to send the wakeup for Computer02 via the Raspberryy PI using the IP address used at home. WoLApp will connect to the Raspberry PI via the internet on port 6000 and send the wakeup command. Since the first wakeup parameter isn't the local name the server will look for a bridge with the name "Office". Assuming that the computer in the office has sucessfully connected to the Raspberry PI then a bridge has been established and the WoLApp running on the Raspberry PI will send "OFFICE-PC,Computer02" over the bridge. WoLApp running on the computer in the office will receive the command and since it has the name OFFICE-PC it will perform a local network WoL for the computer Computer02 using the lookup from the file Computers.txt

WoLApp -rs 80.1.1.1:6000 -w Office,OFFICE-PC,Computer02

