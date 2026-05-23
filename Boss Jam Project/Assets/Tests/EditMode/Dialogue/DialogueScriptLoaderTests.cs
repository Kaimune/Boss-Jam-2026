using NUnit.Framework;
using BossJam.Dialogue;

namespace BossJam.Dialogue.EditModeTests
{
    public class DialogueScriptLoaderTests
    {
        [Test]
        public void Load_ReturnsParsedLines_ForExistingScript()
        {
            var asset = DialogueScriptLoader.Load("intro_wave_1");
            Assert.IsNotNull(asset, "Loader should return a non-null asset for an existing script.");
            Assert.IsNotNull(asset.lines, "Parsed asset should expose a lines list.");
            Assert.Greater(asset.lines.Count, 0, "Pre-fight script should contain at least one line.");
            Assert.IsFalse(string.IsNullOrEmpty(asset.lines[0].speaker), "First line must have a speaker.");
            Assert.IsFalse(string.IsNullOrEmpty(asset.lines[0].text), "First line must have non-empty text.");
        }

        [Test]
        public void Load_ReturnsNull_ForMissingScript()
        {
            var asset = DialogueScriptLoader.Load("__definitely_not_a_real_script__");
            Assert.IsNull(asset);
        }
    }
}
