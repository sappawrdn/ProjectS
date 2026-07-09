#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectS.EditorTools
{
    /// <summary>
    /// One-click audio setup: configures each clip's import (force-mono for the 3D positional sounds, streaming
    /// for the big beds, decompress-on-load for low-latency one-shots) and creates + wires the AudioDirector in
    /// the scene. Re-runnable. Save the scene after so the AudioDirector persists.
    /// </summary>
    public static class AudioSetup
    {
        private const string T1 = "Assets/_Project/Audio/TIER 1/";
        private const string T2 = "Assets/_Project/Audio/TIER 2/";

        // path, forceMono, loadType
        private static readonly (string path, bool mono, AudioClipLoadType load)[] Imports =
        {
            (T1 + "ambient-bed.wav",   false, AudioClipLoadType.Streaming),        // 2D bed, big
            (T2 + "vhs.wav",           false, AudioClipLoadType.Streaming),        // 2D overlay, big
            (T2 + "breath.wav",        false, AudioClipLoadType.Streaming),        // 2D player breath, big
            (T2 + "hearbeat.wav",      false, AudioClipLoadType.CompressedInMemory),
            (T1 + "creature-moving.wav",  true, AudioClipLoadType.CompressedInMemory), // 3D loop
            (T2 + "creature-chasing.wav", true, AudioClipLoadType.Streaming),          // 3D loop, big
            (T1 + "beacon-key.wav",    true, AudioClipLoadType.CompressedInMemory),    // 3D loop
            (T1 + "beacon-door.wav",   true, AudioClipLoadType.CompressedInMemory),    // 3D loop
            (T1 + "creature-detect.wav", true, AudioClipLoadType.DecompressOnLoad),    // 3D one-shot
            (T1 + "creature-attack.wav", true, AudioClipLoadType.DecompressOnLoad),    // 3D one-shot
            (T2 + "creature-scream-untukJumpscare.wav", true, AudioClipLoadType.DecompressOnLoad),
            (T2 + "stinger.wav",       false, AudioClipLoadType.DecompressOnLoad),
            (T2 + "key-pickup.wav",    false, AudioClipLoadType.DecompressOnLoad),
            (T2 + "wall-bump.wav",     false, AudioClipLoadType.DecompressOnLoad),
            (T2 + "player-footstep.wav", false, AudioClipLoadType.DecompressOnLoad),
            (T2 + "QTE-hit.wav",       false, AudioClipLoadType.DecompressOnLoad),  // 2D one-shot
            (T2 + "QTE-miss.wav",      false, AudioClipLoadType.DecompressOnLoad),  // 2D one-shot
        };

        [MenuItem("ProjectS/Set Up Audio")]
        public static void SetUp()
        {
            // 1. Import config.
            foreach (var (path, mono, load) in Imports)
            {
                if (AssetImporter.GetAtPath(path) is not AudioImporter ai)
                {
                    Debug.LogWarning($"[Audio] Missing clip: {path}");
                    continue;
                }
                ai.forceToMono = mono;
                var s = ai.defaultSampleSettings;
                s.loadType = load;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                ai.defaultSampleSettings = s;
                ai.SaveAndReimport();
            }

            // 2. Create + wire the AudioDirector.
            var go = GameObject.Find("AudioDirector");
            if (go == null) go = new GameObject("AudioDirector");
            var dir = go.GetComponent<AudioDirector>();
            if (dir == null) dir = go.AddComponent<AudioDirector>();

            dir.ambientBed     = Load(T1 + "ambient-bed.wav");
            dir.vhs            = Load(T2 + "vhs.wav");
            dir.heartbeat      = Load(T2 + "hearbeat.wav");
            dir.breath         = Load(T2 + "breath.wav");
            dir.creatureMoving = Load(T1 + "creature-moving.wav");
            dir.creatureChasing= Load(T2 + "creature-chasing.wav");
            dir.creatureDetect = Load(T1 + "creature-detect.wav");
            dir.creatureAttack = Load(T1 + "creature-attack.wav");
            dir.creatureScream = Load(T2 + "creature-scream-untukJumpscare.wav");
            dir.beaconKey      = Load(T1 + "beacon-key.wav");
            dir.beaconDoor     = Load(T1 + "beacon-door.wav");
            dir.stinger        = Load(T2 + "stinger.wav");
            dir.keyPickup      = Load(T2 + "key-pickup.wav");
            dir.wallBump       = Load(T2 + "wall-bump.wav");
            dir.footstep       = Load(T2 + "player-footstep.wav");
            dir.qteHit         = Load(T2 + "QTE-hit.wav");
            dir.qteMiss        = Load(T2 + "QTE-miss.wav");

            EditorUtility.SetDirty(dir);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            Debug.Log("[Audio] Imports configured + AudioDirector wired. SAVE THE SCENE (Cmd+S), then Play. " +
                      "Too loud/quiet on a channel? tweak the AudioDirector's level fields.");
        }

        private static AudioClip Load(string path)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (c == null) Debug.LogWarning($"[Audio] Clip not found: {path}");
            return c;
        }
    }
}
#endif
