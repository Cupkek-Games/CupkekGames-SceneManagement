#if UNITY_ADDRESSABLES
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CupkekGames.Systems
{
  /// <summary>
  /// This class is a base class which contains what is common to all game scenes (Locations, Menus, Managers)
  /// </summary>
  [CreateAssetMenu(fileName = "SceneSO", menuName = "CupkekGames/Sequencer/Scene/SceneSO")]
  public class SceneSO : ScriptableObject
  {
    [Header("Make sure asset is a Scene")]
    public AssetReference sceneReference; //Used at runtime to load the scene from the right AssetBundle
  }
}
#endif