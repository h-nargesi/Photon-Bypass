# 2026-11-20 19:49:25 by RouterOS 7.20.6
# system id = b/WNypMnSkD
#
/interface ethernet
set [ find default-name=ether1 ] disable-running-check=no
/ip address
add address=192.168.56.12/24 interface=ether1 network=192.168.56.0
/ip dhcp-client
add interface=ether1
