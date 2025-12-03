# Test Plan

- **Integration**: testing all functions integratedly.
- **Outsource**: testing functions that require external resources.
- **Basic**: testing functions that are independent.

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
- Infrastructure
	[+] PriceCalculator: Compile

## Level 2 - Outsource - Unit Test

Implement mock of all dependencies and test mid-level services.

## Level 3 - Integration - Integration

Implement mock of all outsources and test high-level services.
