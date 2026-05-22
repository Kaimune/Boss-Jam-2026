using UnityEngine;

namespace BossJam.Dialogue
{
    public static class DialogueScriptLoader
    {
        private const string ResourcePathPrefix = "Dialogue/";

        public static DialogueScriptAsset Load(string scriptName)
        {
            var text = Resources.Load<TextAsset>(ResourcePathPrefix + scriptName);
            if (text == null) return null;
            return JsonUtility.FromJson<DialogueScriptAsset>(text.text);
        }
    }
}
