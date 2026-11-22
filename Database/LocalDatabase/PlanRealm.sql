USE FastBypass;
GO

-- Docs/24 §3: many-to-many between the Plan catalog and realms. The purchase
-- flow intersects this set with the capacity filter of Docs/22 D4.
-- (structure-sort dependency: Plan)
IF OBJECT_ID('PlanRealm') IS NULL
CREATE TABLE PlanRealm (
	PlanId	INT	NOT NULL	CONSTRAINT FK_PlanRealm_PlanId_Plans FOREIGN KEY REFERENCES Plans (Id)
											ON DELETE CASCADE,
	RealmId	INT	NOT NULL	CONSTRAINT FK_PlanRealm_RealmId_Realm FOREIGN KEY REFERENCES Realm (Id),
	CONSTRAINT PK_PlanRealm PRIMARY KEY (PlanId, RealmId)
)
GO
