insert into Account (Username, Password, UserType)
values ('RenewA', 'password', 4)
    , ('RenewB', 'password', 4)

-- one debit row per renewal (Renewal.WalletDebit is a NOT NULL foreign key)
insert into Wallet (AccountId, Amount, Direction, Status, Description)
select Id, 500, -1, 1, 'T-renew-debit-a1' from Account where Username = 'RenewA' union all
select Id, 500, -1, 1, 'T-renew-debit-a2' from Account where Username = 'RenewA' union all
select Id, 500, -1, 1, 'T-renew-debit-b1' from Account where Username = 'RenewB'

-- separate statements so the identity order (old < new) is deterministic
insert into Renewal (AccountId, SimultaneousUser, WalletDebit, Comment)
select a.Id, 1, w.Id, 'T-renew-a-old'
from Account a join Wallet w on w.AccountId = a.Id and w.Description = 'T-renew-debit-a1'
where a.Username = 'RenewA'

insert into Renewal (AccountId, SimultaneousUser, WalletDebit, Comment)
select a.Id, 2, w.Id, 'T-renew-a-new'
from Account a join Wallet w on w.AccountId = a.Id and w.Description = 'T-renew-debit-a2'
where a.Username = 'RenewA'

insert into Renewal (AccountId, SimultaneousUser, WalletDebit, Comment)
select a.Id, 1, w.Id, 'T-renew-b-only'
from Account a join Wallet w on w.AccountId = a.Id and w.Description = 'T-renew-debit-b1'
where a.Username = 'RenewB'
