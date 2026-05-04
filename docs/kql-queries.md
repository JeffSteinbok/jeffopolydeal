# Jeffopoly Deal — KQL Queries for Application Insights

Paste these into **Application Insights → Logs** (or a Workbook) to analyze game telemetry.

---

## Games Overview (last 7 days)

```kql
traces
| where timestamp > ago(7d)
| where message startswith "GameStarted"
| extend GameId = tostring(customDimensions.GameId),
         PlayerCount = toint(customDimensions.PlayerCount),
         BotCount = toint(customDimensions.BotCount)
| summarize Games = count(),
            AvgPlayers = avg(PlayerCount),
            AvgBots = avg(BotCount)
```

## Games Per Day

```kql
traces
| where timestamp > ago(30d)
| where message startswith "GameStarted"
| summarize Games = count() by bin(timestamp, 1d)
| order by timestamp asc
| render timechart
```

## Average Game Duration & Turns

```kql
traces
| where timestamp > ago(7d)
| where message startswith "GameEnded"
| extend Duration = todouble(customDimensions.DurationSeconds),
         Turns = toint(customDimensions.TurnCount)
| summarize AvgDuration = avg(Duration),
            MedianDuration = percentile(Duration, 50),
            AvgTurns = avg(Turns),
            MedianTurns = percentile(Turns, 50),
            Games = count()
```

## Win Rate: Humans vs Bots

```kql
traces
| where timestamp > ago(30d)
| where message startswith "GameEnded"
| extend WinnerIsBot = tobool(customDimensions.WinnerIsBot)
| summarize Wins = count() by WinnerIsBot
| extend WinnerType = iff(WinnerIsBot, "Bot", "Human")
| project WinnerType, Wins
| render piechart
```

## Most Played Card Types

```kql
traces
| where timestamp > ago(7d)
| where message startswith "CardPlayed"
| extend CardType = tostring(customDimensions.CardType),
         PlayedAsMoney = tobool(customDimensions.PlayedAsMoney)
| summarize Count = count() by CardType, PlayedAsMoney
| order by Count desc
```

## Most Used Action Cards

```kql
traces
| where timestamp > ago(7d)
| where message startswith "CardPlayed"
| where customDimensions.CardType == "Action"
| extend ActionType = tostring(customDimensions.ActionType),
         PlayedAsMoney = tobool(customDimensions.PlayedAsMoney)
| summarize Count = count() by ActionType, PlayedAsMoney
| order by Count desc
```

## Just Say No Usage Rate

```kql
traces
| where timestamp > ago(30d)
| where message startswith "ActionResponse"
| extend ResponseType = tostring(customDimensions.ResponseType),
         PlayerIsBot = tobool(customDimensions.PlayerIsBot)
| summarize Count = count() by ResponseType, PlayerIsBot
| order by Count desc
```

## Rent Collection by Color

```kql
traces
| where timestamp > ago(7d)
| where message startswith "CardPlayed"
| where customDimensions.CardType == "Rent"
| where tobool(customDimensions.PlayedAsMoney) == false
| extend RentColor = tostring(customDimensions.RentColor),
         RentAmount = toint(customDimensions.RentAmount)
| summarize TimesCharged = count(),
            TotalRent = sum(RentAmount),
            AvgRent = avg(RentAmount) by RentColor
| order by TimesCharged desc
```

## Cards Banked as Money (instead of played)

```kql
traces
| where timestamp > ago(7d)
| where message startswith "CardPlayed"
| where tobool(customDimensions.PlayedAsMoney) == true
| extend CardType = tostring(customDimensions.CardType),
         CardName = tostring(customDimensions.CardName)
| summarize Count = count() by CardType, CardName
| order by Count desc
```

## Bot vs Human Card Play Patterns

```kql
traces
| where timestamp > ago(7d)
| where message startswith "CardPlayed"
| extend PlayerIsBot = tobool(customDimensions.PlayerIsBot),
         CardType = tostring(customDimensions.CardType)
| summarize Count = count() by PlayerType = iff(PlayerIsBot, "Bot", "Human"), CardType
| order by PlayerType, Count desc
```

## Game Duration Distribution (histogram)

```kql
traces
| where timestamp > ago(30d)
| where message startswith "GameEnded"
| extend Duration = todouble(customDimensions.DurationSeconds)
| summarize Count = count() by DurationBucket = bin(Duration, 60)
| order by DurationBucket asc
| render columnchart
```

## Player Roster for a Specific Game

```kql
traces
| where message startswith "GameStarted"
| extend GameId = tostring(customDimensions.GameId),
         Players = tostring(customDimensions.Players)
| where GameId == "<GAME_ID_HERE>"
| project timestamp, GameId, Players
```

## Full Event Timeline for a Game

```kql
traces
| where customDimensions.GameId == "<GAME_ID_HERE>"
| project timestamp,
          Event = extract(@"^(\w+)", 1, message),
          Details = customDimensions
| order by timestamp asc
```
