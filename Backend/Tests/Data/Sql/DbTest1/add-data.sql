insert into Account (Username, Password)
values ('User1', 'password')
    , ('User2', 'password')
    , ('User3', 'password')

insert into Account (Username, Password, Owner)
select 'User11', 'password', Id from Account where Username = 'User1' union
select 'User12', 'password', Id from Account where Username = 'User1' union
select 'User13', 'password', Id from Account where Username = 'User1' union
select 'User14', 'password', Id from Account where Username = 'User1';

insert into Account (Username, Password, Owner)
select 'User21', 'password', Id from Account where Username = 'User2' union
select 'User22', 'password', Id from Account where Username = 'User2';
