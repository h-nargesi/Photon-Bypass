-- Docs/24 fixtures: the default group arrives from the structure scripts
-- (UserGroup.sql seeds «وب سایت»); plans replace the old marketing rows and
-- carry the closed calculator contract Compute(PriceCalculationInput).

insert into Plans (Name, Title, Caption, Description, ShowOnWebsite, IsActive, GroupId,
	UsersBase, UsersCap, DaysBase, DaysCap, TrafficBase, TrafficCap, CalculatorCode)
values ('Scenario-Complete', 'ترافیکی',
        'به مقدار مشخصی ترافیک (۲۵ گیگ) و تقریبا بدون محدودیت زمانی از vpn استفاده کنید.',
        '25G ترافیک تک کاربره ۱۵۰ تومن
هر 25G ترافیک بیشتر ۸۰ تومن
هر کاربر بیشتر ۲۰ تومن',
        1, 1, 1,
        1, 6, 30, 180, 25, 200,
        'using System;
using PhotonBypass.Domain.Static;
public class Calculator
{
    public static PriceCalculationOutput Compute(PriceCalculationInput input)
    {
        if (input.Gigabytes is not int gigabytes || gigabytes <= 0) throw new Exception("Invalid gigabytes value.");
        return new PriceCalculationOutput
        {
            Price = 60 + (input.Users ?? 1) * 10 + (gigabytes / 25) * 50,
            Users = input.Users,
            Days = input.Days,
            Gigabytes = gigabytes,
        };
    }
}')
    , ('Scenario-Trafiki', 'اقتصادی', '', '', 0, 1, 1,
        null, null, null, null, 25, 300,
        'using System;
using PhotonBypass.Domain.Static;
public class Calculator
{
    public static PriceCalculationOutput Compute(PriceCalculationInput input)
    {
        if (input.Gigabytes is not int gigabytes || gigabytes <= 0) throw new Exception("Invalid gigabytes value.");
        var packages = gigabytes / 25;
        return new PriceCalculationOutput
        {
            Price = 40 + packages * 40,
            Users = null,
            Days = (short)(60 + packages * 30),
        };
    }
}')

insert into Realm (Name, DisplayName)
values ('Abr01', N'آلمان ۱')

insert into PlanRealm (PlanId, RealmId)
select p.Id, r.Id
from Plan p cross join Realm r

insert into Server (RealmId, IpAddress, Name, DomainName, BandWidth, OsType, Features, Config)
select Id
    , '192.168.56.12'
    , 'MK-Scenario'
    , 'scen'
    , 10737418240
    , 1
    , 0x402
    , '{
    "SshConfig": {
        "HostName": "192.168.56.12",
        "Username": "admin",
        "Password": "admin",
        "Port": 22
    },
    "WebApiConfig": {
        "HostName": "192.168.56.12",
        "Username": "admin",
        "Password": "admin",
        "Port": 8728,
        "Ssl": false
    }
}'
from Realm

-- scenario-only admin fixture (Docs/20: Role=2 Admin + UserType=4 AllowSelfService;
-- password 'admin' — same committed PBKDF2 test-fixture family as the dev appsettings fixtures).
-- production never seeds an admin: DeployCli creates it at deploy time (Docs/19).
-- Docs/22 D9: a panel user is seeded bare — VPN accounts arrive with purchases.
-- Docs/24 D6: the admin joins the default group (id 1 from the structure seed).
insert into PanelUser (Username, Password, Role, UserType, UserGroupId)
values ('admin', '$PBKDF2$v1$210000$o5kj88mA9pISrUSyKVsJqQ==$Ve055Dx3OaviuxbhfnsnGj/hHb3UFWFYTOrIHbYyN88=', 2, 4, 1)
