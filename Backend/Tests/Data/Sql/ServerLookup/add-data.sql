insert into Realm (Name, CapacityOverrideBytes)
values ('Lookup-A', null)
    , ('Lookup-B', null)

insert into Server (RealmId, IpAddress, Name, DomainName, BandWidth, OsType, Features, IsActive)
select Id, '192.168.58.21', 'NAS-A1', 'nas-a1', 10737418240, 1, 0x400, 1 from Realm where Name = 'Lookup-A' union all
select Id, '192.168.58.22', 'NAS-A2', 'nas-a2', 10737418240, 1, 0x200, 0 from Realm where Name = 'Lookup-A' union all
select Id, '192.168.58.23', 'NAS-B1', 'nas-b1', 10737418240, 1, 0x100, 1 from Realm where Name = 'Lookup-B' union all
select Id, '192.168.58.24', 'RAD-B1', 'rad-b1', 10737418240, 1, 0x2,   1 from Realm where Name = 'Lookup-B'

insert into Server (RealmId, IpAddress, Name, DomainName, BandWidth, OsType, Features, IsActive)
values (null, '192.168.58.25', 'NAS-WILD', 'nas-wild', 10737418240, 1, 0x400, 1)
