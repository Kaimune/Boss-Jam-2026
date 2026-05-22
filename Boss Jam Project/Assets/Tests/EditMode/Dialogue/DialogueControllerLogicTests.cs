using System.Collections.Generic;
using NUnit.Framework;
using BossJam.Dialogue;

namespace BossJam.Dialogue.EditModeTests
{
    public class DialogueControllerLogicTests
    {
        private DialogueScriptAsset SampleScript()
        {
            return new DialogueScriptAsset
            {
                lines = new List<DialogueLine>
                {
                    new DialogueLine { speaker = "minotaur", text = "A" },
                    new DialogueLine { speaker = "theseus",  text = "B" },
                    new DialogueLine { speaker = "theseus",  text = "C" }
                }
            };
        }

        [Test]
        public void Runner_StartsAtFirstLine()
        {
            var runner = new DialogueRunner(SampleScript());
            Assert.AreEqual("minotaur", runner.Current.speaker);
            Assert.AreEqual("A", runner.Current.text);
            Assert.IsFalse(runner.IsFinished);
        }

        [Test]
        public void Runner_AdvancesThroughEveryLine()
        {
            var runner = new DialogueRunner(SampleScript());
            runner.Advance(); Assert.AreEqual("B", runner.Current.text);
            runner.Advance(); Assert.AreEqual("C", runner.Current.text);
            Assert.IsFalse(runner.IsFinished);
            runner.Advance();
            Assert.IsTrue(runner.IsFinished);
        }

        [Test]
        public void Runner_DetectsSpeakerChange()
        {
            var runner = new DialogueRunner(SampleScript());
            Assert.IsTrue(runner.SpeakerChangedSincePrevious);
            runner.Advance();
            Assert.IsTrue(runner.SpeakerChangedSincePrevious);
            runner.Advance();
            Assert.IsFalse(runner.SpeakerChangedSincePrevious);
        }

        [Test]
        public void Runner_EmptyScript_IsFinishedImmediately()
        {
            var runner = new DialogueRunner(new DialogueScriptAsset
            {
                lines = new List<DialogueLine>()
            });
            Assert.IsTrue(runner.IsFinished);
        }
    }
}
