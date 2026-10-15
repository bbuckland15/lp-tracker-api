# Demo checklist: fresh start to live demo

## Part 1: Set up (do this 20–30 minutes before the call)

1. **Open Terminal.** Press Cmd+Space, type `Terminal`, press Enter.
2. **Go to the project:**
   ```bash
   cd ~/Projects/lp-tracker-api
   ```
3. **Reset the database** so the sample data starts clean:
   ```bash
   rm -f src/LpTracker.Api/lptracker.db
   ```
4. **Run the tests.** Expect `total: 54, failed: 0, succeeded: 54`.
   ```bash
   dotnet test
   ```
5. **Start the API.** Wait for `Now listening on: http://localhost:5080`, then leave this tab running.
   ```bash
   dotnet run --project src/LpTracker.Api
   ```
6. **Open a second Terminal tab** for later. Press Cmd+T, then run:
   ```bash
   cd ~/Projects/lp-tracker-api
   ```
7. **Open the browser** to http://localhost:5080/swagger. Run `GET /api/standings` once to check it works.
8. **Open the code in VS Code.** Use File > Open Folder and pick `Projects/lp-tracker-api`. Have these files open in tabs:
   - `README.md`
   - `src/LpTracker.Api/Controllers/MatchesController.cs`
   - `src/LpTracker.Api/Data/StandingsQuery.cs`
   - `src/LpTracker.Core/Ranked/RankedLadder.cs`
   - `docs/queries.sql`
   - `tests/LpTracker.Tests/StandingsQueryTests.cs`

   To see the README diagrams, press Cmd+Shift+V in `README.md`. VS Code needs the free **Markdown Preview Mermaid Support** extension to draw them. Check that they render tonight, not on the call.
9. **Tidy your screen.** Close other apps and tabs, turn on Do Not Disturb, make the VS Code and Terminal font bigger (Cmd + `+`), and test your webcam.

## Part 2: The demo (about 5 minutes)

### 1. Why I built it (30 seconds): README

Version 1 was a Python Discord bot the Nebraska Esports club used for its varsity and JV standings. A command pulled ranks from Riot's API, updated the standings, and rewrote a text file each time. It worked and already handled promotions and demotions, but it had no history and no way to answer questions. Version 2 keeps every game as its own database row.

Optional: show the v1 repo for about 20 seconds side by side, after checking it has no API key or bot token committed.

### 2. Design (1 minute): README diagrams

- One `LpChange` row per player per game, inserted and never rewritten.
- Business rules live in `LpTracker.Core`, with no database dependency.
- Standings are calculated from the rows, never stored.

### 3. Working example (1.5 minutes): Swagger

Sample data at the start:

| Position | Player | LP |
| --- | --- | --- |
| 1 | Alex | 39 |
| 2 | Ben | 27 |
| 3 | Jordan | 25 |
| 4 | Sam | 0 (no games) |

1. **`GET /api/standings`:** point out that Sam appears even with no games.
2. **`POST /api/matches`:** Sam wins 25 LP.
   ```json
   { "results": [ { "playerId": 4, "won": true, "lpDelta": 25 } ] }
   ```
3. **`GET /api/standings` again:** Sam and Jordan now **tie at 25 and share 3rd place**, the same as SQL's `RANK()`.
4. **`POST /api/matches`, invalid:** Ben "won" but lost LP. Expect a **400** with an error list.
   ```json
   { "results": [ { "playerId": 1, "won": true, "lpDelta": -20 } ] }
   ```
5. **`GET /api/players/1/history`:** Ben's games with a running total (21 → 3 → 27). The old version had no history at all.

### 4. Code (1 minute): VS Code

- `MatchesController.Record`: validate → check players exist → one `SaveChanges`, which is one transaction.
- `StandingsQuery`: the database sums, and C# ranks.
- `RankedLadder`: Gold I 90 → Plat IV 10 is +20, not −80.
- `queries.sql`: the same logic in raw SQL (`LEFT JOIN`, `COALESCE`, `RANK()`).

### 5. Testing (1 minute): second Terminal tab

```bash
dotnet test --no-build
```

- 54 tests: fast unit tests, plus database tests on in-memory SQLite.
- Point to "player with no games still appears" and the Riot sync tests that use a fake Riot client.

## Part 3: After the call

In the first tab, press **Ctrl+C** to stop the API.

## If something goes wrong

| Problem | Fix |
| --- | --- |
| `address already in use` | An old run is still going. Close other Terminal windows, or restart Terminal. |
| `command not found: dotnet` | Quit Terminal (Cmd+Q) and reopen it. |
| Weird data or "no such column" | Stop the API, run `rm -f src/LpTracker.Api/lptracker.db`, and start it again. |
| Swagger page won't load | Check that the first tab still shows `Now listening`. Use `http`, not `https`. |
| Anything else live | Don't debug on the call. Switch to your backup screenshots and keep talking through the design. |
