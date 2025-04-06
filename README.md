# Photon Bypass Plan

Selling VPN

## Home

[crossfit-athletes-website-template](https://nicepage.com/st/46692/crossfit-athletes-website-template)

[plan-and-book-your-flights-website-template](https://nicepage.com/st/57476/plan-and-book-your-flights-website-template)

- Home Page
    - Slogan:
        - Types of plans with price
            - Traffic without time
        - Return value in the middle of the plan
        - first 3 days is free
    - Login => page
    - Register => page

## Profile

[CoreUI](https://coreui.io/product/free-angular-admin-template/#live-preview)

### Dashbord

- Register Page (not in menu)
    - Human Test
    - User Name *
    - Email (* or mobile) => validate email
    - Mobile (* or email) => validate whatsapp
    - First Name
    - Last Name
    - Password *
    - Submit => send admin notif

- Login (not in menu)
    - Human Test
    - Google Auth
    - User Name
    - Password

- Forgot password (not in menu)
    - Email/Mobile
    - Submit => send code

- Change Password
    - (OpenVpn | Account)
    - Old Password
    - New Password

- Reset password (hidden)
    - Password *
    - Submit => change password

- Dashboard Page
    - Email Validation
        - Validation Code
        - Resent
    - Support Panel
        - Last Admin's Message
        - Chat Button => page
    - Name/Email Panel (cache 10 min)
        - Edit Info Button (name/phone/email) => page
        - Change Password Button => page
        - Change OVPN Password Button => page
        - Send Config Button =>
            - check send limit
            - renew private key pass
            - send
    - Account State Panel
        - User's connection (last connection/live time)
        - Close User Connection Button => close connection in mikrotik
        - Acount Balance
        - Renewal/Change Button => page
    - Wallet Balance Panel
        - Current Value
        - Increament Button => Payment Popup
    - Usege Chart
        - Reload Button => reload data
    - Logout => logout => home page

- Payment
    - Increment Value
    - Payment Way
    - Submit => to bank page

- History
    - increase/decrease wallet
    - increase plan

- Edit Info Page
    - Email * => validate email
    - Mobile * (to send information via whatsapp) => validate mobile
    - Primary Contact (email|whatsapp)
    - First Name
    - Last Name

- FAQ

- Rnewal
    - Wallet Balance Panel
        - Current Value
        - Increament Button => Payment Popup
    - Users Count <= user count
    - Monthly <= month (enabled: account is disabled | account is monthly)
    - Traffic <= x25 (enabled: account is disabled | account is trafficaly)
    - Submit =>
        - On Change to Traffic: Make sure account time is finished
        - On Change to Monthly: Make sure account traffiic is finished
        - Change Profile and Data if necessary
        - Close extra connections
        - On old account: get empty place
        - Check Server/User Balance

### Accoutnt Management System

- Restrict accounts to connect to just one server
- Server Capacity Alarm
    - Alarm Admin if users count is more than 50 users of server capacity
    - Alarm Admin if users count is less than 100 users of server capacity
- Notif on new Users
- Notif on invalid password

## Pheases

1. Web Site Core
    - Consider Unit Test
    - Cloudflare Human Test
    - Multi Account Panel
        - user-service onTargetChanged event
        - optional target in history
2. MySql Performance
    - Ask ChatGPT
    - Search in Youtube
3. Fake Website
    1. Enamad
    2. samandehi.ir
    3. Bank Payment Gateway
4. Send WhatsApp Notif
5. Google Auth
6. Other features
