using UnityEngine;

namespace MoraeGames.Core
{
    /// <summary>
    /// Generic singleton base class for MonoBehaviours.
    /// Persists across scene loads via DontDestroyOnLoad.
    /// Returns null during application quit to avoid resurrecting ghost objects.
    /// </summary>
    public class SingletonBase<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static bool _isQuitting;

        public static T Instance
        {
            get
            {
                if (_isQuitting) return null;

                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<T>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject($"{typeof(T).Name} (Auto-Created)");
                        _instance = go.AddComponent<T>();
                    }

                    // Getter may resolve the instance before its own Awake runs;
                    // ensure persistence here as well (DontDestroyOnLoad requires a root object).
                    if (_instance.transform.parent == null)
                        DontDestroyOnLoad(_instance.gameObject);
                }
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                if (transform.parent == null)
                    DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }
    }
}
