using UnityEngine;
namespace MoraeGames.UI
{
    /// <summary>Browser-only integration, isolated from desktop builds.</summary>
    public static class WebPlatform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void StarlightCopyText(string text);
#endif
        public static void CopyText(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            StarlightCopyText(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            WebGLInput.captureAllKeyboardInput = false;
#endif
        }
    }
}
