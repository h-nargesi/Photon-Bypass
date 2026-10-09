insert into Account (Username, Password)
values ('Alpha', 'password')
    , ('Beta', 'password')

insert into Wallet (AccountId, Amount, Direction, Status, Description, InvoiceCode)
select a.Id, 1000, 1, 1, 'T-alpha-credit', 6100 from Account a where a.Username = 'Alpha' union all
select a.Id, 500, 1, 4, 'T-alpha-verifying', 6102 from Account a where a.Username = 'Alpha' union all
select a.Id, 300, -1, 1, 'T-beta-debit', null from Account a where a.Username = 'Beta'

insert into Invoice (Code, AccountId, Kind, Title, TotalPrice, WalletDeduction, Payable, Action, Status)
select 6100, a.Id, 2, 'T2-plan-alpha', 1000, 0, 1000, 'Alphat|2u|120d|50g', 0 from Account a where a.Username = 'Alpha' union all
select 6101, a.Id, 1, 'T2-topup-alpha', 700, 0, 700, null, 0 from Account a where a.Username = 'Alpha' union all
select 6102, a.Id, 1, 'T2-topup-verifying', 500, 0, 500, null, 4 from Account a where a.Username = 'Alpha' union all
select 6103, a.Id, 2, 'T2-plan-beta', 400, 0, 700, 'Betat|2u|120d|50g', 0 from Account a where a.Username = 'Beta'
