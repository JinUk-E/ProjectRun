# 📘 Project Guide: Clicker Game (claude.md)

## 1. 📌 Environment & Tech Stack
- **Engine:** Unity 6000.3.6f1 (URP)
- **Platform:** Mobile (iOS/Android)
- **Input:** New Input System
- **Language:** C# (PascalCase for Methods, _camelCase for private fields)
- **Key Packages:** UniTask, DOTween

## 2. 🏗 Architecture Rules (SOLID)
- **Patterns:** Follow SOLID principles. Use Interfaces/Abstract classes for decoupling.
- **Event Lifecycle:** Always match `+=` and `-=` in `OnEnable`/`OnDisable` to prevent memory leaks.
- **Dependency:** Use `[RequireComponent]` and cache references in `Awake`/`Start`.
- **Data:** Use `ScriptableObject` for static/design data. Limit Singleton to global managers.

## 3. ⚡ Async & Threading Policy (UniTask)
- **No Coroutines:** `IEnumerator` is strictly prohibited. Use `async UniTask`.
- **Safety:** Always pass `this.GetCancellationTokenOnDestroy()` to every `await` call.
- **Exception:** Wrap critical async logic in `try-catch` with `Debug.LogError` logging.
- **Traceability:** Avoid deep callback nesting or excessive event chaining for clear stack traces.

## 4. 🚀 Performance Guidelines
- **Zero-Allocation:** No `GetComponent`, `Find`, or `Camera.main` in `Update/FixedUpdate`.
- **UI Optimization:** - No `int.ToString()` or string interpolation (`$`) in per-frame UI updates.
    - Use TMP's `SetText()` with integer arguments or `StringBuilder` reuse.
- **Pooling:** Mandatory Object Pooling for projectiles, effects, and frequent UI elements.

## 5. 🤖 AI Collaboration Policy
- **Logic First:** Explain implementation logic and architecture before generating code.
- **Authority:** Base all solutions on **Official Documentation** (Unity, SDKs).
- **Documentation:** Use XML comments (`///`) for public APIs and complex logic.

## 6. 📱 UI & Resolution Guidelines
- **Standard:** Use `Scale With Screen Size` (Reference: 1080x1920, Match: 0.5).
- **Adaptability:** Use **Anchors/Pivots** for all aspect ratios (16:9 to 21:9).
- **Safe Area:** Implement padding logic for notched/hole-punch displays.
- **Optimization:** Disable `Raycast Target` on non-interactive UI elements.

## 7. 🔗 Quick Links
- [Unity Scripting API](https://docs.unity3d.com/ScriptReference/index.html)
- [DOTween Docs](https://dotween.demigiant.com/documentation.php)