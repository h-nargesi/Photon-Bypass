# Photon Bypass Analyse

api call rate limit

## API list

- prices:
    GET  api/basics/prices
    response `[{
        title: string;
        caption: string;
        description: string[];
    }]`
- token:
    POST api/auth/token `{
        username: string; 
        password: string;
    }`
- logout:
    GET  api/auth/logout
- register:
    POST api/auth/register `{
        username: string;
        email: string;
        emailValid: boolean;
        mobile: string;
        mobileValid: boolean;
        firstname: string;
        lastname: string;
        password: string;
    }`
- get-user:
    GET  api/auth/get-user
- check-user:
    GET  api/auth/check-user
- change-pass:
    POST api/auth/change-pass `{
        token: string;
        password: string;
    }`
- reset-pass
    POST api/auth/reset-pass `{
        emailMobile: string;
    }`
- change-ovpn:
    POST api/account/change-ovpn `{
        token: string;
        password: string;
        target?: string;
    }`
- send-cert-email:
    GET  api/account/send-cert-email `{
        target?: string;
    }`
- traffic-data:
    GET  api/account/traffic-data `{
        target?: string;
    }`
    response `{
        title: string;
        collections: TrafficData[];
        labels: string[];
    }`
- full-info:
    GET  api/account/full-info `{
        target?: string;
    }`
    response `{
        username: string;
        email: string;
        emailValid: boolean;
        mobile: string;
        mobileValid: boolean;
        firstname: string;
        lastname: string;
    }`
- history:
    GET  api/account/history `{
        target?: string;
        from?: number;
        to?: number;
    }`
    response `[{
        id: number;
        target: string;
        eventTime: number;
        eventTimeTitle: string;
        title: string;
        color: string;
        value?: any;
        unit?: string;
        description?: string;
    }]`
- edit-user:
    POST api/account/edit-user `{
        email: string;
        mobile: string;
        firstname: string;
        lastname: string;
    }`
- current-con-state:
    GET  api/connection/current-con-state `{
        target?: string;
    }`
    response `number[]`
- close-con:
    POST api/connection/close-con `{
        index: number;
        target?: string;
    }`
- plan-state:
    GET  api/plan/plan-state `{
        target?: string;
    }`
    response `{
        type: PlanType;
        remainsTitle: string;
        remainsPercent: number;
        simultaneousUserCount: number;
    }`
- plan-info:
    GET  api/plan/plan-info `{
        target?: string;
    }`
    response `{
        target: string;
        type: PlanType;
        value: number;
        simultaneousUserCount: number;
    }`
- estimate:
    POST api/plan/estimate `{
        target: string;
        type: PlanType;
        value: number;
        simultaneousUserCount: number;
    }`
    response `number`
- rnewal:
    POST api/plan/rnewal `{
        target: string;
        type: PlanType;
        value: number;
        simultaneousUserCount: number;
    }`
    response `{
        currentPrice: number;
        moneyNeeds: number;
    }`

## Login

- Gmail auth
- Email/username password
- Register new

## Out Source Calls

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
        - (rad api) check new from latest
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
    - (rad api)
    - (whatsapp api)

## Rnewal Submit Code

this code is not finished

```c#
if (context.month is not null && context.traffic is not null)
    throw new Excetion("Bad request.");

EstimateExpense(account, context, out expense);

if (!AffordFromUserWalet(expense)) {
    throw new UserException("The walet balance is low!");
}

var change_profile = true;
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
    close_connections = context.UserCount < account.UserCount;
    change_profile = context.UserCount != account.UserCount;
}

if (change_profile) {
    account.profile = GetProfile(context);
}

if (account.LastConnection < DateTime.Now.AddWeek(-1)) {
    var realm = GetFreePlaceInServer();
    if (account.Realm != realm) {
        account.Realm = realm;
        SendChangeServerNotif(account);
    }
}

if (account.IsChanged) {
    SaveAccount(account);
    SaveAccountRealmDistriction(account);
}

RegisterNewTopUp(account, context);

if (close_connections) {
    CloseConnection();
}

CheckUserServerBalance();
```
