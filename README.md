# Photon Bypass

selling VPN

## Home

[crossfit-athletes-website-template](https://nicepage.com/st/46692/crossfit-athletes-website-template)

[plan-and-book-your-flights-website-template](https://nicepage.com/st/57476/plan-and-book-your-flights-website-template)

Slogan:
    - types of plans with price
        - traffic without time
    - return value in middle of the plan
    - first 3 days is free

## Profile

[CoreUI](https://coreui.io/product/free-angular-admin-template/#live-preview)

### Dashbord

- Home Page
    - Slogan
    - Login => page
    - Register => page

- Register Page
    - Human Test
    - User Name *
    - Email (* or mobile) => validate email
    - Mobile (* or email) => validate whatsapp
    - First Name
    - Last Name
    - Password *
    - Submit => send admin notif

- Login
    - Human Test
    - User Name
    - Password

- User Page
    - Email Validation
        - Validation Code
        - Resent
    - Support Panel
        - Last Admin's Message
        - Chat Button => page
    - Name/Email Panel (cache 10 min)
        - Edit Info Button (name/phone/email) => page
        - Change Password Button => page
        - Send Config Button => send
    - Account State Panel
        - User's connection (last connection/live time)
        - Close User Connection Button => close connection in mikrotik
        - Acount Balance
        - Renewal/Change Button => page
    - Wallet Balance Panel
        - Current Value
        - Increament Button => Increment Popup => to bank page
    - Usege Chart
        - Reload Button => reload data
    - Logout => logout => home page

- Edit Info Button
    - Email * => validate email
    - Mobile * (to send information via whatsapp) => validate mobile
    - Primary Contact (email|whatsapp)
    - First Name
    - Last Name

- Change Password
    - Old Password
    - New Password

- Rnewal
    - Wallet Balance Panel
        - Current Value
        - Increament Button => Increment Popup => to bank page
    - Users Count <= user count
    - Monthly <= month (enabled: account is disabled | account is monthly)
    - Traffic <= x25 (enabled: account is disabled | account is trafficaly)
    - Submit =>
        ```c#
        if (context.month is not null && context.traffic is not null)
            throw new Excetion("Bad request.");
        if ((context.month is not null || context.traffic is not null) && account.Enabled == true)
            throw new UserException("You can not change account type while is enabled!");

        if (context.traffic is not null) {
            context.type = AccountType.Traffic;
        } else if (context.month is not null) {
            context.type = AccountType.Monthly;
        } else if (context.UserCount != account.UserCount) {
            context.type = account.Type;
        } else {
            return;
        }

        if (context.traffic is null || context.month is null || 
            context.UserCount == account.UserCount) {
            return;
        }
        
        var close_connections = true;
        if (context.traffic is not null && account.Type != AccountType.Traffic) {
            if (account.ExpireDate > DateTime.Now) {
                throw new UserException("The account plan has not finished!");
            }

            var current_total_data_usage = GetUserDataUsage(account.UserID);
            SetAccountDataUsege(current_total_data_usage);
        
        } else if (context.month is not null && account.Type != AccountType.Monthly) {
            if (account.RemainTraffic > 512 * 1024 * 1024) {
                throw new UserException("The account plan has not finished! you need use lan until 512MB.");
            }

            SetAccountDate(DateTime.Now, DateTime.Now);

        } else {
            close_connections = context.UserCount != account.UserCount;
        }
        
        SetAccountProfile(account, context, out expense);

        if (!CheckUserWalet(expense)) {
            throw new UserException("The walet balance is low!");
        }

        if (account.LastConnection < DateTime.Now.AddWeek(-1)) {
            var realm = GetFreePlaceInServer();
            if (account.Realm != realm) {
                account.Realm = realm;
                SendChangeServerNotif(account)
            }
        }

        SaveAccount(account);

        if (close_connections) {
            CloseConnection();
        }

        CheckUserServerBalance();
        ```

### Accoutnt Management System

- Restrict accounts to connect to just one server
- Alarm Admin if users count is more than 80% of server capacity
- Notif on new Users
- Notif on invalid password

## Pheases

1. Web Site Core
2. Fake Website
    1. Enamad
    2. samandehi.ir
    3. Bank Payment Gateway
2. Send WhatsApp Notif
