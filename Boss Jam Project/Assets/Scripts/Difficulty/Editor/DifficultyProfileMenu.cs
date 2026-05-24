#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BossJam.Difficulty.Editor
{
    /// <summary>
    /// Editor-only helper: builds the 8 canonical tier assets under
    /// Assets/Difficulty/Tiers/ and populates their effects programmatically.
    /// Run via Tools > BossJam > Build Default Tier Assets. Idempotent —
    /// rerun whenever the canonical curve changes.
    /// </summary>
    public static class DifficultyProfileMenu
    {
        private const string TiersDir = "Assets/Difficulty/Tiers";

        private static readonly (string fileName, string tierName, string desc, Color tint)[] Defaults =
        {
            ("Impossible.asset", "Impossible", "Hero dies in 1 hit. No movespeed. No abilities. Boss attacks deal 2.", new Color(0.4f, 0.0f, 0.0f)),
            ("VeryHard.asset",   "Very Hard",  "Hero: 3 hp, 5 movespeed, melee enabled.",                              new Color(0.85f, 0.15f, 0.15f)),
            ("Hard.asset",       "Hard",       "Hero: 10 movespeed, melee + dodge, 2 dmg. Boss attacks deal 1.",       new Color(0.95f, 0.55f, 0.10f)),
            ("Medium.asset",     "Medium",     "Hero finds fireballs + regenerates 1 hp / 5s.",                        new Color(0.95f, 0.85f, 0.10f)),
            ("Easy.asset",       "Easy",       "Hero: 4 melee dmg + post-hit iframes.",                                new Color(0.30f, 0.85f, 0.30f)),
            ("Beginner.asset",   "Beginner",   "Hero save-scums on first 1hp. Fireballs ×1.2 faster.",                 new Color(0.15f, 0.75f, 0.95f)),
            ("Tutorial.asset",   "Tutorial",   "Homing fireballs. Conditional respawn-to-full. 15 movespeed.",         new Color(0.15f, 0.45f, 0.95f)),
            ("Error.asset",      "Error.",     "Boss instakill on any hit. Fireballs ×1.44 faster. 20 movespeed.",     new Color(0.65f, 0.20f, 0.85f)),
        };

        [MenuItem("Tools/BossJam/Build Default Tier Assets")]
        public static void BuildDefaultTierAssets()
        {
            if (!Directory.Exists(TiersDir))
                Directory.CreateDirectory(TiersDir);

            var byName = new Dictionary<string, Difficulty>();
            foreach (var d in Defaults)
            {
                string path = $"{TiersDir}/{d.fileName}";
                var existing = AssetDatabase.LoadAssetAtPath<Difficulty>(path);
                if (existing == null)
                {
                    existing = ScriptableObject.CreateInstance<Difficulty>();
                    AssetDatabase.CreateAsset(existing, path);
                }
                existing.tierName = d.tierName;
                existing.description = d.desc;
                existing.tint = d.tint;
                byName[d.tierName] = existing;
                EditorUtility.SetDirty(existing);
            }

            PopulateImpossible(byName["Impossible"]);
            PopulateVeryHard(byName["Very Hard"]);
            PopulateHard(byName["Hard"]);
            PopulateMedium(byName["Medium"]);
            PopulateEasy(byName["Easy"]);
            PopulateBeginner(byName["Beginner"]);
            PopulateTutorial(byName["Tutorial"]);
            PopulateError(byName["Error."]);

            var profileGuids = AssetDatabase.FindAssets("t:DifficultyProfile");
            if (profileGuids.Length == 0)
            {
                Debug.LogWarning("[BossJam] No DifficultyProfile asset found — skipping profile wiring.");
            }
            else
            {
                var profilePath = AssetDatabase.GUIDToAssetPath(profileGuids[0]);
                var profile = AssetDatabase.LoadAssetAtPath<DifficultyProfile>(profilePath);
                if (profile != null)
                {
                    profile.tiers = new List<Difficulty>
                    {
                        byName["Impossible"],
                        byName["Very Hard"],
                        byName["Hard"],
                        byName["Medium"],
                        byName["Easy"],
                        byName["Beginner"],
                        byName["Tutorial"],
                        byName["Error."],
                    };
                    EditorUtility.SetDirty(profile);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BossJam] Built/refreshed {Defaults.Length} tier assets under {TiersDir}/. Impossible populated; other tiers populated in later phases.");
        }

        // ── Tier population helpers ─────────────────────────────────────
        private static StatModifierEffect Stat(Target t, Op op, float v) =>
            new StatModifierEffect { target = t, op = op, value = v };

        private static void PopulateImpossible(Difficulty d)
        {
            d.effects = new List<IDifficultyEffect>
            {
                Stat(Target.HeroMaxHp,             Op.Override, 1),
                Stat(Target.HeroMoveSpeed,         Op.Override, 0),
                Stat(Target.HeroFireballEnabled,   Op.Override, 0),
                Stat(Target.HeroMeleeEnabled,      Op.Override, 0),
                Stat(Target.BossAttackDamage,      Op.Override, 2),
                Stat(Target.BossMaxHp,             Op.Override, 10),
            };
            EditorUtility.SetDirty(d);
        }

        private static void PopulateVeryHard(Difficulty d)
        {
            d.effects = new List<IDifficultyEffect>
            {
                Stat(Target.HeroMaxHp,             Op.Override, 3),
                Stat(Target.HeroMoveSpeed,         Op.Override, 5),
                Stat(Target.HeroMeleeEnabled,      Op.Override, 1),
                Stat(Target.HeroMeleeDamage,       Op.Override, 1),
                Stat(Target.HeroFireballEnabled,   Op.Override, 0),
                Stat(Target.HeroDodgeEnabled,      Op.Override, 0),
                Stat(Target.BossMaxHp,             Op.Override, 10),
                Stat(Target.BossAttackDamage,      Op.Override, 2),
            };
            EditorUtility.SetDirty(d);
        }

        private static void PopulateHard(Difficulty d)
        {
            d.effects = new List<IDifficultyEffect>
            {
                Stat(Target.HeroMaxHp,             Op.Override, 3),
                Stat(Target.HeroMoveSpeed,         Op.Override, 10),
                Stat(Target.HeroMeleeEnabled,      Op.Override, 1),
                Stat(Target.HeroMeleeDamage,       Op.Override, 2),
                Stat(Target.HeroDodgeEnabled,      Op.Override, 1),
                Stat(Target.HeroFireballEnabled,   Op.Override, 0),
                Stat(Target.BossMaxHp,             Op.Override, 10),
                Stat(Target.BossAttackDamage,      Op.Override, 1),
            };
            EditorUtility.SetDirty(d);
        }

        private static void PopulateMedium(Difficulty d)
        {
            d.effects = new List<IDifficultyEffect>
            {
                Stat(Target.HeroMaxHp,             Op.Override, 3),
                Stat(Target.HeroMoveSpeed,         Op.Override, 10),
                Stat(Target.HeroMeleeEnabled,      Op.Override, 1),
                Stat(Target.HeroMeleeDamage,       Op.Override, 2),
                Stat(Target.HeroDodgeEnabled,      Op.Override, 1),
                Stat(Target.HeroFireballEnabled,   Op.Override, 1),
                Stat(Target.BossMaxHp,             Op.Override, 10),
                Stat(Target.BossAttackDamage,      Op.Override, 1),
                new HeroRegenEffect { hpPerInterval = 1, intervalSeconds = 5f },
            };
            EditorUtility.SetDirty(d);
        }

        private static void PopulateEasy(Difficulty d)
        {
            d.effects = new List<IDifficultyEffect>
            {
                Stat(Target.HeroMaxHp,             Op.Override, 3),
                Stat(Target.HeroMoveSpeed,         Op.Override, 10),
                Stat(Target.HeroMeleeEnabled,      Op.Override, 1),
                Stat(Target.HeroMeleeDamage,       Op.Override, 4),
                Stat(Target.HeroDodgeEnabled,      Op.Override, 1),
                Stat(Target.HeroFireballEnabled,   Op.Override, 1),
                Stat(Target.BossMaxHp,             Op.Override, 10),
                Stat(Target.BossAttackDamage,      Op.Override, 1),
                new HeroRegenEffect { hpPerInterval = 1, intervalSeconds = 5f },
                new HeroIframesOnHitEffect { seconds = 1f },
            };
            EditorUtility.SetDirty(d);
        }

        private static void PopulateBeginner(Difficulty d)
        {
            d.effects = new List<IDifficultyEffect>
            {
                Stat(Target.HeroMaxHp,             Op.Override, 3),
                Stat(Target.HeroMoveSpeed,         Op.Override, 10),
                Stat(Target.HeroMeleeEnabled,      Op.Override, 1),
                Stat(Target.HeroMeleeDamage,       Op.Override, 4),
                Stat(Target.HeroDodgeEnabled,      Op.Override, 1),
                Stat(Target.HeroFireballEnabled,   Op.Override, 1),
                Stat(Target.BossMaxHp,             Op.Override, 10),
                Stat(Target.BossAttackDamage,      Op.Override, 1),
                new HeroRegenEffect { hpPerInterval = 1, intervalSeconds = 5f },
                new HeroIframesOnHitEffect { seconds = 1f },
                new HeroRespawnEffect {
                    mode = HeroRespawnMode.SaveScumOnFirstOneHp,
                    thresholdHp = 1,
                    restoreHp = 5,
                    replayIntro = true,
                },
                new FireballSpeedEffect { multiplier = 1.2f },
            };
            EditorUtility.SetDirty(d);
        }

        private static void PopulateTutorial(Difficulty d)
        {
            d.effects = new List<IDifficultyEffect>
            {
                Stat(Target.HeroMaxHp,             Op.Override, 3),
                Stat(Target.HeroMoveSpeed,         Op.Override, 15),
                Stat(Target.HeroMeleeEnabled,      Op.Override, 1),
                Stat(Target.HeroMeleeDamage,       Op.Override, 4),
                Stat(Target.HeroDodgeEnabled,      Op.Override, 1),
                Stat(Target.HeroFireballEnabled,   Op.Override, 1),
                Stat(Target.BossMaxHp,             Op.Override, 10),
                Stat(Target.BossAttackDamage,      Op.Override, 1),
                new HeroRegenEffect { hpPerInterval = 1, intervalSeconds = 5f },
                new HeroIframesOnHitEffect { seconds = 1f },
                new HeroRespawnEffect {
                    mode = HeroRespawnMode.FullHpIfNotInstakilled,
                    thresholdHp = 2,
                    windowSeconds = 1.5f,
                },
                new FireballSpeedEffect { multiplier = 1.2f },
                new FireballHomingEffect { turnRateDegPerSec = 180f },
            };
            EditorUtility.SetDirty(d);
        }

        private static void PopulateError(Difficulty d)
        {
            d.effects = new List<IDifficultyEffect>
            {
                Stat(Target.HeroMaxHp,             Op.Override, 3),
                Stat(Target.HeroMoveSpeed,         Op.Override, 20),
                Stat(Target.HeroMeleeEnabled,      Op.Override, 1),
                Stat(Target.HeroMeleeDamage,       Op.Override, 4),
                Stat(Target.HeroDodgeEnabled,      Op.Override, 1),
                Stat(Target.HeroFireballEnabled,   Op.Override, 1),
                Stat(Target.BossMaxHp,             Op.Override, 10),
                Stat(Target.BossAttackDamage,      Op.Override, 1),
                new HeroRegenEffect { hpPerInterval = 1, intervalSeconds = 5f },
                new HeroIframesOnHitEffect { seconds = 1f },
                new HeroRespawnEffect {
                    mode = HeroRespawnMode.FullHpIfNotInstakilled,
                    thresholdHp = 2,
                    windowSeconds = 1.5f,
                },
                new FireballSpeedEffect { multiplier = 1.44f },
                new FireballHomingEffect { turnRateDegPerSec = 180f },
                new BossInstakillEffect(),
            };
            EditorUtility.SetDirty(d);
        }
    }
}
#endif
