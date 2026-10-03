insert into Account (Username, Password)
values ('Alpha', 'password')
    , ('Beta', 'password')

insert into Wallet (AccountId, Amount, Direction, Status, Description, InvoiceCode)
select a.Id, 100, 1, 0, 'T4-alpha-1', 5001 from Account a where a.Username = 'Alpha' union all
select a.Id, 200, 1, 0, 'T4-alpha-2', 5001 from Account a where a.Username = 'Alpha' union all
select a.Id, 300, 1, 0, 'T4-beta', 5001 from Account a where a.Username = 'Beta'
