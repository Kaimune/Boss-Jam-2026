# Boss Health Bar UI — Design

**Date:** 2026-05-17
**Branch:** `feat/boss-game-movement`
**Status:** Approved, ready for implementation plan

## Goal

Add a top-center, boss-fight-style health bar UI that reflects the boss player's
HP. The boss already has `hp = 10` and a `TakeDamage` method
(`Assets/Scripts/Player/BossController.cs`) that currently only logs; this design
wires those state changes to an on-screen segmented bar with a damage flash.

## Visual summary

```
┌──────────────────────────────────────────┐
│              BOSS                        │
│         ██ ██ ██ ██ ██ ██ ██ ░░ ░░ ░░    │
│                                          │
│                  [boss]                  │
└──────────────────────────────────────────┘
```

- Top-center placement, screen-space overlay.
- One segment per HP point (segmented, not continuous).
- "BOSS" label above the bar; no numeric HP text.
- Instant drop on damage with a brief white flash on each segment that just
  emptied.

## Architecture

Two components joined by a C# event.

### `BossController` changes

- Replace the single private `hp` field with:
  - `[SerializeField] int maxHp = 10` (Inspector-tunable max).
  - `int currentHp` (runtime, initialized to `maxHp` in `Awake`).
- Public read-only surface:
  - `public int MaxHp => maxHp;`
  - `public int CurrentHp => currentHp;`
- Public event: `public event Action<int, int> HpChanged;` — args are
  `(currentHp, maxHp)`.
- Fire `HpChanged` once on `Start` (initial state, after subscribers' `OnEnable`
  has run) and once inside `TakeDamage` after the new value is computed and
  clamped to `[0, maxHp]`.
- Keep the existing damage log and the "no death handling yet" log.

### `BossHealthBarUI` (new)

- Location: `Assets/Scripts/UI/BossHealthBarUI.cs` (new `UI` folder).
- MonoBehaviour on the bar root inside a screen-space `Canvas`.
- Inspector fields:
  - `BossController boss` — serialized reference.
  - `HpSegment segmentPrefab` — segment prefab to instantiate.
  - `Transform segmentRow` — parent transform with a `HorizontalLayoutGroup`.
- On `Start`: instantiate `boss.MaxHp` segments under `segmentRow`, cache them
  in `List<HpSegment>`, set initial filled state. Segments are instantiated
  once; `OnEnable`/`OnDisable` do not rebuild them.
- `OnEnable`: subscribe to `boss.HpChanged`. If segments already exist
  (i.e. not first enable), also re-sync filled state from `boss.CurrentHp`
  without flashing.
- `OnDisable`: unsubscribe from `boss.HpChanged`.
- `OnHpChanged(current, max)`:
  1. For each segment, `SetFilled(i < current)`.
  2. For each segment index `i` in `[current, prevCurrent)`, call `Flash()`
     — these are the segments that just went from filled to empty.
  3. Update `prevCurrent = current`.
- `prevCurrent` is initialized to `boss.CurrentHp` whenever the bar (re)syncs
  without an event — on initial `Start` and on `OnEnable` re-sync — so the
  first observed HP value never triggers a flash.
- If `boss` is null in `Awake`, log a warning and disable the component
  (no per-frame null guards needed elsewhere).

### `HpSegment` (new)

- Location: `Assets/Scripts/UI/HpSegment.cs`.
- Lightweight component on the segment prefab.
- Inspector fields (all visual tuning lives here, not in `BossHealthBarUI`):
  - `Image emptyOverlay` — toggled active when segment is "lost".
  - `Image flashOverlay` — white overlay whose alpha is animated.
  - `float flashDuration = 0.15f`.
- Methods:
  - `void SetFilled(bool filled)` — `emptyOverlay.gameObject.SetActive(!filled)`.
  - `void Flash()` — start coroutine that ramps `flashOverlay.color.a` from
    1 → 0 over `flashDuration`. Stops any existing flash coroutine first
    so rapid hits don't stack.

## Scene structure

```
Canvas (Screen Space - Overlay, scale-with-screen-size)
└── BossHealthBar           (RectTransform anchored top-center, BossHealthBarUI)
    ├── Label "BOSS"        (TextMeshProUGUI)
    └── SegmentRow          (HorizontalLayoutGroup, fixed spacing)
        └── (segments instantiated at runtime from HpSegment prefab)
```

