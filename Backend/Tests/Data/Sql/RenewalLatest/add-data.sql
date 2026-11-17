insert into PanelUser (Username, Password, UserType)
values ('RenewA', 'password', 4)
    , ('RenewB', 'password', 4)

-- Docs/22: renewals stack per VpnAccount (RealmId NOT NULL, username = the
-- owner's panel username — D3 empty suffix)
insert into Realm (Name)
values ('RenewLatest')

insert into VpnAccount (UserId, RealmId, Username, VpnPassword)
select pu.Id, r.Id, pu.Username, 'password'
from PanelUser pu cross join Realm r
where pu.Username in ('RenewA', 'RenewB') and r.Name = 'RenewLatest'

-- one debit row per renewal (Renewal.WalletDebit is a NOT NULL foreign key);
-- wallet rows anchor to the panel user (Docs/22 D8)
insert into Wallet (AccountId, Amount, Direction, Status, Description)
select Id, 500, -1, 1, 'T-renew-debit-a1' from PanelUser where Username = 'RenewA' union all
select Id, 500, -1, 1, 'T-renew-debit-a2' from PanelUser where Username = 'RenewA' union all
select Id, 500, -1, 1, 'T-renew-debit-b1' from PanelUser where Username = 'RenewB'

-- separate statements so the identity order (old < new) is deterministic
insert into Renewal (VpnAccountId, SimultaneousUser, WalletDebit, Comment)
select va.Id, 1, w.Id, 'T-renew-a-old'
from VpnAccount va join Wallet w on w.AccountId = va.UserId and w.Description = 'T-renew-debit-a1'
where va.Username = 'RenewA'

insert into Renewal (VpnAccountId, SimultaneousUser, WalletDebit, Comment)
select va.Id, 2, w.Id, 'T-renew-a-new'
from VpnAccount va join Wallet w on w.AccountId = va.UserId and w.Description = 'T-renew-debit-a2'
where va.Username = 'RenewA'

insert into Renewal (VpnAccountId, SimultaneousUser, WalletDebit, Comment)
select va.Id, 1, w.Id, 'T-renew-b-only'
from VpnAccount va join Wallet w on w.AccountId = va.UserId and w.Description = 'T-renew-debit-b1'
where va.Username = 'RenewB'
