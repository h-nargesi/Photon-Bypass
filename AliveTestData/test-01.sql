insert into Realm (IsActive, Name) values 
(1, 'VM04'),
(1, 'VM06'),
(1, 'VM08')
;

declare @VM04 int = (select top 1 Id from Realm where Name = 'VM04');
declare @VM06 int = (select top 1 Id from Realm where Name = 'VM04');
declare @VM08 int = (select top 1 Id from Realm where Name = 'VM04');

insert into Server (RealmId, IpAddress, Name, DomainName, BandWidth, OsType, Features) values
(@VM04, '0.0.0.0', 'VM04', 'vm04.photon.ir', 200, 1, 0x1402),
(@VM06, '0.0.0.0', 'VM06', 'vm06.photon.ir', 200, 2, 0x0804),
(@VM08, '0.0.0.0', 'VM08', 'vm08.photon.ir', 200, 3, 0x0804)
;

insert into PanelUser (Username, Password, Name, UserType) values
('noushinn', '000', 'noushin', 0)
;

declare @noushinn int = (select top 1 Id from PanelUser where Username = 'noushinn');

insert into VpnAccount (UserId, RealmId, Username, VpnPassword) values
(@noushinn, @VM04, 'noushin', 'noushin')
;
