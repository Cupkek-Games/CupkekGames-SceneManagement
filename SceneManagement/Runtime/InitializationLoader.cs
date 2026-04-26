#if UNITY_ADDRESSABLES
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using CupkekGames.Core;
using CupkekGames.Luna;

namespace CupkekGames.Systems
{
  /// <summary>
  /// This class is responsible for starting the game by loading the persistent managers scene 
  /// and raising the event to load the Main Menu
  /// </summary>

  public class InitializationLoader : MonoBehaviour
  {
    [MultiLineHeader("Persistent Scenes\nScenes that will load at start and never unload again.\nThese scenes are NOT managed by SceneLoader.\nDo not create SceneSO for them.\nMake sure asset is a Scene.")]
    [SerializeField] private List<AssetReference> persistentScenes;
    [MultiLineHeader("Start Scenes\nScenes to load after persistent scenes are loaded.\nThese scenes are managed by SceneLoader.")]
    [SerializeField] private List<SceneSO> startScenes;
    [MultiLineHeader("Unload Persistent Scenes\nSet this to true if the persistent scenes only need to load once.\nThey will be unloaded immediately.")]
    [SerializeField] private bool _unloadPersistentScenes;
    private List<AsyncOperationHandle<SceneInstance>> _sceneHandles = new();
    private int _persistentLoaded = 0;

    private void Start()
    {
      if (persistentScenes.Count == 0)
      {
        OnPersistentScenesReady();
      }
      else
      {
        foreach (AssetReference persistent in persistentScenes)
        {
          var handle = persistent.LoadSceneAsync(LoadSceneMode.Additive, true);

          handle.Completed += OnPersistentSceneLoad;

          _sceneHandles.Add(handle);
        }
      }
    }

    private void OnPersistentSceneLoad(AsyncOperationHandle<SceneInstance> obj)
    {
      _persistentLoaded++;

      if (_persistentLoaded == persistentScenes.Count)
      {
        OnPersistentScenesReady();
      }
    }

    private void OnPersistentScenesReady()
    {
      if (_unloadPersistentScenes)
      {
        foreach (AsyncOperationHandle<SceneInstance> handle in _sceneHandles)
        {
          Addressables.UnloadSceneAsync(handle);
        }
      }

      SceneLoaderAddressable.Instance.SetActiveScene(startScenes[0]);
      SceneLoaderAddressable.Instance.LoadScene(startScenes, SceneTransitionDatabase.Instance.Transitions.GetValue("Fade"));
    }
  }
}
#endif