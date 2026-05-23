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
        private static (DialogueController controller, GameObject canvas) BuildScene(float secondsPerChar, float interLine)
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

            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(DialogueController).GetField("secondsPerChar", F).SetValue(controller, secondsPerChar);
            typeof(DialogueController).GetField("interLineHoldSeconds", F).SetValue(controller, interLine);
            typeof(DialogueController).GetField("dialogueText", F).SetValue(controller, text);
            typeof(DialogueController).GetField("canvasGroup", F).SetValue(controller, group);

            return (controller, canvasGo);
        }

        [UnityTest]
        public IEnumerator Controller_AutoAdvancesAndFiresFinished()
        {
            var (controller, canvas) = BuildScene(0f, 0f);
            bool finished = false;
            controller.Finished += () => finished = true;

            controller.Play("intro_wave_1");
            Assert.IsTrue(controller.IsPlaying);

            float timeout = 5f;
            while (!finished && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }

            Assert.IsTrue(finished, "Finished must fire within 5s without manual advance.");
            Assert.IsFalse(controller.IsPlaying);
            Object.Destroy(canvas);
        }

        [UnityTest]
        public IEnumerator Controller_SkipAllImmediatelyFinishes()
        {
            var (controller, canvas) = BuildScene(0.5f, 1f);
            bool finished = false;
            controller.Finished += () => finished = true;

            controller.Play("intro_wave_1");
            yield return null;
            controller.SkipAll();

            float timeout = 1.5f;
            while (!finished && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }

            Assert.IsTrue(finished, "SkipAll must terminate within ~1s.");
            Object.Destroy(canvas);
        }
    }
}
