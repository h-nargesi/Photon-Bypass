USE FastBypass;
GO

-- Docs/24 D1/D2: the sellable product. Replaces the old marketing-only
-- records: title/caption/description move here and the calculator code rides
-- on the plan. Axis pairs are base|cap; a NULL axis is UNDEFINED (the
-- calculator decides its value on each purchase, NULL output = unlimited),
-- never "unlimited by itself".
IF OBJECT_ID('Plans') IS NULL
CREATE TABLE Plans (
	Id				INT IDENTITY	NOT NULL	CONSTRAINT PK_Plans_Id PRIMARY KEY,
	-- internal identifier (admin-facing, unique)
	Name			NVARCHAR(50)	NOT NULL	CONSTRAINT UK_Plans_Name UNIQUE,
	-- marketing display (merged from the old website records, Docs/24 D1)
	Title			NVARCHAR(100)	NOT NULL,
	Caption			NVARCHAR(255)	NULL,
	Description		NVARCHAR(1000)	NULL,
	ShowOnWebsite	BIT				NOT NULL	CONSTRAINT DF_Plan_ShowOnWebsite DEFAULT 0,
	IsActive		BIT				NOT NULL	CONSTRAINT DF_Plan_IsActive DEFAULT 1,
	GroupId			INT				NOT NULL	CONSTRAINT FK_Plan_GroupId_UserGroup FOREIGN KEY REFERENCES UserGroup (Id),
	-- base|cap axis pairs; the member picks an integer multiple of the base
	-- within [base, cap] for each defined axis (Docs/24 §6)
	UsersBase		TINYINT			NULL,
	UsersCap		TINYINT			NULL,
	DaysBase		SMALLINT		NULL,
	DaysCap			SMALLINT		NULL,
	TrafficBase		INT				NULL,	-- gigabytes
	TrafficCap		INT				NULL,
	RateBase		TINYINT			NULL,	-- megabits per second
	RateCap			TINYINT			NULL,
	CalculatorCode	VARCHAR(511)	NOT NULL,
	Created			DATETIME		NOT NULL	CONSTRAINT DF_Plan_Created DEFAULT GETDATE(),
	CONSTRAINT CK_Plans_AxisPairs CHECK (
		((UsersBase IS NULL AND UsersCap IS NULL) OR (UsersBase IS NOT NULL AND UsersCap IS NOT NULL)) AND
		((DaysBase IS NULL AND DaysCap IS NULL) OR (DaysBase IS NOT NULL AND DaysCap IS NOT NULL)) AND
		((TrafficBase IS NULL AND TrafficCap IS NULL) OR (TrafficBase IS NOT NULL AND TrafficCap IS NOT NULL)) AND
		((RateBase IS NULL AND RateCap IS NULL) OR (RateBase IS NOT NULL AND RateCap IS NOT NULL))),
	CONSTRAINT CK_Plans_AtLeastOneAxis CHECK (
		UsersBase IS NOT NULL OR DaysBase IS NOT NULL OR TrafficBase IS NOT NULL OR RateBase IS NOT NULL),
	CONSTRAINT CK_Plans_AxisRanges CHECK (
		(UsersBase IS NULL OR UsersBase >= 1) AND (UsersCap IS NULL OR UsersCap >= UsersBase) AND
		(DaysBase IS NULL OR DaysBase >= 1) AND (DaysCap IS NULL OR DaysCap >= DaysBase) AND
		(TrafficBase IS NULL OR TrafficBase >= 1) AND (TrafficCap IS NULL OR TrafficCap >= TrafficBase) AND
		(RateBase IS NULL OR RateBase >= 1) AND (RateCap IS NULL OR RateCap >= RateBase))
)
GO
