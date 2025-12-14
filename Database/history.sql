IF OBJECT_ID('History') IS NULL
CREATE TABLE History (
	Id					INT IDENTITY	NOT NULL	CONSTRAINT PK_History_Id PRIMARY KEY,
	EventTime			DATETIME		NOT NULL	CONSTRAINT DF_Histiry_EventTime DEFAULT GETDATE(),
	Title				NVARCHAR(48)	NOT NULL,
	Color				VARCHAR(8)		NOT NULL,
	Value
	Unit
	Description
	Created

	CONSTRAINT FK_History_Owner_History_Id			FOREIGN KEY (Owner)	REFERENCES History (Id),
)
