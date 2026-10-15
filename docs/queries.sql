-- LP Tracker: the SQL behind the API, written out by hand for discussion.
-- The app uses Entity Framework Core, which generates similar SQL from the LINQ in StandingsQuery.cs.
-- Syntax below is SQL Server (T-SQL) to match MyRacePass's stack; SQLite is nearly identical.

---------------------------------------------------------------------------
-- 1. Tables
---------------------------------------------------------------------------
CREATE TABLE Players (
    Id        INT IDENTITY PRIMARY KEY,
    Name      NVARCHAR(50) NOT NULL UNIQUE,
    CreatedAt DATETIME2    NOT NULL
);

CREATE TABLE Matches (
    Id       INT IDENTITY PRIMARY KEY,
    PlayedAt DATETIME2 NOT NULL
);

-- One row per player per game. Inserted, never rewritten.
CREATE TABLE LpChanges (
    Id       INT IDENTITY PRIMARY KEY,
    PlayerId INT NOT NULL REFERENCES Players(Id),                    -- can't delete a player with history
    MatchId  INT NOT NULL REFERENCES Matches(Id) ON DELETE CASCADE,  -- deleting a match removes its rows
    Won      BIT NOT NULL,
    LpDelta  INT NOT NULL,
    CONSTRAINT UQ_LpChanges_Match_Player UNIQUE (MatchId, PlayerId) -- one result per player per match
);

-- Standings and history both filter / group by PlayerId.
CREATE INDEX IX_LpChanges_PlayerId ON LpChanges (PlayerId);

---------------------------------------------------------------------------
-- 2. Standings
---------------------------------------------------------------------------
-- LEFT JOIN so players with no games still appear (an INNER JOIN would drop them).
-- COALESCE turns their NULL sum into 0. RANK() gives ties the same position: 1, 2, 2, 4.
SELECT
    RANK() OVER (ORDER BY COALESCE(SUM(c.LpDelta), 0) DESC)    AS Position,
    p.Id,
    p.Name,
    COALESCE(SUM(c.LpDelta), 0)                                AS TotalLp,
    COUNT(CASE WHEN c.Won = 1 THEN 1 END)                      AS Wins,
    COUNT(CASE WHEN c.Won = 0 THEN 1 END)                      AS Losses
FROM Players p
LEFT JOIN LpChanges c ON c.PlayerId = p.Id
GROUP BY p.Id, p.Name
ORDER BY Position, p.Name;

---------------------------------------------------------------------------
-- 3. One player's LP history with a running total
---------------------------------------------------------------------------
DECLARE @PlayerId INT = 1;

SELECT
    m.Id AS MatchId,
    m.PlayedAt,
    c.Won,
    c.LpDelta,
    SUM(c.LpDelta) OVER (ORDER BY m.PlayedAt, m.Id ROWS UNBOUNDED PRECEDING) AS RunningTotal
FROM LpChanges c
JOIN Matches m ON m.Id = c.MatchId
WHERE c.PlayerId = @PlayerId     -- uses IX_LpChanges_PlayerId
ORDER BY m.PlayedAt, m.Id;

---------------------------------------------------------------------------
-- 4. Same standings query as a stored procedure (MyRacePass uses these)
---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.GetStandings
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        RANK() OVER (ORDER BY COALESCE(SUM(c.LpDelta), 0) DESC) AS Position,
        p.Id,
        p.Name,
        COALESCE(SUM(c.LpDelta), 0) AS TotalLp
    FROM Players p
    LEFT JOIN LpChanges c ON c.PlayerId = p.Id
    GROUP BY p.Id, p.Name
    ORDER BY Position, p.Name;
END;

---------------------------------------------------------------------------
-- 5. Bonus questions the old text file couldn't answer
---------------------------------------------------------------------------
-- LP gained in the last 7 days, per player
-- The date filter lives inside the subquery so players with no recent games still show 0.
SELECT p.Name, COALESCE(SUM(recent.LpDelta), 0) AS LpThisWeek
FROM Players p
LEFT JOIN (
    SELECT c.PlayerId, c.LpDelta
    FROM LpChanges c
    JOIN Matches m ON m.Id = c.MatchId
    WHERE m.PlayedAt >= DATEADD(DAY, -7, SYSUTCDATETIME())
) recent ON recent.PlayerId = p.Id
GROUP BY p.Name
ORDER BY LpThisWeek DESC;
