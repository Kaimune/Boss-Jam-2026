using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using BossJam.Dialogue;

namespace BossJam.Dialogue.PlayModeTests
{
    public class PreFightDialoguePlayModeTest
    {
        [UnityTest]
        public IEnumerator Controller_PlaysScriptAndFiresFinished()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.AddComponent<Canvas>();
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            var group = canvasGo.AddComponent<CanvasGroup>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;

            var controllerGo = new GameObject("DialogueController");
            controllerGo.transform.SetParent(canvasGo.transform, false);
            var controller = controllerGo.AddComponent<DialogueController>();

            bool finished = false;
            controller.Finished += () => finished = true;

            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(DialogueController).GetField("secondsPerChar", F).SetValue(controller, 0f);
            typeof(DialogueController).GetField("dialogueText", F).SetValue(controller, text);
            typeof(DialogueController).GetField("canvasGroup", F).SetValue(controller, group);

            controller.Play("boss_pre_fight");
            Assert.IsTrue(controller.IsPlaying, "Play() should put controller into IsPlaying.");

            float timeout = 5f;
            while (!finished && timeout > 0f)
            {
                controller.RequestAdvance();
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(finished, "Finished event must fire within 5 seconds.");
            Assert.IsFalse(controller.IsPlaying);
            Object.Destroy(canvasGo);
        }
    }
}
