using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoraeGames.Core
{
    /// <summary>
    /// Lightweight object pool for runtime-spawned effects (projectiles, markers,
    /// smoke) per Rule §4: pooling is mandatory for frequent effect objects.
    /// Keyed stacks; Release deactivates and stores instead of Destroy.
    /// Scene unloads destroy pooled instances — Get() discards stale entries,
    /// so no explicit clear is required.
    /// </summary>
    public static class EffectPool
    {
        private static readonly Dictionary<string, Stack<GameObject>> _pools =
            new Dictionary<string, Stack<GameObject>>();

        private static Transform _root;

        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    GameObject rootObj = new GameObject("[EffectPool]");
                    _root = rootObj.transform;
                }
                return _root;
            }
        }

        /// <summary>
        /// Returns a pooled instance for the key, or builds one with the factory.
        /// The returned object is active; caller is responsible for positioning/reset.
        /// </summary>
        public static GameObject Get(string key, Func<GameObject> factory)
        {
            // Ensure the pool root exists while gameplay is running — Release may be
            // called during scene teardown, where creating objects is unsafe.
            Transform _ = Root;

            if (_pools.TryGetValue(key, out Stack<GameObject> stack))
            {
                // Discard entries destroyed by scene unloads
                while (stack.Count > 0)
                {
                    GameObject pooled = stack.Pop();
                    if (pooled != null)
                    {
                        pooled.SetActive(true);
                        return pooled;
                    }
                }
            }
            return factory();
        }

        /// <summary>Deactivates the object and returns it to its pool. Safe on double-release.</summary>
        public static void Release(string key, GameObject obj)
        {
            if (obj == null) return;
            if (!obj.activeSelf) return; // already released

            obj.SetActive(false);
            // Never create the root here (teardown safety); a destroyed parent just
            // takes the pooled object with it, which Get() tolerates.
            if (_root != null)
                obj.transform.SetParent(_root, false);

            if (!_pools.TryGetValue(key, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                _pools[key] = stack;
            }
            stack.Push(obj);
        }
    }
}
