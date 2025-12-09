# Test Plan

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
- Controller
    [+] AccessService

## Level 2 - Application - Integration

Implement mock of all dependencies and test mid-level services.

- IAccountApplication:
	[+] GetUser
	[+] GetFullInfo
	[+] EditUser
	[+] ChangePassword
- IAuthApplication:
	[+] CheckUserPassword
	[+] ResetPassword
	[+] Register
- IConnectionApplication:
	[+] GetCurrentConnectionState
	[+] CloseConnection
- IPlanApplication:
	[-] GetPlanState
	[ ] GetPlanInfo
	[ ] Estimate
	[-] Renewal
- IVpnApplication:
	[ ] ChangeOvpnPassword
	[ ] SendCertEmail
	[-] TrafficData (Merge, FindFirstEmptyDate, ConvertToModel)
- AccountMonitoringService:
	[-] InactiveAbandonedUsers
	[-] NotifSendServices
- ServerManagementService:
	[-] GetAvailableRealm
	[-] CheckUserServerBalance
	[ ] GetDefaultCertificate

## Level 3 - Outsource - Unit Test

Run out-sources and test out-source interfaces.

- Infrastructure Repositories
- Mikrotik Radius

## Level 4 - Integration - Integration

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
	[ ] ChangeOvpnPassword
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
