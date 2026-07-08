#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace ProjectS.EditorTools
{
    /// <summary>
    /// One-click: force the 4:3 aspect (pillarbox) on the player camera + create the VHS video overlay wired to
    /// the mp4 in Assets/_Project/Video/. Re-run after dropping/replacing the video. Save the scene afterwards.
    /// </summary>
    public static class VideoFxSetup
    {
        private const string VideoDir = "Assets/_Project/Video";

        [MenuItem("ProjectS/Set Up VHS + 4:3")]
        public static void SetUp()
        {
            // 1. 4:3 on the main (player) camera.
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[VideoFx] No Main Camera in the scene — open a gameplay scene (PlacedObjects) first.");
                return;
            }
            if (cam.GetComponent<AspectRatioEnforcer>() == null)
                Undo.AddComponent<AspectRatioEnforcer>(cam.gameObject);

            // 2. Find the VHS clip (by name — the folder also holds the menu/onboarding videos).
            VideoClip clip = null;
            if (AssetDatabase.IsValidFolder(VideoDir))
            {
                var guid = AssetDatabase.FindAssets("t:VideoClip", new[] { VideoDir })
                    .FirstOrDefault(g => AssetDatabase.GUIDToAssetPath(g).ToLower().Contains("vhs"));
                if (guid != null) clip = AssetDatabase.LoadAssetAtPath<VideoClip>(AssetDatabase.GUIDToAssetPath(guid));
            }

            // 3. Create/refresh the VHS overlay object.
            var go = GameObject.Find("VHSOverlay");
            if (go == null) go = new GameObject("VHSOverlay");
            var overlay = go.GetComponent<VHSOverlay>();
            if (overlay == null) overlay = go.AddComponent<VHSOverlay>();

            var so = new SerializedObject(overlay);
            so.FindProperty("_clip").objectReferenceValue = clip;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(overlay);
            Selection.activeGameObject = go;

            if (clip == null)
                Debug.LogWarning($"[VideoFx] 4:3 set ✓, but NO mp4 found in {VideoDir}. Drop the VHS mp4 there + re-run. (Save the scene either way.)");
            else
                Debug.Log($"[VideoFx] 4:3 + VHS overlay ('{clip.name}') set up. Press Play to see it. SAVE the scene (Cmd+S).");
        }
    }
}
#endif
