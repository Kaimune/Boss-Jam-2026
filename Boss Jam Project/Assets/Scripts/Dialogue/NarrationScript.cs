using System;
using System.Collections.Generic;

namespace BossJam.Dialogue
{
    [Serializable]
    public class NarrationScript
    {
        public List<string> lines;
    }

    public static class NarrationScriptLoader
    {
        private const string ResourcePathPrefix = "Narration/";

        public static NarrationScript Load(string scriptName)
        {
            var text = UnityEngine.Resources.Load<UnityEngine.TextAsset>(ResourcePathPrefix + scriptName);
            if (text == null) return null;
            return UnityEngine.JsonUtility.FromJson<NarrationScript>(text.text);
        }
    }
}
