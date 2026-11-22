insert into PanelUser (Username, Password, UserType, UserGroupId)
values ('Alpha', 'password', 4, 1)
    , ('Beta', 'password', 4, 1)

insert into Wallet (AccountId, Amount, Direction, Status, Description, InvoiceCode)
select a.Id, 1000, 1, 1, 'T-alpha-credit', 6100 from PanelUser a where a.Username = 'Alpha' union all
select a.Id, 500, 1, 4, 'T-alpha-verifying', 6102 from PanelUser a where a.Username = 'Alpha' union all
select a.Id, 300, -1, 1, 'T-beta-debit', null from PanelUser a where a.Username = 'Beta'

-- Docs/24 D9: plan invoice actions carry the plan token (grammar
-- t|p|[u|d|g|b|r|a]); these pending rows are read, never settled.
insert into Invoice (Code, AccountId, Kind, Title, TotalPrice, WalletDeduction, Payable, Action, Status)
select 6100, a.Id, 2, 'T2-plan-alpha', 1000, 0, 1000, 'Alphat|1p|2u|120d|50g', 0 from PanelUser a where a.Username = 'Alpha' union all
select 6101, a.Id, 1, 'T2-topup-alpha', 700, 0, 700, null, 0 from PanelUser a where a.Username = 'Alpha' union all
select 6102, a.Id, 1, 'T2-topup-verifying', 500, 0, 500, null, 4 from PanelUser a where a.Username = 'Alpha' union all
select 6103, a.Id, 2, 'T2-plan-beta', 400, 0, 700, 'Betat|1p|2u|120d|50g', 0 from PanelUser a where a.Username = 'Beta'
