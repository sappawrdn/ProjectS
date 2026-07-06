#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace ProjectS.EditorTools
{
    /// <summary>
    /// The Apple PHASE plug-in (PHASEBuildStep.OnProcessEntitlements) unconditionally injects two
    /// entitlements into every iOS build:
    ///   - com.apple.developer.coremotion.head-pose        (physical head-pose tracking)
    ///   - com.apple.developer.spatial-audio.profile-access (Spatial Audio profile access)
    /// Both require a PAID Apple Developer account to provision; a free "Personal Team" can't sign them,
    /// so the Xcode build fails with "requires a provisioning profile with the Head Pose and Spatial
    /// Audio Profile features."
    ///
    /// Our design pans spatial audio from the in-game listener transform — it does NOT need physical
    /// head-pose tracking — so we strip both after Unity generates the Xcode project. This runs LATE
    /// (high callbackOrder) so it executes after PHASE has written the entitlements file.
    ///
    /// NOTE: if we ever move to a paid account AND want AirPods head-tracking / Spatial Audio profile,
    /// set STRIP = false (or delete this file) so the entitlements survive.
    /// </summary>
    public static class StripPaidEntitlements
    {
        private const bool STRIP = true;

        private static readonly string[] KeysToRemove =
        {
            "com.apple.developer.coremotion.head-pose",
            "com.apple.developer.spatial-audio.profile-access",
        };

        [PostProcessBuild(9999)] // run after Apple.Core / PHASE build steps
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (!STRIP || target != BuildTarget.iOS) return;

            // Unity names the file "{ProductName}.entitlements"; glob to stay robust to renames.
            string[] entitlementFiles = Directory.GetFiles(pathToBuiltProject, "*.entitlements", SearchOption.TopDirectoryOnly);
            if (entitlementFiles.Length == 0)
            {
                Debug.Log("[StripPaidEntitlements] No .entitlements file found — nothing to strip.");
                return;
            }

            foreach (string file in entitlementFiles)
            {
                var plist = new PlistDocument();
                plist.ReadFromFile(file);

                bool changed = false;
                foreach (string key in KeysToRemove)
                {
                    if (plist.root.values.ContainsKey(key))
                    {
                        plist.root.values.Remove(key);
                        changed = true;
                    }
                }

                if (changed)
                {
                    plist.WriteToFile(file);
                    Debug.Log($"[StripPaidEntitlements] Removed paid-only entitlements from {Path.GetFileName(file)} " +
                              "(free-account signing). Set STRIP=false if you move to a paid account and want head-tracking.");
                }
            }
        }
    }
}
#endif
