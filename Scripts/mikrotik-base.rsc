# 2026-11-20 19:50:31 by RouterOS 7.20.6
# system id = 31JqUBQQMnO
#
/interface ethernet
set [ find default-name=ether1 ] disable-running-check=no
/user-manager limitation
add name=limit-traffic-25G transfer-limit=26843545600B
/user-manager profile
add name=profile-25G-10d name-for-users="25 Gigabytes 10 Days" validity=1w3d
/user-manager user
add disabled=yes name=User02
add name=User01
/ip address
add address=192.168.56.11 interface=ether1 network=192.168.56.11
/ip dhcp-client
add interface=ether1
/user-manager profile-limitation
add limitation=limit-traffic-25G profile=profile-25G-10d
/user-manager user-profile
add profile=profile-25G-10d user=*1
add profile=profile-25G-10d user=User02
add profile=profile-25G-10d user=*1
add profile=profile-25G-10d user=User02
add profile=profile-25G-10d user=User01
