# Photon Bypass Plan

Selling VPN

- Advertisement Pages
- Profile Pages
- Background Services

## Advertisement Pages

We have just home page in this category.

### Home

Page Elements:

- Slogan *(panel)*:
    - Types of plans with price
        - Traffic without time
    - Return value in the middle of the plan
    - first 3 days is free
- Start *(Button)* => link to login page

Design Info:

[crossfit-athletes-website-template](https://nicepage.com/st/46692/crossfit-athletes-website-template)

[plan-and-book-your-flights-website-template](https://nicepage.com/st/57476/plan-and-book-your-flights-website-template)

## Profile Pages

Design Info:

[CoreUI](https://coreui.io/product/free-angular-admin-template/#live-preview)

### Login

Properties:

- Human Test *(auto action)* => to prevent robats

Elements:

- Human Test *(auto action)* => to prevent robats
- Google Auth *(button)* => (phease 5)
- Register *(link)* => go to register page
- Forget Password *(link)* => go to forget-password page
- User Name *(required input)*
- Password *(required input)*
- Submit *(button)*
    - Notif admin on invalid password

**TODO: Human Test**

### Register

Properties:

- Human Test *(auto action)* => to prevent robats
- This page is not in menu

Elements:

- User Name *(required input)*
- Email *(required input)*
- Mobile *(input)*
- First Name *(input)*
- Last Name *(input)*
- Password *(required input)*
- Submit *(button)*
    - one of `email` or `mobile` are required (phease 4)
    - send admin notif on submit

**TODO: send admin notif on submit**\
**TODO: Human Test**

### Forgot Password

Properties:

- Human Test *(auto action)* => to prevent robats
- This page is not in menu

Elements:

- Email/Mobile *(input)*
- Submit *(button)* => send code

**TODO: Human Test**

### Reset Password

Properties:

- No link to this page

Elements:

- HashCode *(hiddent input)* => from query string
- Password *(required input)*
- Submit *(button)* => to change password

### Dashbord

Elements:

- Email Validation *(panel)*
    - Show if email/mobile are not valid
    - Validation Code *(input)*
    - Send/Resend *(button)*
    - Submit *(button)*
- Support Panel *(panel)*
    - Chat Button *(link)* => outer link to WhatsApp page
    - feedback button (phease 4)
- Account Info *(panel)*
    - Cache user data
    - Edit Account Info *(link)* => Edit page
    - Change Password *(link)* => Change-Password page
    - Change OVPN Password *(link)* => Change-Password page
    - Send Config *(button)*
        - Check send limit
        - Renew private key pass
        - Send
- Account State *(panel)*
    - Show last connection list and live time
    - Close User Connection Button => close connection in mikrotik
    - Acount Balance
    - Renewal/Change Button => Renewal Page
- Wallet Balance *(panel)*
    - Usege Chart will not load data on page initialize
    - Current Value
    - Increament Button => Payment Popup => Payment Page
- Usege Chart *(panel)*
    - Reload *(button)* => reload data
- Logout *(link)* => logout => home page

**TODO: Check send limit**

### Change Password

in two mode (OpenVpn | Account)

Elements:

- Type *(hidden input)* => (OpenVpn | Account)
- Old Password *(input)*
- New Password *(input)*

### Payment Popup

Elements:

- Increment Value *(readonly)*
- Payment Way *(select)*
- Submit => to bank page

### History

Log Record Types:

- increase/decrease wallet
- increase plan
- auto sent messages
- closing connection

### Edit Info Page

- Email Validation *(panel)*
    - Show if email/mobile are not valid
    - Validation Code *(input)*
    - Send/Resend *(button)*
    - Submit *(button)*
- Email *(input)*
- Mobile *(input)*
    - to send information via whatsapp (phease 4)
- Primary Contact *(select)*
    - email|whatsapp
    - (phease 4)
- First Name *(input)*
- Last Name *(input)*
- Submit
    - one of `email` or `mobile` are required (phease 4)

### FAQ

### Renewal

- Wallet Balance Panel
    - Current Value
    - Increament Button => Payment Popup
- Users Count *(select)*
- Monthly *(select)* <= month (enabled: account is disabled | account is monthly)
- Traffic *(select)* <= x25 (enabled: account is disabled | account is trafficaly)
- Submit *(button)* =>
    - On Change to Traffic: Make sure account time is finished
    - On Change to Monthly: Make sure account traffiic is finished
    - Change Profile and Data if necessary
    - Close extra connections
    - On old account: get empty place
    - Check Server/User Balance

**TODO: enable -> Monthly/Traffic**

## Background Services

### Servcer Capacity Management

- Server Capacity Alarm
    - Alarm Admin if users count is more than 70 percent of server capacity
    - Alarm Admin if users count is less than 100 percent of server capacity
- Restrict accounts to connect to just one server
- Get free place for new renewal

### Account Management System

- Notif user for end of service
- Deactive affter a week not renewal

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
    - feedback button
5. Google Auth
6. Other features
