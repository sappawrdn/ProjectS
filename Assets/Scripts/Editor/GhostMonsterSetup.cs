#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectS.EditorTools
{
    /// <summary>
    /// One-click setup for the designer's Mixamo ghost (Assets/Ghost): configures the FBX imports (Humanoid so
    /// the clips retarget, loop flags, mobile texture size), builds a material from the BaseColor PNG + an
    /// AnimatorController (Idle/Walk/Attack), then swaps the greybox capsule on the scene's Monster for the
    /// animated model (keeping the NavMeshAgent + MonsterAI + collider). Re-runnable.
    ///
    /// Because I can't see the 3D result, expect to nudge a couple of consts from your screenshots:
    ///   GhostHeight (scale), GhostYaw (if it faces the wrong way).
    /// </summary>
    public static class GhostMonsterSetup
    {
        private const string Dir = "Assets/Ghost";
        private const string IdleFbx = Dir + "/Unarmed Idle.fbx";
        private const string WalkFbx = Dir + "/Walking.fbx";
        private const string AttackFbx = Dir + "/Zombie Attack.fbx";
        private const string TexPath = Dir + "/monster_LP_DefaultMaterial_BaseColor.png";
        private const string MatPath = Dir + "/GhostMonster.mat";
        private const string ControllerPath = Dir + "/GhostMonster.controller";

        private const float GhostHeight = 1.9f; // metres tall (normalized from bounds)
        private const float GhostYaw = 0f;      // add 180 if the model faces away from its travel direction

        [MenuItem("ProjectS/Set Up Ghost Monster")]
        public static void SetUp()
        {
            // 1. FBX import config: Humanoid + loop flags (idle/walk loop, attack/turns don't).
            ConfigureFbx(IdleFbx, true);
            ConfigureFbx(WalkFbx, true);
            ConfigureFbx(AttackFbx, false);
            ConfigureFbx(Dir + "/Left Turn 90.fbx", false);
            ConfigureFbx(Dir + "/Right Turn.fbx", false);

            // 2. Texture (shrink the 4K to mobile) + material.
            var tex = ConfigureTexture(TexPath);
            var mat = MakeMaterial(MatPath, tex);

            // 3. Animator controller from the three clips.
            var idle = LoadClip(IdleFbx);
            var walk = LoadClip(WalkFbx);
            var attack = LoadClip(AttackFbx);
            if (idle == null || walk == null)
            {
                Debug.LogWarning("[Ghost] Couldn't load Idle/Walk clips — is the Ghost folder imported? Aborting.");
                return;
            }
            var controller = BuildController(ControllerPath, idle, walk, attack);

            // 4. Swap the capsule on the scene Monster for the animated model.
            AttachToMonster(mat, controller);
        }

        private static void ConfigureFbx(string path, bool loop)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter imp)
            {
                Debug.LogWarning($"[Ghost] Missing FBX: {path}");
                return;
            }
            imp.animationType = ModelImporterAnimationType.Human;      // Mixamo → Humanoid (clips retarget)
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            var clips = imp.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++) clips[i].loopTime = loop;
            if (clips.Length > 0) imp.clipAnimations = clips;

            imp.SaveAndReimport();
        }

        private static Texture2D ConfigureTexture(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.maxTextureSize = 1024; // 4K BaseColor → 1K for mobile
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material MakeMaterial(string path, Texture2D tex)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("[Ghost] URP/Lit shader not found."); return null; }

            AssetDatabase.DeleteAsset(path);
            var mat = new Material(shader);
            if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex; }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static AnimationClip LoadClip(string fbxPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview"));
        }

        private static Avatar LoadAvatar(string fbxPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
        }

        private static AnimatorController BuildController(string path, AnimationClip idle, AnimationClip walk, AnimationClip attack)
        {
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
            sm.defaultState = sIdle;

            var toWalk = sIdle.AddTransition(sWalk);
            toWalk.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            toWalk.hasExitTime = false; toWalk.duration = 0.15f;

            var toIdle = sWalk.AddTransition(sIdle);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            toIdle.hasExitTime = false; toIdle.duration = 0.15f;

            if (attack != null)
            {
                var sAtk = sm.AddState("Attack"); sAtk.motion = attack;
                var toAtk = sm.AddAnyStateTransition(sAtk);
                toAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                toAtk.hasExitTime = false; toAtk.duration = 0.05f;
                var atkOut = sAtk.AddTransition(sIdle);
                atkOut.hasExitTime = true; atkOut.exitTime = 0.9f; atkOut.duration = 0.15f;
            }

            AssetDatabase.SaveAssets();
            return ac;
        }

        private static void AttachToMonster(Material mat, AnimatorController controller)
        {
            var monster = GameObject.Find("Monster");
            if (monster == null) { Debug.LogWarning("[Ghost] No 'Monster' in the scene — open a gameplay scene first."); return; }

            // Hide the greybox capsule visual (keep the collider + NavMeshAgent + MonsterAI).
            if (monster.TryGetComponent(out MeshRenderer capsuleMr)) capsuleMr.enabled = false;

            var old = monster.transform.Find("GhostModel");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbx);
            if (fbx == null) { Debug.LogWarning("[Ghost] Idle FBX not found."); return; }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, monster.transform);
            model.name = "GhostModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, GhostYaw, 0f);
            model.transform.localScale = Vector3.one;

            // Scale to a human-ish height, then drop the feet to the capsule base (capsule pivot centred, base
            // at 1 m below the Monster transform — see CreateMonsterInternal: height 2, baseOffset 1).
            if (TryBounds(model, out Bounds b) && b.size.y > 1e-3f)
                model.transform.localScale *= GhostHeight / b.size.y;
            if (TryBounds(model, out Bounds b2))
                model.transform.position += Vector3.up * ((monster.transform.position.y - 1f) - b2.min.y);

            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (mat != null) smr.sharedMaterial = mat;

            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.avatar = LoadAvatar(IdleFbx);
            anim.applyRootMotion = false; // NavMeshAgent drives position, not the clip

            if (model.GetComponent<MonsterVisual>() == null) model.AddComponent<MonsterVisual>();

            Selection.activeGameObject = model;
            Debug.Log("[Ghost] Ghost model attached to Monster (capsule hidden). Press Play — it should Idle, Walk " +
                      "when chasing, Attack on a scare. Facing wrong way? set GhostYaw = 180. Too big/small? tweak GhostHeight.");
        }

        private static bool TryBounds(GameObject go, out Bounds bounds)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { bounds = default; return false; }
            bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
            return true;
        }
    }
}
#endif