Prefab: `Assets/Prefabs/UI/HpSegment.prefab`
- Root `Image` = filled-state color (e.g., red).
- Child `Image` "EmptyOverlay" = dark overlay, toggled on when lost.
- Child `Image` "FlashOverlay" = white overlay, alpha 0 at rest, animated by
  `HpSegment.Flash()`.

## Data flow

```
Boss takes damage (Fireball collision, attack hit, etc.)
        │
        ▼
BossController.TakeDamage(amount, source)
   • currentHp = max(0, currentHp - amount)
   • Debug.Log(...)
   • HpChanged?.Invoke(currentHp, maxHp)
        │
        ▼
BossHealthBarUI.OnHpChanged(current, max)
   • For i in 0..max-1: segments[i].SetFilled(i < current)
   • For each segment in [current, prevCurrent): segments[i].Flash()
   • prevCurrent = current
```

Initial state: `BossController.Start` fires `HpChanged(currentHp, maxHp)` once
so the UI shows the starting bar without polling.

## Edge cases

- **Multi-HP damage:** every segment that emptied this call flashes.
- **MaxHp changed in Inspector at runtime:** bar is built once on `Start` from
  `MaxHp`; runtime resizing not supported. Acceptable for design-time tuning.
- **Boss reference missing on UI:** log a warning in `Awake`, disable component.
- **HP below 0:** clamped to 0 in `TakeDamage`. No death handling yet (matches
  the existing "Boss would die here — no death handling yet." log).
- **Disable/enable cycles:** UI unsubscribes in `OnDisable`, re-subscribes in
  `OnEnable`, and re-syncs to current HP on `OnEnable` so a re-enabled UI
  doesn't show stale state.
- **Rapid hits:** `Flash()` cancels any running coroutine before starting,
  so flashes don't visually stack on the same segment.

## Future flexibility — what stays open

We keep these seams clean so a future visual pass is art + prefab work, not
logic changes. We are **not** building any of these now; we're just not
preventing them.

- **All visuals on prefabs.** Colors, sprites, segment size, spacing, flash
  duration, flash color — every one is a SerializeField on `HpSegment` or
  `HorizontalLayoutGroup` config on `SegmentRow`. `BossHealthBarUI` has zero
  magic numbers.
- **`HpSegment` is the visual swap point.** It exposes only `SetFilled(bool)`
  and `Flash()`. A future prefab (animated sprite, particle burst, shader
  effect) can re-implement those; `BossHealthBarUI` is untouched.
- **`BossController` is shape-agnostic.** It only emits `(current, max)`. A
  future switch to continuous fill, lag-bar overlay, or layered shielded HP is
  a UI-only change.
- **Bar root is a single Canvas child.** Future chrome (border, name plate,
  portrait, glow) gets added as siblings of `BossHealthBar` or wrapped around
  it. The prefab hierarchy itself is the skin.

Explicitly out of scope (would be premature):

- No `IBossHealthBarUI` interface / strategy pattern — one consumer.
- No animation framework integration; coroutines are enough.
- No theme ScriptableObject; the prefab is the theme.

## Testing

No automated tests planned — this is a Unity UI surface and the project has
no test harness yet. Manual validation:

1. Walk boss into a Fireball → segment empties, flashes white briefly.
2. Multi-hit (e.g., raise Fireball damage to 2) → two adjacent segments flash
   together.
3. Damage all 10 → bar empties to zero, no errors, existing "would die here"
   log still appears.
4. Edit `MaxHp` to 5 in Inspector before Play → bar has 5 segments.
5. Disable and re-enable the `BossHealthBarUI` GameObject mid-fight → bar
   re-syncs to current HP, no stale state.

## Files touched

- **Modified:** `Assets/Scripts/Player/BossController.cs` — `hp` → `maxHp` +
  `currentHp`, public `MaxHp`/`CurrentHp` properties, `HpChanged` event,
  `Start` initial fire, clamp in `TakeDamage`.
- **New:** `Assets/Scripts/UI/BossHealthBarUI.cs`.
- **New:** `Assets/Scripts/UI/HpSegment.cs`.
- **New:** `Assets/Prefabs/UI/HpSegment.prefab`.
- **Modified scene:** `Assets/Scenes/BossScene.unity` — add Canvas +
  `BossHealthBar` hierarchy, wire boss reference.
