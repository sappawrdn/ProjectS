#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

namespace ProjectS.EditorTools
{
    /// <summary>
    /// Builds the MainMenu scene: a MainMenuController wired to the onboarding + menu videos (found by name in
    /// Assets/_Project/Video), and registers MainMenu (first) + PlacedObjects in Build Settings so Start loads
    /// the game. Re-runnable.
    /// </summary>
    public static class MenuSceneSetup
    {
        private const string VideoDir = "Assets/_Project/Video";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/PlacedObjects.unity";

        [MenuItem("ProjectS/Build Main Menu Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var onboarding = FindClip(p => p.Contains("onboard"));
            var menu = FindClip(p => p.Contains("menu") && !p.Contains("onboard"));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var go = new GameObject("MainMenu");
            var ctrl = go.AddComponent<MainMenuController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("_onboarding").objectReferenceValue = onboarding;
            so.FindProperty("_menu").objectReferenceValue = menu;
            so.FindProperty("_gameScene").stringValue = "PlacedObjects";
            so.ApplyModifiedProperties();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, MenuScenePath);

            // Build settings: MainMenu first, then the game.
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();

            Selection.activeGameObject = go;
            Debug.Log($"[Menu] MainMenu scene built (onboarding='{Name(onboarding)}', menu='{Name(menu)}') + build list set " +
                      "[MainMenu, PlacedObjects]. Press Play. Tap zones off? nudge _startRect/_settingsRect on MainMenu. " +
                      "Video rotated/portrait? that's the 1080x1440 rotation issue — re-export landscape.");
        }

        private static VideoClip FindClip(System.Func<string, bool> match)
        {
            if (!AssetDatabase.IsValidFolder(VideoDir)) return null;
            var guid = AssetDatabase.FindAssets("t:VideoClip", new[] { VideoDir })
                .FirstOrDefault(g => match(AssetDatabase.GUIDToAssetPath(g).ToLower()));
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<VideoClip>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static string Name(VideoClip c) => c != null ? c.name : "MISSING";
    }
}
#endif
