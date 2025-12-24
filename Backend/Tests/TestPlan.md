# Test Plan

- **Scenario**: testing all functions integratedly with complex scenario.
- **Integration**: testing all functions integratedly.
- **Outsource**: testing functions that require external resources.
- **Application**: mocking outsource and test all function.
- **Basic**: testing basic functions that are independent.

## Level 1 - Basic Functions - Unit Test

Implement mock of used dependencies then test the function.

- Shared
	[+] EntityExtensions: GetColumn
	[+] EntityExtensions: GetTable
	[+] ExceptionHandlingMiddleware: Invoke
	[+] JsonConverter: UnixTimestampConverter
	[+] JsonConverter: StringNumberConverter
	[+] PersianHandlerTest: ToPersianString
	[+] PersianHandlerTest: MonthToDays
- Business
	[+] AccountBusiness
	[+] PlanStateBusiness
	[+] RenewalBusiness
- Infrastructure
	[+] PriceCalculator: Compile
	[ ] EventService: Register, Unregister, Call
- Controller
	[+] AccessService

## Level 2 - Application

Implement mock of all dependencies and test mid-level services.

- IAccountApplication:
	[+] GetUser
	[+] GetFullInfo
	[+] EditUser
	[+] ChangePassword
- IAuthApplication:
	[+] CheckUserPassword
	[+] ForgetPassword
	[ ] ResetPassword
	[+] Register
- IConnectionApplication:
	[+] GetCurrentConnectionState
	[+] CloseConnection
- IPlanApplication:
	[+] GetPlanState
	[+] GetPlanInfo
	[+] Renewal
- IVpnApplication:
	[+] ChangeVpnPassword
	[+] SendCertEmail
	[+] TrafficData
- AccountMonitoringService:
	[+] InactiveAbandonedUsers
	[+] NotifSendServices
- ServerManagementService:
	[+] GetAvailableRealm
	[-] CheckUserServerBalance
	[+] GetDefaultCertificate
	[+] UpdateTrafficData (Merge, FindFirstEmptyDate, ConvertToModel)
- Radius
	[+] GetTrafficData
	[+] SyncUserAndActive

## Level 3 - Outsource - Unit Test

Run out-sources and test out-source interfaces.

- Infrastructure Repositories
	[=] Transaction
	[+] Account
	[ ] ResetPass
	[+] History
	[ ] Price
	[ ] Realm
	[ ] Renewal
	[ ] Server
	[ ] TrafficData
	[ ] PlanState

- Mikrotik Radius
	- AccountRadiusSyncUserManagerService
		[ ] RemoveUsers
		[ ] DeactivateUserExcept
		[ ] DeactivateUser
		[+] SyncUserAndActive
		[ ] ChangeVpnPassword
	- SessionRadiusSyncUserManagerService
		[ ] GetActiveConnections
		[ ] CloseConnectionBySessionId
		[ ] CloseConnectionByUsername
		[ ] GetTrafficData

## Level 4 - Integration

Implement mock of all outsources and test high-level services.

Controllers:

- AccountController:
	[ ] GetUser
	[ ] GetFullInfo
	[ ] EditUser
	[ ] ChangePassword
	[ ] GetHistory
- AuthController:
	[ ] Login
	[ ] ForgetPassword
	[ ] ResetPassword
	[ ] Register
- BasicsController:
	[ ] GetPrices
- ConnectionController:
	[ ] GetCurrentConnectionState
	[ ] CloseConnection
- PlanController:
	[ ] GetPlanState
	[ ] GetPlanInfo
	[ ] Estimate
	[ ] Renewal
- VpnController:
	[ ] ChangeVpnPassword
	[ ] SendCertEmail
	[ ] TrafficData

Services:

- AccountMonitoringService:
	[ ] InactiveAbandonedUsers
	[ ] NotifSendServices
- ServerManagementService:
	[ ] GetAvailableRealm
	[ ] CheckUserServerBalance
	[ ] GetDefaultCertificate

## Level 5 - Scenario

[=] RegisterAndBuy