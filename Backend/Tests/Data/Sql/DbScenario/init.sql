insert into Price (State, Title, Caption, Description, CalculatorCode)
values (2, 'ترافیکی', 
        'به مقدار مشخصی ترافیک (۲۵ گیگ) و تقریبا بدون محدودیت زمانی از vpn استفاده کنید.',
        '25G ترافیک تک کاربره ۱۵۰ تومن
هر 25G ترافیک بیشتر ۸۰ تومن
هر کاربر بیشتر ۲۰ تومن',
        'using System;
public class Calculator
{
    public static int Compute(int users, int days, int gigabytes)
    {
        if (gigabytes <= 0) throw new Exception("Invalid gigabytes value.");
        return 60 + users * 10 + (gigabytes / 25) * 50;
    }
}')
    , (1, '', 
        '', 
        '',
       'using System;
public class Calculator
{
    public static int Compute(int users, int days, int gigabytes)
    {
        if (gigabytes > 0)
        {
            return 40 + users * 10 + (gigabytes / 25) * 40;
        }

        if (days > 0)
        {
            return (days / 30) * 100;
        }

        throw new Exception("Invalid gigabytes/days value.");
    }
}')

insert into Realm (Name)
values ('Abr01');

insert into Server (RealmId, IpAddress, Name, DomainName, BandWidth, OsType, Features, Config)
select Id
    , '192.168.56.12'
    , 'MK-Scenario'
    , 'scen'
    , 10737418240
    , 1
    , 0x402
    , '{
    "WebApiConfig": {
        "HostName": "192.168.56.12",
        "Username": "admin",
        "Password": "admin",
        "Port": 8728,
        "Ssl": false
    }
}'
from Realm