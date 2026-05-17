# Boss Health Bar UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a top-center, segmented boss-fight-style health bar that updates instantly with a white flash when the boss takes damage.

**Architecture:** `BossController` exposes `MaxHp`, `CurrentHp`, and an `HpChanged(current, max)` event. A new screen-space-overlay `Canvas` hosts a `BossHealthBarUI` that subscribes to the event, instantiates one `HpSegment` per max-HP at `Start`, and toggles segment fill / flashes on damage.

**Tech Stack:** Unity (built-in uGUI + TextMeshPro), MCP for Unity for scene/prefab edits.

**Spec:** `docs/superpowers/specs/2026-05-17-boss-healthbar-ui-design.md`

**No automated tests:** The spec explicitly defers tests — this is UI surface area and the project has no test harness. Each task uses Unity console + Play-mode manual checks as its verification step.

---

## File Structure

| Path | Action | Responsibility |
| --- | --- | --- |
| `Assets/Scripts/Player/BossController.cs` | Modify | Replace `hp` with `maxHp`/`currentHp`, expose properties, fire `HpChanged`. |
| `Assets/Scripts/UI/HpSegment.cs` | Create | Single segment visual: `SetFilled(bool)` + `Flash()`. |
| `Assets/Scripts/UI/BossHealthBarUI.cs` | Create | Subscribes to boss event, builds segment row, dispatches fills + flashes. |
| `Assets/Prefabs/UI/HpSegment.prefab` | Create | Visual prefab with root Image, EmptyOverlay child, FlashOverlay child, `HpSegment` component. |
| `Assets/Scenes/BossScene.unity` | Modify | Add `Canvas` → `BossHealthBar` → `Label` + `SegmentRow`; wire references. |

---

## Task 1: Refactor `BossController` HP and add `HpChanged` event

**Files:**
- Modify: `Assets/Scripts/Player/BossController.cs` (lines 1-4 add using, 16-17 field, ~70-74 TakeDamage, add Start)

- [ ] **Step 1: Add the `Serialization` using directive**

Open `Assets/Scripts/Player/BossController.cs`. After line 4 (`using UnityEngine;`), add:

```csharp
using UnityEngine.Serialization;
```

- [ ] **Step 2: Replace the `hp` field with `maxHp` + runtime `currentHp` and public surface**

Replace the existing block:

```csharp
        [Header("HP")]
        [SerializeField, Min(1)] private int hp = 10;
```

with:

```csharp
        [Header("HP")]
        [SerializeField, Min(1), FormerlySerializedAs("hp")] private int maxHp = 10;
        private int currentHp;

        public int MaxHp => maxHp;
        public int CurrentHp => currentHp;
        public event System.Action<int, int> HpChanged;
```

Why `FormerlySerializedAs("hp")`: any scene/prefab that previously serialized a non-default `hp` value (e.g., tuning to 5 or 20) will carry that value forward into `maxHp` instead of resetting to the field default.

- [ ] **Step 3: Initialize `currentHp` in `Awake`**

In the existing `Awake` method (currently at ~line 94), add this line as the first statement of the method body:

```csharp
            currentHp = maxHp;
```

- [ ] **Step 4: Add `Start` that fires the initial `HpChanged`**

