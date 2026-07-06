#if UNITY_EDITOR
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectS.EditorTools
{
    /// <summary>
    /// Throwaway greybox generator for early mechanic testing.
    ///   ProjectS > Create Greybox Test Setup  — room + a ready-to-play Player rig at the spawn (one click).
    ///   ProjectS > Create Greybox Room         — just the room.
    ///   ProjectS > Create Player               — just the Player rig (CharacterController + camera + flashlight).
    /// Everything is marked Navigation Static so it can be baked later. Real level design is authored by
    /// hand in the scene editor (architecture.md); this just unblocks movement/AI iteration.
    /// </summary>
    public static class GreyboxRoomGenerator
    {
        private const float RoomSize = 20f;   // floor is RoomSize x RoomSize metres
        private const float WallHeight = 3f;  // ~3m for a backrooms feel (architecture.md)
        private const float WallThickness = 0.3f;

        [MenuItem("ProjectS/Create Greybox Test Setup")]
        public static void CreateTestSetup()
        {
            var room = CreateRoomInternal();
            BakeNavMesh(room);                 // agent needs a baked surface before it spawns
            var player = CreatePlayerInternal();

            var spawn = room.transform.Find("PlayerSpawn");
            if (spawn != null) player.transform.position = spawn.position;

            var monster = CreateMonsterInternal();
            monster.transform.position = new Vector3(RoomSize / 2f - 2f, 1f, RoomSize / 2f - 2f); // far corner

            Selection.activeGameObject = player;
            Debug.Log("[Greybox] Test setup ready. Press Play — WASD move, mouse look. Monster debug keys: 1=Static 2=Watcher 3=Hunter, Q=won-QTE.");
        }

        [MenuItem("ProjectS/Create Game Loop Test")]
        public static void CreateGameLoopTest()
        {
            var room = CreateRoomInternal();
            BakeNavMesh(room);

            var player = CreateGameplayActors(Vector3.zero, new Vector3(0f, 1f, 6f));
            var spawn = room.transform.Find("PlayerSpawn");
            if (spawn != null) player.transform.position = spawn.position;

            // 1 held at start + these 2 findable = 3 total.
            CreateKey("Key_1", new Vector3(6f, 0.6f, 6f));
            CreateKey("Key_2", new Vector3(-7f, 0.6f, -2f));
            CreateExit(new Vector3(7f, 0.6f, -8f));

            Selection.activeGameObject = player;
            Debug.Log("[Greybox] Game loop ready. You hold 1 key; collect 2 more (monster escalates Static→Watcher→Hunter), reach the GREEN exit to WIN.");
        }

        // Just the gameplay actors (Player + Monster + GameState + all systems) — drop into any environment
        // (e.g. an imported art level). No walls, no keys/exit; place those to fit your layout.
        [MenuItem("ProjectS/Create Gameplay Actors")]
        public static void CreateGameplayActorsMenu()
        {
            CreateGameplayActors(Vector3.zero, new Vector3(0f, 1f, 6f));
            Debug.Log("[Greybox] Gameplay actors created. 1) Move Player onto your level's floor. " +
                      "2) ProjectS > Spawn Key (×2) + Spawn Exit, place them. " +
                      "3) Select the level root → ProjectS > Bake NavMesh (Selected). Then Play.");
        }

        private static GameObject CreateGameplayActors(Vector3 playerPos, Vector3 monsterPos)
        {
            var player = CreatePlayerInternal();
            player.transform.position = playerPos;

            var monster = CreateMonsterInternal();
            monster.transform.position = monsterPos;
            SetMonsterStartTierStatic(monster);

            var gsGo = new GameObject("GameState");
            Undo.RegisterCreatedObjectUndo(gsGo, "Create Gameplay Actors");
            var gameState = gsGo.AddComponent<ProjectS.GameState>();
            var gsSo = new SerializedObject(gameState);
            gsSo.FindProperty("_monster").objectReferenceValue = monster.GetComponent<ProjectS.MonsterAI>();
            gsSo.ApplyModifiedProperties();

            var qte = gsGo.AddComponent<ProjectS.QTEController>();
            var qteSo = new SerializedObject(qte);
            qteSo.FindProperty("_monster").objectReferenceValue = monster.GetComponent<ProjectS.MonsterAI>();
            qteSo.FindProperty("_player").objectReferenceValue = player.GetComponent<ProjectS.PlayerController>();
            qteSo.ApplyModifiedProperties();

            gsGo.AddComponent<ProjectS.InsanitySystem>();  // vignette + monster modulation
            gsGo.AddComponent<ProjectS.RearrangeSystem>(); // phantom + light death
            gsGo.AddComponent<ProjectS.ScareDirector>();   // Event B false-catch

            Selection.activeGameObject = player;
            return player;
        }

        [MenuItem("ProjectS/Spawn Key")]
        public static void SpawnKeyMenu()
        {
            Selection.activeGameObject = CreateKey("Key", SceneFocusPoint() + Vector3.up * 0.6f);
        }

        [MenuItem("ProjectS/Spawn Exit")]
        public static void SpawnExitMenu()
        {
            Selection.activeGameObject = CreateExit(SceneFocusPoint() + Vector3.up * 0.6f);
        }

        // Where the Scene view is looking, so spawned items land in front of you (fallback: origin).
        private static Vector3 SceneFocusPoint()
        {
            var view = SceneView.lastActiveSceneView;
            return view != null ? view.pivot : Vector3.zero;
        }

        [MenuItem("ProjectS/Bake NavMesh")]
        public static void BakeNavMeshMenu()
        {
            var root = GameObject.Find("Greybox");
            if (root == null)
            {
                Debug.LogWarning("[Greybox] No 'Greybox' object found to bake.");
                return;
            }
            BakeNavMesh(root);
        }

        // Bake on any environment root (e.g. an imported art level like TstLevel). Select it first.
        [MenuItem("ProjectS/Bake NavMesh (Selected)")]
        public static void BakeNavMeshSelected()
        {
            var sel = Selection.activeGameObject;
            if (sel == null)
            {
                Debug.LogWarning("[Greybox] Select the environment root in the Hierarchy first, then Bake NavMesh (Selected).");
                return;
            }
            BakeNavMesh(sel);
        }

        private static void BakeNavMesh(GameObject room)
        {
            var surface = room.GetComponent<NavMeshSurface>();
            if (surface == null) surface = Undo.AddComponent<NavMeshSurface>(room);
            surface.collectObjects = CollectObjects.Children; // floor + walls under Greybox
            surface.BuildNavMesh();

            // BuildNavMesh() only builds in memory — persist it as an asset so the baked mesh survives a
            // scene reload / clone (otherwise the agent has no navmesh on reopen and the monster won't move).
            if (surface.navMeshData != null)
            {
                const string dir = "Assets/NavMeshData";
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder("Assets", "NavMeshData");

                string path = dir + "/" + room.name + "-NavMesh.asset";
                if (!AssetDatabase.Contains(surface.navMeshData))
                {
                    AssetDatabase.DeleteAsset(path); // clear any stale bake at this path
                    AssetDatabase.CreateAsset(surface.navMeshData, path);
                }
                AssetDatabase.SaveAssets();
            }

            EditorUtility.SetDirty(surface);
            Debug.Log("[Greybox] NavMesh baked + saved to Assets/NavMeshData/Greybox-NavMesh.asset.");
        }

        [MenuItem("ProjectS/Create Greybox Room")]
        public static void CreateRoom()
        {
            Selection.activeGameObject = CreateRoomInternal();
        }

        [MenuItem("ProjectS/Create Player")]
        public static void CreatePlayer()
        {
            Selection.activeGameObject = CreatePlayerInternal();
        }

        private static GameObject CreateRoomInternal()
        {
            var root = new GameObject("Greybox");
            Undo.RegisterCreatedObjectUndo(root, "Create Greybox Room");

            float half = RoomSize / 2f;

            CreateFloor(root, RoomSize);

            // Perimeter walls (N/S span X, E/W span Z).
            CreateWall(root, "Wall_N", new Vector3(0f, WallHeight / 2f, half), new Vector3(RoomSize, WallHeight, WallThickness));
            CreateWall(root, "Wall_S", new Vector3(0f, WallHeight / 2f, -half), new Vector3(RoomSize, WallHeight, WallThickness));
            CreateWall(root, "Wall_E", new Vector3(half, WallHeight / 2f, 0f), new Vector3(WallThickness, WallHeight, RoomSize));
            CreateWall(root, "Wall_W", new Vector3(-half, WallHeight / 2f, 0f), new Vector3(WallThickness, WallHeight, RoomSize));

            // A couple of interior walls so it isn't an empty box (rough corridors to move around).
            CreateWall(root, "Wall_Int1", new Vector3(-3f, WallHeight / 2f, 2f), new Vector3(WallThickness, WallHeight, 10f));
            CreateWall(root, "Wall_Int2", new Vector3(4f, WallHeight / 2f, -3f), new Vector3(8f, WallHeight, WallThickness));

            // Ceiling point lights (targets for the rearrange "light death").
            float ceiling = WallHeight - 0.2f;
            CreateCeilingLight(root, new Vector3(-5f, ceiling, -5f));
            CreateCeilingLight(root, new Vector3(5f, ceiling, -5f));
            CreateCeilingLight(root, new Vector3(-5f, ceiling, 5f));
            CreateCeilingLight(root, new Vector3(5f, ceiling, 5f));

            // Spawn marker for the player.
            var spawn = new GameObject("PlayerSpawn");
            Undo.RegisterCreatedObjectUndo(spawn, "Create Greybox Room");
            spawn.transform.SetParent(root.transform);
            spawn.transform.position = new Vector3(-half + 2f, 0f, -half + 2f);

            return root;
        }

        private static void CreateCeilingLight(GameObject parent, Vector3 pos)
        {
            var go = new GameObject("CeilingLight");
            Undo.RegisterCreatedObjectUndo(go, "Create Greybox Room");
            go.transform.SetParent(parent.transform);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 1.5f;
            light.color = new Color(1f, 0.95f, 0.85f);
        }

        private static GameObject CreatePlayerInternal()
        {
            // Remove the scene's default camera so our first-person camera is the only one.
            var defaultCam = GameObject.Find("Main Camera");
            if (defaultCam != null) Undo.DestroyObjectImmediate(defaultCam);

            var player = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(player, "Create Player");
            player.tag = "Player";

            var cc = player.AddComponent<CharacterController>();
            cc.radius = 0.3f;                       // architecture.md capsule radius
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            player.AddComponent<ProjectS.PlayerController>();

            // First-person camera at eye height.
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(player.transform);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f); // eye height
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();

            // Flashlight spotlight, forward from the camera.
            var lightGo = new GameObject("Flashlight");
            lightGo.transform.SetParent(camGo.transform);
            lightGo.transform.localPosition = Vector3.zero;
            lightGo.transform.localRotation = Quaternion.identity;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 20f;
            light.spotAngle = 60f;
            light.intensity = 3f;
            light.color = new Color(1f, 0.96f, 0.9f);

            return player;
        }

        private static GameObject CreateMonsterInternal()
        {
            var monster = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            monster.name = "Monster";
            Undo.RegisterCreatedObjectUndo(monster, "Create Monster");
            monster.layer = 2; // Ignore Raycast — so it never blocks its own line-of-sight checks

            var agent = monster.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 2f;
            agent.baseOffset = 1f;         // capsule pivot is centred; lift so its base sits on the navmesh
            agent.stoppingDistance = 1.2f; // stop just outside contact — don't drive into the player

            // The encounter is the QTE, not a body-block: make the capsule a trigger so it never jams the
            // player's CharacterController (an overlapping solid capsule freezes movement while look still works).
            var col = monster.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            monster.AddComponent<ProjectS.MonsterAI>();

            Colorize(monster, new Color(0.8f, 0.1f, 0.1f)); // red
            return monster;
        }

        private static void SetMonsterStartTierStatic(GameObject monster)
        {
            var ai = monster.GetComponent<ProjectS.MonsterAI>();
            var so = new SerializedObject(ai);
            var prop = so.FindProperty("_startTier");
            if (prop != null) prop.enumValueIndex = (int)ProjectS.MonsterTier.Static;
            so.ApplyModifiedProperties();
        }

        private static GameObject CreateKey(string name, Vector3 pos)
        {
            var key = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            key.name = name;
            Undo.RegisterCreatedObjectUndo(key, "Spawn Key");
            key.transform.position = pos;
            key.transform.localScale = Vector3.one * 0.5f;
            Object.DestroyImmediate(key.GetComponent<Collider>()); // proximity pickup — no collider needed
            key.AddComponent<ProjectS.Key>();
            Colorize(key, new Color(1f, 0.85f, 0.1f)); // yellow
            return key;
        }

        private static GameObject CreateExit(Vector3 pos)
        {
            var exit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            exit.name = "Exit";
            Undo.RegisterCreatedObjectUndo(exit, "Spawn Exit");
            exit.transform.position = pos;
            exit.transform.localScale = new Vector3(1.5f, 2.2f, 0.3f);
            Object.DestroyImmediate(exit.GetComponent<Collider>());
            exit.AddComponent<ProjectS.ExitDoor>();
            Colorize(exit, new Color(0.1f, 0.8f, 0.2f)); // green
            return exit;
        }

        private static void Colorize(GameObject go, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            var mat = new Material(shader) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void CreateFloor(GameObject parent, float size)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent.transform);
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(size, 0.1f, size);
            MarkNavigationStatic(floor);
        }

        private static void CreateWall(GameObject parent, string name, Vector3 pos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent.transform);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            MarkNavigationStatic(wall);
        }

        private static void MarkNavigationStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic | StaticEditorFlags.ContributeGI);
        }
    }
}
#endif
