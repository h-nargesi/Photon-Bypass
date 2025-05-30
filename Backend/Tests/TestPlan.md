# Test Plan

## Level 1 - Baisc Functions - Internal

- Shared
	[+] EntityExtensions: GetColumn, GetTable
	[+] ExceptionHandlingMiddleware: Invoke
	[+] JsonConverter: UnixTimestampConverter, StringNumberConverter
	[+] PersianHandlerTest: ToPersianString, MonthToDays
- Infrastructure
	[+] PriceCalculator: Compile
- Application
	[+] ConnectionApplication: GetCurrentConnectionState 'Merge Connections'
	[+] PlanApplication: GetPlanState 'Calculate Ramining', Renewal
	[+] VpnApplication: TrafficData (Merge, FindFirstEmptyDate, ConvertToModel)
	[+] AccountMonitoringService: DeactiveAbandonedUsers, NotifSendServices
	[ ] ServerManagementService: GetAvalableRealm, GetDefaultCertificate, CheckUserServerBalance

## Level 2 - Queries

- ph_v_all_profiles
- ph_v_users_balance

## Level 4 - Services - Integration

- IAccountApplication: GetUser, GetFullInfo, EditUser, ChangePassword, GetHistory
- IAuthApplication: CheckUserPassword, ResetPassword, Register, CopyFromPermanentUser
- IBasicsApplication: GetPrices
- IConnectionApplication: GetCurrentConnectionState, CloseConnection
- IPlanApplication: GetPlanState, GetPlanInfo, Estimate, Renewal
- IVpnApplication: ChangeOvpnPassword, SendCertEmail, TrafficData
- AccountMonitoringService: DeactiveAbandonedUsers, NotifSendServices
- ServerManagementService: GetAvalableRealm, GetDefaultCertificate, CheckUserServerBalance

## Level 5 - Real Word - Integration

- IAccountApplication: GetUser, GetFullInfo, EditUser, ChangePassword, GetHistory
- IAuthApplication: CheckUserPassword, ResetPassword, Register, CopyFromPermanentUser
- IBasicsApplication: GetPrices
- IPlanApplication: GetPlanState, GetPlanInfo, Estimate, Renewal
- IVpnApplication: ChangeOvpnPassword, SendCertEmail, TrafficData
- AccountMonitoringService: DeactiveAbandonedUsers, NotifSendServices
- ServerManagementService: GetAvalableRealm, GetDefaultCertificate, CheckUserServerBalance
