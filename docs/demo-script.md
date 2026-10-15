# Demo script (about 5 minutes)

What the interviewers asked for: walk them through the design, the implementation and how you'd test it. Share your screen.

## 1. The why (30 seconds): README, top table

"My first version was a Python Discord bot that pulled ranked data from Riot's API and rewrote a text file on every update. It had no history, a crash could corrupt the file, and it couldn't answer simple questions. I rebuilt the core in C# with ASP.NET Core, EF Core and SQL."

## 2. Design (1 minute): README diagrams

- Point at the ER diagram: one LpChange row per player per game, inserted and never rewritten.
- Point at the architecture diagram: business rules live in Core with no database dependency, so they're easy to test.
- Standings aren't stored. They're calculated from the rows, so they can't drift out of sync.

## 3. Working example (1.5 minutes): `dotnet run`, Swagger

1. `GET /api/standings`. Note that Sam shows up with 0 LP and no games.
2. `POST /api/matches` with a valid game, then `GET /api/standings` again to show the change.
3. `POST /api/matches` with a win and negative LP to show the `400` and its error list.
4. `GET /api/players/1/history` to show the running total, which the old version couldn't do.

## 3b. Riot integration (1 minute)

- Open "How the Riot sync works" in the README.
- The key point: **Riot doesn't report LP per game.** The sync snapshots each player's rank every few minutes and compares it with the last snapshot.
- Promotions are why `RankedLadder` exists: Gold I 90 → Plat IV 10 is +20, not −80. There's a test for it.
- If you have a valid key, run `POST /api/sync` live. If not, show `RiotSyncServiceTests` with the fake Riot client.

## 4. Code (1 minute)

- `MatchesController.Record`: validate, check players exist, one `SaveChanges` = one transaction.
- `StandingsQuery`: the database sums, and C# ranks.
- `docs/queries.sql`: the same thing in raw SQL with `LEFT JOIN`, `COALESCE` and `RANK()`, plus a stored procedure version.

## 5. Testing (1 minute): `dotnet test`

- Fast unit tests for the ranking and validation rules.
- Database tests on in-memory SQLite. Highlight **"player with no games still appears"**: an `INNER JOIN` would silently drop them.
- Next, I'd add API-level integration tests with `WebApplicationFactory`.

## Likely follow-up questions

- **Why not store a total on the Player row?** It could drift out of sync with the history. At this size, calculating it is cheap. At scale, I'd cache it or keep a summary table updated in the same transaction.
- **Two people record a match at once?** Each insert is its own transaction. The unique index blocks the same player twice in one match.
- **How would you make it real-time?** SignalR hub: after a match saves, broadcast the new standings.
- **How would you deploy it?** Migrations, then SQL Server on RDS, then a container on Fargate, then CI running `dotnet test`.
- **Why an interface for the Riot client?** So the sync logic can be tested without a key or network, and a fake client can simulate edge cases like lagging match history.
- **What about rate limits?** On a 429, the run stops and waits for Riot's `Retry-After`. Only one sync runs at a time, and a player whose rank hasn't changed costs one call per check.
- **What if the tracker was off for a day?** The total LP is still exact. The per-game split is estimated and flagged with `IsEstimated`.
- **What would you do differently?** Add migrations and integration tests from the start.
