# Uma Companion — Live-State WebSocket Contract (v1)

The companion app runs a WebSocket **server** on `ws://localhost:8974`. Mike's game hook-in
(built separately, UmaExtractor-style) connects as a **client** and pushes JSON frames as game
state changes. The app never touches the game process.

Every frame:

```jsonc
{ "v": 1, "type": "<frame-type>", "payload": <object|array> }
```

Unknown fields are ignored; unknown `type` values are logged and dropped. `v` lets the contract
evolve without breaking older hooks.

## Frame types

### `career_turn` — pushed each training turn
```jsonc
{ "v": 1, "type": "career_turn", "payload": {
    "turn": 24,
    "stats": { "speed": 412, "stamina": 301, "power": 288, "guts": 190, "wiz": 240 },
    "energy": 62,              // 0-100, drives failure rate
    "mood": 4,                 // 1 (Awful) .. 5 (Great)
    "skill_points": 145,
    "fans": 12040,
    "goal_race_id": 1001,      // current objective race, or null
    "training_options": [      // exactly the 5 facilities as shown on the training screen
      { "facility": "speed", "level": 3,
        "gains": { "speed": 24, "power": 8, "skill_pts": 4 },
        "fail_pct": 5,
        "supports": [ { "support_card_id": 30028, "bond": 74, "hint": true } ] }
    ] } }
```

### `roster` — pushed on the Veteran List screen (or File → Import of a data.json)
Payload is **verbatim UmaExtractor / rockisch-umadump `data.json`** — an array of trained
characters. Do not reshape it in the hook; the app owns the mapping.
```jsonc
{ "v": 1, "type": "roster", "payload": [
    { "card_id": 100101, "chara_id": 1001,
      "speed": 1200, "stamina": 900, "power": 800, "guts": 400, "wiz": 650,
      "skill_array": [ ... ], "factor_id_array": [ ... ],
      "proper_distance_1": 7, "proper_ground_1": 7, "rank_score": 12345 } ] }
```

### `race_results` — pushed when a race is won/finished (feeds the trophy tracker)
```jsonc
{ "v": 1, "type": "race_results", "payload": [
    { "chara_id": 1003, "race_id": 1001, "won": true } ] }
```

## Development without the game
`tools/FakeHookSender` (to be added) connects to the server and replays fixture frames from
`tests/fixtures/*.json`, so the entire app and all engines are developable and testable with the
game closed.
