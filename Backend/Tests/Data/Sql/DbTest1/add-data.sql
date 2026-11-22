insert into PanelUser (Username, Password, UserGroupId)
values ('User99', 'password', 1)
    , ('User2', 'password', 1)
    , ('User3', 'password', 1)

insert into PanelUser (Username, Password, Owner, UserGroupId)
select 'User11', 'password', Id, 1 from PanelUser where Username = 'User99' union
select 'User12', 'password', Id, 1 from PanelUser where Username = 'User99' union
select 'User13', 'password', Id, 1 from PanelUser where Username = 'User99' union
select 'User14', 'password', Id, 1 from PanelUser where Username = 'User99';

insert into PanelUser (Username, Password, Owner, UserGroupId)
select 'User21', 'password', Id, 1 from PanelUser where Username = 'User2' union
select 'User22', 'password', Id, 1 from PanelUser where Username = 'User2';

-- Docs/22: VPN identities live on VpnAccount (RealmId NOT NULL, username
-- starts with the owner's panel username). User99 carries the one the
-- GetAccountId lookup resolves.
insert into Realm (Name)
values ('DbTest1-Realm')

insert into VpnAccount (UserId, RealmId, Username, VpnPassword)
select pu.Id, r.Id, pu.Username, 'password'
from PanelUser pu cross join Realm r
where pu.Username = 'User99' and r.Name = 'DbTest1-Realm'
