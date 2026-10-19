insert into Account (Username, Password, UserType)
values ('ResetA', 'password', 4)
    , ('ResetB', 'password', 4)

-- ResetA: two live codes and one already-expired code
insert into ResetPassword (AccountId, HashCode, ExpireDate)
select Id, 'T-reset-a-live-1', DATEADD(day, 1, GETDATE()) from Account where Username = 'ResetA' union all
select Id, 'T-reset-a-live-2', DATEADD(hour, 12, GETDATE()) from Account where Username = 'ResetA' union all
select Id, 'T-reset-a-old', DATEADD(day, -5, GETDATE()) from Account where Username = 'ResetA'

-- ResetB: one live code that must stay untouched
insert into ResetPassword (AccountId, HashCode, ExpireDate)
select Id, 'T-reset-b-live', DATEADD(day, 1, GETDATE()) from Account where Username = 'ResetB'
