#if UNITY_ADDRESSABLES
using System.Collections.Generic;
using UnityEngine;
using CupkekGames.Luna;

namespace CupkekGames.SceneManagement
{
    /// <summary>
    /// Optional automatic Addressables load of one or more <see cref="SceneSO"/> scenes on <c>Start</c>
    /// (e.g. from an initialization scene). Mirrors <see cref="SceneLoaderStartup"/> but uses
    /// <see cref="SceneLoaderAddressable"/>. Disable <see cref="_loadOnStart"/> to skip automatic loading
    /// while keeping the singleton for manual loads. For persistent bootstrap scenes first, use <see cref="InitializationLoader"/>.
    /// </summary>
    public class SceneLoaderAddressableStartup : MonoBehaviour
    {
        [SerializeField]
        private bool _loadOnStart = true;

        [Tooltip("Scenes to load additively. Null entries are skipped.")]
        [SerializeField]
        private List<SceneSO> _scenesToLoad = new();

        [SerializeField]
        private string _transitionKey = "Fade";

        [Tooltip("Keeps the loading transition visible until SceneLoaderAddressable.CompleteDeferredLoadingTransition() (e.g. from a sequencer in the loaded scene).")]
        [SerializeField]
        private bool _deferFadeOutUntilManualComplete;

        [Tooltip("If true, calls SceneLoaderAddressable.SetActiveScene on the first non-null scene before load.")]
        [SerializeField]
        private bool _setActiveSceneFromFirst = true;

        private void Start()
        {
            if (!_loadOnStart)
                return;

            List<SceneSO> scenes = new List<SceneSO>();
            for (int i = 0; i < _scenesToLoad.Count; i++)
            {
                if (_scenesToLoad[i] != null)
                    scenes.Add(_scenesToLoad[i]);
            }

            if (scenes.Count == 0)
                return;

            SceneTransition transition = SceneTransitionDatabase.Instance.Transitions.GetValue(_transitionKey);

            if (_setActiveSceneFromFirst)
                SceneLoaderAddressable.Instance.SetActiveScene(scenes[0]);

            SceneLoaderAddressable.Instance.LoadScene(scenes, transition, _deferFadeOutUntilManualComplete);
        }
    }
}
#endif
