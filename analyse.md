# Photon Bypass Analyse

## Login

- Gmail auth
- email/username password
- register new

## Radius Calls

### Rad API

- Login
- User's Connection
- Acount Balance
- Usege Chart Reload
- Edit Info Page
- Change OVPN Password
- Rnewal

### Local DB

- Login
- Register
- Validation
- Name/Email Panel
- Wallet Balance Panel
- Usege Chart > Reload
- Payment
- Edit Info Page
- Change Login Password
- Change OVPN Password
- Rnewal

### Mikrotik SSH

- Close User Connection

### WhatsApp Businuss API (Send)

- Register / Validation
- Account Balance Warning Service

### Base On Pages

- Login
    - (rad api)
    - update (local db)
- Register
    - (local db)
    - (whatsapp api) verification
- User Page
    - Validation
        - (local db)
        - (whatsapp api) verification
    - Name/Email Panel
        - (local db)
    - Account State > User's connection
        - (rad api)
    - Account State > Close User Connection
        - (mikrotik ssh)
    - Account State Panel > Acount Balance
        - (rad api | rad db)
    - Wallet Balance Panel
        - (local db)
    - Usege Chart > Reload
        - (rad api | rad db) check new from latest
        - (local db)

- Payment
    - (local db)

- Edit Info Page
    - (rad api)
    - update (local db)

- Change Login Password 
    - (local db)

- Change OVPN Password
    - (rad api)
    - update (local db)

- Rnewal
    - (rad api)
    - update (local db)

- Account Balance Warning Service
    - (rad api | rad db)
    - (whatsapp api)

## Rnewal Submit Code

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
