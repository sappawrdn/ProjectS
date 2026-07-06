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

                const string path = dir + "/Greybox-NavMesh.asset";
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

            // Spawn marker for the player.
            var spawn = new GameObject("PlayerSpawn");
            Undo.RegisterCreatedObjectUndo(spawn, "Create Greybox Room");
            spawn.transform.SetParent(root.transform);
            spawn.transform.position = new Vector3(-half + 2f, 0f, -half + 2f);

            return root;
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
            agent.baseOffset = 1f; // capsule pivot is centred; lift so its base sits on the navmesh

            monster.AddComponent<ProjectS.MonsterAI>();

            // Greybox red so it reads at a glance.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader) { color = new Color(0.8f, 0.1f, 0.1f) };
                monster.GetComponent<Renderer>().sharedMaterial = mat;
            }

            return monster;
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
