-- T3 ops audit: every NAS that serves OpenVPN must carry the ServerFeature.Ovpn bit (0x400).
-- Run against production (FastBypass) before enabling the OVPN buttons; the reporting SELECT is
-- read-only and safe to re-run. The UPDATE at the bottom is OPT-IN: uncomment per row and run
-- only after the operator confirms the server really serves OVPN certificates.
USE FastBypass;
GO

-- Report: active NAS servers that look like Mikrotik OVPN candidates.
-- healthy  = has the Ovpn bit (0x400) set in Features.
-- suspect  = Mikrotik NAS without the Ovpn bit (the OVPN download/email button will stay hidden).
-- The Features column is printed both raw (decimal) and as hex for verification.
SELECT s.Id
     , s.RealmId
     , s.Name
     , s.IpAddress
     , s.OsType
     , s.Features
     , CAST(s.Features AS BINARY(4)) AS FeaturesHex
     , CASE WHEN (s.Features & 0x400) <> 0 THEN 'healthy' ELSE 'suspect' END AS OvpnBitState
     , CASE
           WHEN (s.Features & 0x0002) <> 0 THEN 'UserManager'
           WHEN (s.Features & 0x0001) <> 0 THEN 'RadiusDesk'
           WHEN (s.Features & 0x0004) <> 0 THEN 'Xui'
           ELSE 'none'
       END AS RadiusKind
FROM Server s
WHERE s.IsActive = 1
  AND (s.Features & 0xFF00) <> 0        -- any NAS capability bit
ORDER BY OvpnBitState, s.RealmId, s.Id;
GO

-- Optional manual fix (per row, operator confirmation required).
-- Example: set the Ovpn bit for server 3 without touching the other feature bits:
-- UPDATE Server SET Features = Features | 0x400 WHERE Id = 3;
