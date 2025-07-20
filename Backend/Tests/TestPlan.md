# Test Plan

## Level 1 - Baisc Functions - Internal

Implement mock of used dependencies then test the function.

- Shared
	[+] EntityExtensions: GetColumn
	[+] EntityExtensions: GetTable
	[+] ExceptionHandlingMiddleware: Invoke
	[+] JsonConverter: UnixTimestampConverter
	[+] JsonConverter: StringNumberConverter
	[+] PersianHandlerTest: ToPersianString
	[+] PersianHandlerTest: MonthToDays
- Infrastructure
	[+] PriceCalculator: Compile

## Level 4 - Services - Integration

Implement mock of all dependencies and test high-level services.

- IAccountApplication:
	[ ] GetUser
	[ ] GetFullInfo
	[ ] EditUser
	[ ] ChangePassword
	[ ] GetHistory
- IAuthApplication: 
	[ ] CheckUserPassword
	[ ] ResetPassword
	[ ] Register
	[ ] CopyFromPermanentUser
- IBasicsApplication:
	[ ] GetPrices
- IConnectionApplication:
	[+] GetCurrentConnectionState
	[ ] CloseConnection
- IPlanApplication:
	[+] GetPlanState
	[ ] GetPlanInfo
	[ ] Estimate
	[+] Renewal
- IVpnApplication:
	[ ] ChangeOvpnPassword
	[ ] SendCertEmail
	[+] TrafficData (Merge, FindFirstEmptyDate, ConvertToModel)
- AccountMonitoringService:
	[+] DeactiveAbandonedUsers
	[+] NotifSendServices
- ServerManagementService:
	[+] GetAvalableRealm
	[+] CheckUserServerBalance
	[ ] GetDefaultCertificate

## Level 5 - OutSources

Run out-sources and test out-source interfaces.

- Queries
	[ ] ph_v_all_profiles
	[ ] ph_v_users_balance
- Radius Desk Web
- Radius Desk Databse
- VPN Nodes
