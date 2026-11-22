insert into PanelUser (Username, Password, UserGroupId)
values ('RenewA', 'password', 1)
    , ('RenewB', 'password', 1)

-- Docs/24: renewals reference their plan (NOT NULL FK) — one fixture plan.
insert into Plans (Name, Title, IsActive, GroupId, UsersBase, UsersCap, DaysBase, DaysCap, TrafficBase, TrafficCap, CalculatorCode)
values ('RenewLatest-Fixture', 'تست', 1, 1, 1, 6, 30, 180, 25, 200,
        'using System; using PhotonBypass.Domain.Static; public class Calculator { public static PriceCalculationOutput Compute(PriceCalculationInput input) => new PriceCalculationOutput { Price = 100, Users = input.Users, Days = input.Days, Gigabytes = input.Gigabytes }; }')

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
insert into Renewal (VpnAccountId, PlanId, SimultaneousUser, WalletDebit, Comment)
select va.Id, p.Id, 1, w.Id, 'T-renew-a-old'
from VpnAccount va join Wallet w on w.AccountId = va.UserId and w.Description = 'T-renew-debit-a1'
    cross join Plans p
where va.Username = 'RenewA' and p.Name = 'RenewLatest-Fixture'

insert into Renewal (VpnAccountId, PlanId, SimultaneousUser, WalletDebit, Comment)
select va.Id, p.Id, 2, w.Id, 'T-renew-a-new'
from VpnAccount va join Wallet w on w.AccountId = va.UserId and w.Description = 'T-renew-debit-a2'
    cross join Plans p
where va.Username = 'RenewA' and p.Name = 'RenewLatest-Fixture'

insert into Renewal (VpnAccountId, PlanId, SimultaneousUser, WalletDebit, Comment)
select va.Id, p.Id, 1, w.Id, 'T-renew-b-only'
from VpnAccount va join Wallet w on w.AccountId = va.UserId and w.Description = 'T-renew-debit-b1'
    cross join Plans p
where va.Username = 'RenewB' and p.Name = 'RenewLatest-Fixture'
