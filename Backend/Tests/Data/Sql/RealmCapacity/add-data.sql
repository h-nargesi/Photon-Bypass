insert into Realm (Name, CapacityOverrideBytes)
values ('Cap-Free', null)
    , ('Cap-Pinned', 5368709120)
    , ('Cap-Closed', 0)

insert into Server (RealmId, IpAddress, Name, DomainName, BandWidth, OsType, Features)
select Id, '192.168.57.11', 'NAS-Free', 'nas-free', 10737418240, 1, 0x400 from Realm where Name = 'Cap-Free' union all
select Id, '192.168.57.12', 'NAS-Pinned', 'nas-pinned', 10737418240, 1, 0x400 from Realm where Name = 'Cap-Pinned' union all
select Id, '192.168.57.13', 'NAS-Closed', 'nas-closed', 10737418240, 1, 0x400 from Realm where Name = 'Cap-Closed'