`BossController` has no `Start` method. Add one immediately after `OnEnable`/`OnDisable` (so it's grouped with the lifecycle methods):

```csharp
        private void Start()
        {
            HpChanged?.Invoke(currentHp, maxHp);
        }
```

Why `Start` (not `Awake`): subscribers attach in their own `OnEnable`, which runs after every `Awake` but before any `Start`. Firing in `Start` guarantees the UI is subscribed before the initial value arrives.

- [ ] **Step 5: Update `TakeDamage` to mutate `currentHp`, clamp, and fire the event**

Replace the existing `TakeDamage` method:

```csharp
        public void TakeDamage(int amount, IGridEntity source)
        {
            hp -= amount;
            Debug.Log($"Boss took {amount} damage (hp={hp}, from {source})");
            if (hp <= 0) Debug.Log("Boss would die here — no death handling yet.");
        }
```

with:

```csharp
        public void TakeDamage(int amount, IGridEntity source)
        {
            currentHp = Mathf.Max(0, currentHp - amount);
            Debug.Log($"Boss took {amount} damage (hp={currentHp}, from {source})");
            HpChanged?.Invoke(currentHp, maxHp);
            if (currentHp <= 0) Debug.Log("Boss would die here — no death handling yet.");
        }
```

- [ ] **Step 6: Verify compilation**

In Unity (or via MCP `read_console`), wait for domain reload to finish, then check the console for errors. Expected: zero compile errors. If the console mentions `hp` being undefined anywhere else in the project, search for stale references and update them — `hp` was previously private to `BossController`, so external references shouldn't exist.

Run via MCP:
```
mcp__unity__read_console (types: ["error", "warning"], since this script change)
```

Expected: no errors referring to `BossController.hp`.

- [ ] **Step 7: Smoke-test in Play mode (without UI yet)**

Enter Play mode in `BossScene`. Walk the boss into a Fireball. Expected console output (unchanged shape, just `currentHp` instead of `hp` in the local variable):

```
Boss took 1 damage (hp=9, from Fireball ...)
```

Exit Play mode. The UI doesn't exist yet — this confirms the refactor didn't break the damage path.

- [ ] **Step 8: Commit**

```bash
git add "Boss Jam Project/Assets/Scripts/Player/BossController.cs"
git commit -m "refactor(boss): split hp into maxHp/currentHp + HpChanged event"
```

---

## Task 2: Create `HpSegment` component

**Files:**
- Create: `Assets/Scripts/UI/HpSegment.cs`

- [ ] **Step 1: Create the script**

Create `Assets/Scripts/UI/HpSegment.cs` (the `UI` folder is new — Unity will create it on first asset). Use this exact content:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BossJam.UI
{
    public class HpSegment : MonoBehaviour
    {
        [SerializeField] private GameObject emptyOverlay;
        [SerializeField] private Image flashOverlay;
        [SerializeField, Min(0f)] private float flashDuration = 0.15f;

        private Coroutine flashRoutine;

        public void SetFilled(bool filled)
        {
            if (emptyOverlay != null) emptyOverlay.SetActive(!filled);
        }

        public void Flash()
        {
            if (flashOverlay == null) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            var c = flashOverlay.color;
            float t = 0f;
            c.a = 1f;
            flashOverlay.color = c;
            while (t < flashDuration)
            {
                t += Time.deltaTime;
                c.a = Mathf.Clamp01(1f - t / flashDuration);
                flashOverlay.color = c;
                yield return null;
            }
            c.a = 0f;
            flashOverlay.color = c;
            flashRoutine = null;
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Wait for Unity domain reload, then via MCP `read_console`: expected zero errors. The new namespace `BossJam.UI` is fine — Unity auto-discovers MonoBehaviours regardless of namespace.

- [ ] **Step 3: Commit**

```bash
git add "Boss Jam Project/Assets/Scripts/UI/HpSegment.cs" "Boss Jam Project/Assets/Scripts/UI/HpSegment.cs.meta"
git commit -m "feat(ui): add HpSegment component (SetFilled + Flash)"
```

If the `.meta` file isn't present yet, run a Unity refresh first (MCP `refresh_unity`), then re-stage.

---

## Task 3: Build `HpSegment.prefab`

**Files:**
- Create: `Assets/Prefabs/UI/HpSegment.prefab`

This task uses Unity Editor work (or MCP equivalents). The end state: a UI prefab with this hierarchy and wiring:

```
HpSegment                        (RectTransform, Image red, HpSegment component)
├── EmptyOverlay                 (RectTransform stretch, Image dark grey)
└── FlashOverlay                 (RectTransform stretch, Image white, alpha=0)
```

`HpSegment` component references: `emptyOverlay` → EmptyOverlay GameObject, `flashOverlay` → FlashOverlay Image, `flashDuration` = 0.15.

- [ ] **Step 1: Create the prefab root in the scene (temporarily)**

In `BossScene` (or any temporary scene), create the hierarchy via the Hierarchy menu:

1. Right-click in Hierarchy → UI → Image. Name it `HpSegment`. (This creates a Canvas too if one doesn't exist; we'll discard the temporary Canvas at the end of this task.)
2. Set `HpSegment` `Image.color` to red (e.g., `#C8323C`). Set `RectTransform` size to `32 × 48` (will be overridden by the layout group in the bar later).
3. Add component: `HpSegment` (the script from Task 2).

- [ ] **Step 2: Add `EmptyOverlay` child**

1. Right-click `HpSegment` → UI → Image. Name it `EmptyOverlay`.
2. `RectTransform`: anchor preset "stretch-stretch", left/right/top/bottom = 0.
3. `Image.color`: dark grey with alpha 1 (e.g., `#2A2A2AFF`).
4. Initial state: `GameObject` **inactive** (uncheck the active checkbox top-left of the Inspector). Reason: prefab default state is "filled", and `SetFilled(true)` turns the overlay off.

- [ ] **Step 3: Add `FlashOverlay` child**

1. Right-click `HpSegment` → UI → Image. Name it `FlashOverlay`.
2. `RectTransform`: anchor preset "stretch-stretch", left/right/top/bottom = 0.
3. `Image.color`: white with alpha **0** (`#FFFFFF00`). This is the resting state; `Flash()` ramps alpha 1 → 0.
4. `Image.raycastTarget`: uncheck (overlay should never block input).
5. `GameObject`: active.

Also uncheck `Image.raycastTarget` on the root `HpSegment` Image and on `EmptyOverlay` — none of them should block clicks.

- [ ] **Step 4: Wire `HpSegment` component references**

Select the `HpSegment` root. In the `HpSegment` component:
- `Empty Overlay` → drag the `EmptyOverlay` GameObject.
- `Flash Overlay` → drag the `FlashOverlay` Image (Unity will pick the `Image` component since the field is typed `Image`).
- `Flash Duration` → 0.15.

- [ ] **Step 5: Save as prefab**

1. In the Project window, create folder `Assets/Prefabs/UI` (right-click `Assets/Prefabs` → Create → Folder → `UI`).
2. Drag the `HpSegment` GameObject from Hierarchy into `Assets/Prefabs/UI/`. Confirm "Original Prefab" when Unity asks.
3. Delete the `HpSegment` GameObject (and the temporary Canvas that got auto-created in Step 1, **if** it has no other children) from the Hierarchy.

- [ ] **Step 6: Verify the prefab opens cleanly**

Double-click `HpSegment.prefab` in the Project window. Confirm the hierarchy matches the diagram at the top of this task and the component references in the Inspector are all populated (no `Missing (...)`). Exit prefab edit mode.

- [ ] **Step 7: Commit**

```bash
git add "Boss Jam Project/Assets/Prefabs/UI" "Boss Jam Project/Assets/Prefabs/UI.meta"
git commit -m "feat(ui): add HpSegment prefab"
```

---

## Task 4: Create `BossHealthBarUI` controller

**Files:**
- Create: `Assets/Scripts/UI/BossHealthBarUI.cs`

- [ ] **Step 1: Create the script**

Create `Assets/Scripts/UI/BossHealthBarUI.cs` with this exact content:

```csharp
using System.Collections.Generic;
using BossJam.Player;
using UnityEngine;

namespace BossJam.UI
{
    public class BossHealthBarUI : MonoBehaviour
    {
        [SerializeField] private BossController boss;
        [SerializeField] private HpSegment segmentPrefab;
        [SerializeField] private Transform segmentRow;

        private readonly List<HpSegment> segments = new List<HpSegment>();
        private int prevCurrent;
        private bool built;

        private void Awake()
        {
            if (boss == null || segmentPrefab == null || segmentRow == null)
            {
                Debug.LogWarning($"{nameof(BossHealthBarUI)}: missing reference; disabling.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            Build(boss.MaxHp);
            SyncFromBoss();
        }

        private void OnEnable()
        {
            if (boss == null) return;
            boss.HpChanged += OnHpChanged;
            if (built) SyncFromBoss();
        }

        private void OnDisable()
        {
            if (boss == null) return;
            boss.HpChanged -= OnHpChanged;
        }

        private void Build(int max)
        {
            for (int i = 0; i < max; i++)
            {
                var seg = Instantiate(segmentPrefab, segmentRow);
                seg.SetFilled(true);
                segments.Add(seg);
            }
            built = true;
        }

        private void SyncFromBoss()
        {
            int cur = boss.CurrentHp;
            for (int i = 0; i < segments.Count; i++)
                segments[i].SetFilled(i < cur);
            prevCurrent = cur;
        }

        private void OnHpChanged(int current, int max)
        {
            for (int i = 0; i < segments.Count; i++)
                segments[i].SetFilled(i < current);
            for (int i = current; i < prevCurrent && i < segments.Count; i++)
                segments[i].Flash();
            prevCurrent = current;
        }
    }
}
```

Note: `Start` runs after `OnEnable` in the same frame for a freshly-spawned object, so `boss.MaxHp` is available and the subscription is already in place. `built` guards against double-syncing before `Build` has run.

- [ ] **Step 2: Verify compilation**

MCP `read_console`: expected zero errors. If "namespace `BossJam.Player` not found" appears, confirm `BossController` still declares `namespace BossJam.Player` (it does in Task 1).

- [ ] **Step 3: Commit**

```bash
git add "Boss Jam Project/Assets/Scripts/UI/BossHealthBarUI.cs" "Boss Jam Project/Assets/Scripts/UI/BossHealthBarUI.cs.meta"
git commit -m "feat(ui): add BossHealthBarUI (event-driven segmented bar)"
```

---

## Task 5: Add Canvas + bar hierarchy to `BossScene`

**Files:**
- Modify: `Assets/Scenes/BossScene.unity`

End-state hierarchy:

```
HUD Canvas                      (Canvas: Screen Space - Overlay, CanvasScaler: Scale With Screen Size 1920x1080)
└── BossHealthBar               (RectTransform: anchored top-center, BossHealthBarUI component)
    ├── Label                   (TextMeshProUGUI: "BOSS", centered)
    └── SegmentRow              (RectTransform, HorizontalLayoutGroup, ContentSizeFitter)
```

- [ ] **Step 1: Create the `HUD Canvas`**

Open `Assets/Scenes/BossScene.unity`. In Hierarchy:

1. Right-click empty space → UI → Canvas. Rename to `HUD Canvas`.
2. On the `Canvas` component: `Render Mode` = `Screen Space - Overlay` (default), `Sort Order` = 0.
3. On the `CanvasScaler` component: `UI Scale Mode` = `Scale With Screen Size`, `Reference Resolution` = `1920 × 1080`, `Match` = 0.5 (between width and height).
4. `GraphicRaycaster`: leave defaults.
5. Ensure an `EventSystem` GameObject was auto-created at the scene root. If one already existed, delete the duplicate.

- [ ] **Step 2: Create `BossHealthBar` child**

1. Right-click `HUD Canvas` → Create Empty. Rename to `BossHealthBar`.
2. `RectTransform`: anchor preset "top-center" (hold `Alt` when clicking the preset to also set the pivot). Anchored position `(0, -40)`. Size `(800, 80)`.
3. Add component: `BossHealthBarUI` (from Task 4). Leave its references empty for now; we wire them in Step 5.

- [ ] **Step 3: Create the `Label` child**

1. Right-click `BossHealthBar` → UI → Text - TextMeshPro. (If Unity prompts to import the TMP Essentials package, accept it.) Rename to `Label`.
2. `RectTransform`: anchor "top-center" (`Alt+top-center`). Anchored position `(0, 0)`. Size `(200, 28)`.
3. `TextMeshProUGUI`: Text = `BOSS`, Font Size = `22`, Alignment = `Center` + `Middle`, Color white.
4. `Raycast Target`: uncheck.

- [ ] **Step 4: Create the `SegmentRow` child**

1. Right-click `BossHealthBar` → Create Empty. Rename to `SegmentRow`.
2. `RectTransform`: anchor "bottom-center" (`Alt+bottom-center`). Anchored position `(0, 0)`. Size `(800, 40)`.
3. Add component `Horizontal Layout Group`:
   - `Padding` 0/0/0/0.
   - `Spacing` = 6.
   - `Child Alignment` = `Middle Center`.
   - `Control Child Size`: Width ✓, Height ✓.
   - `Use Child Scale`: both off.
   - `Child Force Expand`: Width off, Height off. (Off so segments stay at the prefab's 32×40 size and the row centers them with spacing.)
4. Add component `Content Size Fitter`:
   - `Horizontal Fit` = `Preferred Size`.
   - `Vertical Fit` = `Preferred Size`.

- [ ] **Step 5: Wire the `BossHealthBarUI` references**

Select `BossHealthBar`. In the `BossHealthBarUI` component:
- `Boss` → drag the boss GameObject from the scene Hierarchy (the one with `BossController`; usually named `Boss` or similar — search the Hierarchy for the `BossController` component).
- `Segment Prefab` → drag `Assets/Prefabs/UI/HpSegment.prefab`.
- `Segment Row` → drag the `SegmentRow` GameObject (from this same hierarchy).

- [ ] **Step 6: Save the scene**

`File → Save` (or `Cmd+S`). Confirm `Assets/Scenes/BossScene.unity` now shows as modified in git.

- [ ] **Step 7: Verify in Play mode**

Enter Play mode. Expected:
- A "BOSS" label appears at top-center.
- 10 red segments sit below the label.
- Walking the boss into a Fireball: one segment goes dark and briefly flashes white.
- Console shows the same `Boss took 1 damage (hp=9, ...)` log as before.

Exit Play mode. **Do not save** any Play-mode changes (Unity should not prompt, but in case it does, discard).

- [ ] **Step 8: Commit**

```bash
git add "Boss Jam Project/Assets/Scenes/BossScene.unity"
git commit -m "feat(scene): wire boss healthbar HUD into BossScene"
```

Note: The scene also has unrelated working-tree changes from the broader feature branch — be careful to stage **only** `BossScene.unity` if those other changes haven't been committed yet. (`git status` will show whether siblings like the mixamo animation files are dirty.)

---

## Task 6: Manual validation pass

No file changes. No commit. This is a structured walkthrough of the spec's testing checklist to confirm the feature is done.

- [ ] **Step 1: Fresh play test from a clean Play mode entry**

Enter Play mode in `BossScene`. Confirm:
1. Bar shows 10 filled segments at start (no flashes on entry).
2. The "BOSS" label is visible above the bar.
3. Boss can move; HUD doesn't follow camera or wobble.

- [ ] **Step 2: Single-segment damage**

Walk the boss into one Fireball. Confirm:
1. Rightmost segment turns dark.
2. That same segment flashes white briefly (~0.15s).
3. Console: `Boss took 1 damage (hp=9, ...)`.

- [ ] **Step 3: Multi-segment damage (optional, if a 2-damage source is wired)**

If no 2-damage source exists in the scene, skip — temporarily call `boss.TakeDamage(2, null)` from the Unity console (via Debug → Window or a quick test button) and confirm two adjacent segments flash together. Revert any temporary test code without committing.

- [ ] **Step 4: Empty the bar**

Take damage 10 times (walk into 10 Fireballs, or repeat the temporary call). Confirm:
1. Bar empties to zero segments filled.
2. Console: `Boss would die here — no death handling yet.`
3. No null-reference or coroutine errors after the bar is empty.

- [ ] **Step 5: Resized `MaxHp`**

Exit Play mode. On the boss, change `Max Hp` in the Inspector to `5`. Enter Play mode. Confirm the bar now shows exactly 5 segments. Restore `Max Hp` to `10` afterward and **don't** commit the temporary change (or commit it separately if you wanted to retune anyway).

- [ ] **Step 6: Disable/enable cycle**

In Play mode, mid-fight (after taking ~3 damage), uncheck the `BossHealthBar` GameObject in the Hierarchy, then re-check it. Confirm:
1. Bar re-appears with the **current** HP state (7 segments filled, 3 dark).
2. No flash on re-enable (it's a sync, not a damage event).
3. Further damage still flashes correctly.

---

## Self-Review Notes

Spec coverage checked against `2026-05-17-boss-healthbar-ui-design.md`:

| Spec section | Plan task(s) |
| --- | --- |
| BossController changes (maxHp/currentHp/event) | Task 1 |
| BossHealthBarUI (Awake/Start/OnEnable/OnDisable/OnHpChanged) | Tasks 4, 5 |
| HpSegment (SetFilled/Flash, flash duration field) | Tasks 2, 3 |
| Scene structure (Canvas/BossHealthBar/Label/SegmentRow) | Task 5 |
| HpSegment.prefab structure | Task 3 |
| Edge case: multi-HP damage | Task 6 step 3 |
| Edge case: MaxHp changed in Inspector | Task 6 step 5 |
| Edge case: missing boss reference | Task 4 (Awake null check) |
| Edge case: HP below 0 (clamp) | Task 1 step 5 |
| Edge case: disable/enable cycles | Tasks 4 (OnEnable re-sync) + 6 step 6 |
| Edge case: rapid hits don't stack flashes | Task 2 (StopCoroutine in Flash) |
| Manual testing checklist | Task 6 |
| Future flexibility (visuals on prefab, segment swap point) | Implicit in Tasks 2/3 separation; no new task needed |

No placeholders. Type/method names verified consistent: `MaxHp`, `CurrentHp`, `HpChanged`, `SetFilled`, `Flash`, `flashDuration`, `emptyOverlay`, `flashOverlay`, `segmentPrefab`, `segmentRow` — all match across tasks.
