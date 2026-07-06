#if UNITY_EDITOR
using System.Collections.Generic;
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

        // Maze generator config.
        private const int MazeW = 7;              // cells across
        private const int MazeH = 7;              // cells deep
        private const float CellSize = 5f;        // corridor width (m) — wide enough for props + passing
        private const float MazeWallH = 3f;
        private const float MazeWallThick = 0.25f;
        private const float BraidChance = 0.12f;  // chance to remove an interior wall → loops (evasion flow)

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

        // ===================== Maze level =====================
        // Procedural backrooms maze (greybox). Grid of cells, walls carved by a recursive backtracker, then
        // braided (some interior walls removed) so there are loops to evade through — not a dead-end-only maze.
        // Places the full gameplay rig + keys at opposite corners + exit at the far corner, then bakes navmesh.
        // Re-run for a fresh layout. Skin with real art later by swapping walls at the same grid positions.
        [MenuItem("ProjectS/Generate Maze Level")]
        public static void GenerateMazeLevel()
        {
            var maze = BuildMaze();
            BakeNavMesh(maze);

            var player = CreateGameplayActors(CellCenter(0, 0), CellCenter(MazeW / 2, MazeH / 2) + Vector3.up * 1f);

            CreateExit(CellCenter(MazeW - 1, MazeH - 1) + Vector3.up * 0.6f);
            CreateKey("Key_1", CellCenter(MazeW - 1, 0) + Vector3.up * 0.6f);
            CreateKey("Key_2", CellCenter(0, MazeH - 1) + Vector3.up * 0.6f);

            Selection.activeGameObject = player;
            Debug.Log("[Maze] Maze generated. Player at one corner, GREEN exit at the far corner, 2 keys in the other corners. Re-run 'Generate Maze Level' for a new layout.");
        }

        private static GameObject BuildMaze()
        {
            var root = new GameObject("Maze");
            Undo.RegisterCreatedObjectUndo(root, "Generate Maze Level");

            // Wall grids: vWall[x,z] = wall on the X=x boundary of row z; hWall[x,z] = wall on the Z=z boundary
            // of column x. Start fully walled, then carve.
            var vWall = new bool[MazeW + 1, MazeH];
            var hWall = new bool[MazeW, MazeH + 1];
            for (int x = 0; x <= MazeW; x++) for (int z = 0; z < MazeH; z++) vWall[x, z] = true;
            for (int x = 0; x < MazeW; x++) for (int z = 0; z <= MazeH; z++) hWall[x, z] = true;

            CarveMaze(vWall, hWall);
            BraidMaze(vWall, hWall);

            // Floor (one slab under the whole maze).
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(MazeW * CellSize / 2f, -0.05f, MazeH * CellSize / 2f);
            floor.transform.localScale = new Vector3(MazeW * CellSize, 0.1f, MazeH * CellSize);
            MarkNavigationStatic(floor);

            // Standing vertical walls (run along Z).
            for (int x = 0; x <= MazeW; x++)
                for (int z = 0; z < MazeH; z++)
                    if (vWall[x, z])
                        CreateWall(root, $"V_{x}_{z}",
                            new Vector3(x * CellSize, MazeWallH / 2f, z * CellSize + CellSize / 2f),
                            new Vector3(MazeWallThick, MazeWallH, CellSize));

            // Standing horizontal walls (run along X).
            for (int x = 0; x < MazeW; x++)
                for (int z = 0; z <= MazeH; z++)
                    if (hWall[x, z])
                        CreateWall(root, $"H_{x}_{z}",
                            new Vector3(x * CellSize + CellSize / 2f, MazeWallH / 2f, z * CellSize),
                            new Vector3(CellSize, MazeWallH, MazeWallThick));

            // Scatter ceiling lights (light-death targets + basic lighting).
            for (int x = 1; x < MazeW; x += 2)
                for (int z = 1; z < MazeH; z += 2)
                    CreateCeilingLight(root, CellCenter(x, z) + Vector3.up * (MazeWallH - 0.2f));

            return root;
        }

        // Recursive backtracker (iterative): visit every cell, knocking down a wall to each newly-visited neighbour.
        private static void CarveMaze(bool[,] vWall, bool[,] hWall)
        {
            var visited = new bool[MazeW, MazeH];
            var stack = new Stack<Vector2Int>();
            visited[0, 0] = true;
            stack.Push(new Vector2Int(0, 0));

            while (stack.Count > 0)
            {
                var c = stack.Peek();
                var neighbours = new List<Vector2Int>();
                if (c.x + 1 < MazeW && !visited[c.x + 1, c.y]) neighbours.Add(new Vector2Int(c.x + 1, c.y));
                if (c.x - 1 >= 0 && !visited[c.x - 1, c.y]) neighbours.Add(new Vector2Int(c.x - 1, c.y));
                if (c.y + 1 < MazeH && !visited[c.x, c.y + 1]) neighbours.Add(new Vector2Int(c.x, c.y + 1));
                if (c.y - 1 >= 0 && !visited[c.x, c.y - 1]) neighbours.Add(new Vector2Int(c.x, c.y - 1));

                if (neighbours.Count == 0) { stack.Pop(); continue; }

                var n = neighbours[Random.Range(0, neighbours.Count)];
                if (n.x == c.x + 1) vWall[c.x + 1, c.y] = false;      // carve east
                else if (n.x == c.x - 1) vWall[c.x, c.y] = false;     // carve west
                else if (n.y == c.y + 1) hWall[c.x, c.y + 1] = false; // carve north
                else hWall[c.x, c.y] = false;                         // carve south
                visited[n.x, n.y] = true;
                stack.Push(n);
            }
        }

        // Remove some interior walls so the maze has loops (a chase needs alternate routes, not just dead-ends).
        private static void BraidMaze(bool[,] vWall, bool[,] hWall)
        {
            for (int x = 1; x < MazeW; x++)
                for (int z = 0; z < MazeH; z++)
                    if (vWall[x, z] && Random.value < BraidChance) vWall[x, z] = false;

            for (int x = 0; x < MazeW; x++)
                for (int z = 1; z < MazeH; z++)
                    if (hWall[x, z] && Random.value < BraidChance) hWall[x, z] = false;
        }

        private static Vector3 CellCenter(int x, int z) =>
            new Vector3(x * CellSize + CellSize / 2f, 0f, z * CellSize + CellSize / 2f);

        // Texture + ceiling + red mood lighting, matched to the PSX pack's own renders (dark hospital-red;
        // the flashlight lights your way). Keeps the greybox geometry (perfectly aligned) — swapping actual
        // wall MODELS is a later, iterative step.
        [MenuItem("ProjectS/Skin Maze (PSX + Backrooms mood)")]
        public static void SkinMazePsx()
        {
            var maze = GameObject.Find("Maze");
            if (maze == null)
            {
                Debug.LogWarning("[Skin] No 'Maze' object found — run 'Generate Maze Level' first.");
                return;
            }

            var wallMat = MakeTexturedMaterial("Assets/PSXBackrooms/Textures/TileTextureBase.png", "PSX_Wall", new Vector2(2f, 1.5f));
            var floorMat = MakeTexturedMaterial("Assets/PSXBackrooms/Textures/FloorTile1.png", "PSX_Floor", new Vector2(7f, 7f));
            var ceilMat = MakeTexturedMaterial("Assets/PSXBackrooms/Textures/Ceiling1.png", "PSX_Ceiling", new Vector2(7f, 7f));
            if (wallMat == null || floorMat == null || ceilMat == null) return;

            // Textures on walls + floor.
            int walls = 0;
            foreach (Transform child in maze.transform)
            {
                var r = child.GetComponent<Renderer>();
                if (r == null) continue;
                if (child.name == "Floor") r.sharedMaterial = floorMat;
                else if (child.name.StartsWith("V_") || child.name.StartsWith("H_")) { r.sharedMaterial = wallMat; walls++; }
            }

            // Ceiling slab over the whole maze (always rebuild so a stale/broken one can't linger).
            var oldCeiling = maze.transform.Find("Ceiling");
            if (oldCeiling != null) Object.DestroyImmediate(oldCeiling.gameObject);
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(maze.transform);
            ceiling.transform.position = new Vector3(MazeW * CellSize / 2f, MazeWallH + 0.05f, MazeH * CellSize / 2f);
            ceiling.transform.localScale = new Vector3(MazeW * CellSize, 0.1f, MazeH * CellSize);
            ceiling.GetComponent<Renderer>().sharedMaterial = ceilMat;

            ApplyBackroomsMood(maze);

            Debug.Log($"[Skin] Maze skinned + moody red lighting ({walls} walls + floor + ceiling). " +
                      "Too dark/bright? Tell me, or tweak Directional Light / CeilingLight intensity.");
        }

        // Dark hospital-red mood: dim the sun, dark-red ambient, red-tinted ceiling lights. Flashlight leads.
        private static void ApplyBackroomsMood(GameObject maze)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.025f, 0.025f); // darker

            var sun = GameObject.Find("Directional Light");
            if (sun != null && sun.TryGetComponent(out Light sunLight))
            {
                sunLight.intensity = 0.08f; // near-off; flashlight + ceiling lights lead
                sunLight.color = new Color(1f, 0.55f, 0.5f);
            }

            foreach (Transform child in maze.transform)
            {
                if (child.name != "CeilingLight") continue;
                if (child.TryGetComponent(out Light light))
                {
                    light.color = new Color(1f, 0.3f, 0.26f);
                    light.intensity = 3f;
                    light.range = 12f;
                }
            }
        }

        // Fix giant FBX imports once: measure a known prop, compute a global import scale so it's real-world
        // sized, apply to ALL PSX models + reimport. After this, dragging any PSX model in comes in sensible.
        [MenuItem("ProjectS/Fix PSX Model Scale")]
        public static void FixPsxModelScale()
        {
            const string dir = "Assets/PSXBackrooms/Models";
            const string refPath = dir + "/HospitalBed.fbx";
            var refPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(refPath);
            var refImporter = AssetImporter.GetAtPath(refPath) as ModelImporter;
            if (refPrefab == null || refImporter == null)
            {
                Debug.LogWarning("[Scale] HospitalBed.fbx not found in Assets/PSXBackrooms/Models/.");
                return;
            }

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(refPrefab);
            bool ok = TryWorldBounds(temp, out Bounds b);
            float maxDim = ok ? Mathf.Max(b.size.x, b.size.z) : 0f;
            Object.DestroyImmediate(temp);
            if (maxDim < 1e-4f) { Debug.LogWarning("[Scale] Couldn't measure HospitalBed."); return; }

            // Scale so the bed footprint ≈ 2.2 m; other models share the native unit → they scale in proportion.
            float desired = refImporter.globalScale * (2.2f / maxDim);

            var guids = AssetDatabase.FindAssets("t:Model", new[] { dir });
            int n = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter imp)
                {
                    imp.globalScale = desired;
                    imp.SaveAndReimport();
                    n++;
                }
            }
            Debug.Log($"[Scale] Applied global scale {desired:0.####} to {n} PSX models. Drag any in — sensible size now.");
        }

        // Scatter PSX hospital furniture across maze cells (skipping gameplay-critical cells). Each prop's base
        // is snapped to the floor so it doesn't matter where its pivot is. Re-run to reshuffle (clears old props).
        [MenuItem("ProjectS/Scatter Hospital Props")]
        public static void ScatterHospitalProps()
        {
            var maze = GameObject.Find("Maze");
            if (maze == null)
            {
                Debug.LogWarning("[Props] No 'Maze' found — run 'Generate Maze Level' first.");
                return;
            }

            string[] propNames = { "HospitalBed", "HospitalChair", "HospitalTray" };
            var prefabs = new List<GameObject>();
            foreach (var n in propNames)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/PSXBackrooms/Models/{n}.fbx");
                if (p != null) prefabs.Add(p);
            }
            if (prefabs.Count == 0)
            {
                Debug.LogWarning("[Props] No hospital FBX found in Assets/PSXBackrooms/Models/.");
                return;
            }

            // Fresh start.
            var existing = maze.transform.Find("Props");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var propsRoot = new GameObject("Props");
            Undo.RegisterCreatedObjectUndo(propsRoot, "Scatter Hospital Props");
            propsRoot.transform.SetParent(maze.transform);

            // Don't clutter gameplay-critical cells (spawn, exit, keys, monster start).
            var used = new HashSet<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(MazeW - 1, MazeH - 1),
                new Vector2Int(MazeW - 1, 0), new Vector2Int(0, MazeH - 1),
                new Vector2Int(MazeW / 2, MazeH / 2)
            };

            int placed = 0, guard = 0;
            const int target = 12;
            while (placed < target && guard++ < 300)
            {
                var cell = new Vector2Int(Random.Range(0, MazeW), Random.Range(0, MazeH));
                if (used.Contains(cell)) continue;
                used.Add(cell); // one prop per cell

                var prefab = prefabs[Random.Range(0, prefabs.Count)];
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, propsRoot.transform);
                Undo.RegisterCreatedObjectUndo(inst, "Scatter Hospital Props");
                inst.transform.position = CellCenter(cell.x, cell.y);
                inst.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);
                NormalizeAndFloor(inst, TargetSize(prefab.name));
                placed++;
            }

            Debug.Log($"[Props] Placed {placed} hospital props (auto-scaled to sensible sizes + floor-snapped).");
        }

        // Real-world target footprint per prop so any FBX import scale ends up sensible.
        private static float TargetSize(string prefabName)
        {
            if (prefabName.Contains("Bed")) return 2.2f;
            if (prefabName.Contains("Chair")) return 0.9f;
            return 0.7f; // tray / default
        }

        // Scale the prop so its largest horizontal dimension ≈ targetMaxDim, then sit its base on the floor.
        private static void NormalizeAndFloor(GameObject go, float targetMaxDim)
        {
            if (TryWorldBounds(go, out Bounds b))
            {
                float maxDim = Mathf.Max(b.size.x, b.size.z);
                if (maxDim > 1e-3f) go.transform.localScale *= targetMaxDim / maxDim;
            }
            if (TryWorldBounds(go, out Bounds b2))
                go.transform.position += Vector3.up * (0f - b2.min.y); // base on floor (y=0)
        }

        private static bool TryWorldBounds(GameObject go, out Bounds bounds)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { bounds = default; return false; }
            bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
            return true;
        }

        // ===================== Dress the maze (ceiling props + doors) =====================
        // Run 'Fix PSX Model Scale' first (so models are real-world sized). These offsets let me correct
        // model orientation quickly once you screenshot the result.
        private const int MazeDoorPercent = 85;   // % of walls that get doors (dense hospital corridors)
        private const float DoorYawOffset = 0f;   // add 90/180 if doors face wrong
        private const float DoorScale = 1.3f;     // doors a bit taller

        [MenuItem("ProjectS/Dress Maze — Ceiling + Doors")]
        public static void DressMazeCeilingDoors()
        {
            var maze = GameObject.Find("Maze");
            if (maze == null) { Debug.LogWarning("[Dress] No 'Maze' found — run 'Generate Maze Level' first."); return; }

            var ceilingLight = LoadPsxModel("CeilingLight");
            var sprinkler = LoadPsxModel("CeilingSprinkler");
            var vent = LoadPsxModel("CeilingVent");
            // Only the solid slab doors (DoorType2 has a see-through window that reveals the wall behind).
            var doors = new List<GameObject>();
            foreach (var n in new[] { "DoorType1V1", "DoorType1V2" })
            {
                var d = LoadPsxModel(n);
                if (d != null) doors.Add(d);
            }

            // Gather placement data before adding children (don't mutate while iterating).
            var lightPositions = new List<Vector3>();
            var lightCells = new HashSet<Vector2Int>();
            var walls = new List<(Vector3 pos, bool vertical)>();
            foreach (Transform child in maze.transform)
            {
                if (child.name == "CeilingLight" && child.TryGetComponent<Light>(out _))
                {
                    lightPositions.Add(child.position);
                    lightCells.Add(WorldToCell(child.position));
                }
                else if (child.name.StartsWith("V_")) walls.Add((child.position, true));
                else if (child.name.StartsWith("H_")) walls.Add((child.position, false));
            }

            var old = maze.transform.Find("Dressing");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var dressing = new GameObject("Dressing");
            Undo.RegisterCreatedObjectUndo(dressing, "Dress Maze");
            dressing.transform.SetParent(maze.transform);

            int lights = 0, ceil = 0, doorsPlaced = 0;

            // 1. Light fixtures at the existing point-light spots.
            if (ceilingLight != null)
                foreach (var p in lightPositions) { PlaceCeilingProp(ceilingLight, dressing.transform, p.x, p.z); lights++; }

            // 2. Sprinklers + vents scattered on the other cells.
            for (int x = 0; x < MazeW; x++)
                for (int z = 0; z < MazeH; z++)
                {
                    if (lightCells.Contains(new Vector2Int(x, z))) continue;
                    var c = CellCenter(x, z);
                    float r = Random.value;
                    if (r < 0.4f && sprinkler != null) { PlaceCeilingProp(sprinkler, dressing.transform, c.x, c.z); ceil++; }
                    else if (r < 0.55f && vent != null) { PlaceCeilingProp(vent, dressing.transform, c.x, c.z, 0.12f); ceil++; } // vent hangs deep → embed a bit
                }

            // 3. Doors flush on standing walls, facing the corridor — two per wall (offset along it) for a
            //    dense hospital-corridor look.
            if (doors.Count > 0)
                foreach (var w in walls)
                {
                    if (Random.Range(0, 100) >= MazeDoorPercent) continue;
                    Vector3 along = w.vertical ? Vector3.forward : Vector3.right; // wall runs along Z (V) or X (H)
                    PlaceDoor(doors[Random.Range(0, doors.Count)], dressing.transform, w.pos + along * 1.2f, w.vertical);
                    PlaceDoor(doors[Random.Range(0, doors.Count)], dressing.transform, w.pos - along * 1.2f, w.vertical);
                    doorsPlaced += 2;
                }

            Debug.Log($"[Dress] {lights} lights, {ceil} sprinklers/vents, {doorsPlaced} doors placed. " +
                      "Ceiling props facing UP or doors facing the WRONG way? Tell me — it's a one-line rotation-offset fix.");
        }

        private static GameObject LoadPsxModel(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/PSXBackrooms/Models/{name}.fbx");

        private static Vector2Int WorldToCell(Vector3 pos) => new Vector2Int(
            Mathf.Clamp((int)(pos.x / CellSize), 0, MazeW - 1),
            Mathf.Clamp((int)(pos.z / CellSize), 0, MazeH - 1));

        private static void PlaceCeilingProp(GameObject prefab, Transform parent, float x, float z, float embed = 0f)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(inst, "Dress Maze");

            // Auto lay-flat: rotate so the model's THINNEST axis points up, so a flat fixture sits flush on the
            // ceiling regardless of how it was authored (avoids per-model rotation guessing).
            inst.transform.rotation = Quaternion.identity;
            inst.transform.position = Vector3.zero;
            if (TryWorldBounds(inst, out Bounds lb))
            {
                Vector3 s = lb.size;
                if (s.x <= s.y && s.x <= s.z) inst.transform.rotation = Quaternion.Euler(0f, 0f, 90f);       // x → up
                else if (s.z <= s.y && s.z <= s.x) inst.transform.rotation = Quaternion.Euler(90f, 0f, 0f);  // z → up
                // else y is already thinnest → already flat
            }

            inst.transform.position = new Vector3(x, MazeWallH, z);
            if (TryWorldBounds(inst, out Bounds b))
                inst.transform.position += Vector3.up * (MazeWallH + embed - b.max.y); // flush (+ optional embed)
        }

        private static void PlaceDoor(GameObject prefab, Transform parent, Vector3 wallPos, bool vertical)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(inst, "Dress Maze");

            // Wall normal points into the corridor we place the door on (+X for V walls, +Z for H walls).
            Vector3 wallNormal = vertical ? Vector3.right : Vector3.forward;
            OrientAgainstWall(inst, wallNormal);
            if (DoorYawOffset != 0f) inst.transform.rotation = Quaternion.Euler(0f, DoorYawOffset, 0f) * inst.transform.rotation;

            inst.transform.localScale *= DoorScale; // a touch taller
            inst.transform.position = wallPos + wallNormal * (MazeWallThick / 2f + 0.02f);
            if (TryWorldBounds(inst, out Bounds b))
                inst.transform.position += Vector3.up * (0f - b.min.y); // base on floor
        }

        // Orient a flat, tall object (a door) so its tallest axis stands up and its thinnest axis faces along
        // the wall normal — works whatever the model's authored orientation is.
        private static void OrientAgainstWall(GameObject inst, Vector3 wallNormal)
        {
            inst.transform.rotation = Quaternion.identity;
            inst.transform.position = Vector3.zero;
            if (!TryWorldBounds(inst, out Bounds b)) return;

            Vector3 s = b.size;
            Vector3 tallAxis = (s.x >= s.y && s.x >= s.z) ? Vector3.right : (s.y >= s.z ? Vector3.up : Vector3.forward);
            Vector3 thinAxis = (s.x <= s.y && s.x <= s.z) ? Vector3.right : (s.y <= s.z ? Vector3.up : Vector3.forward);
            if (tallAxis == thinAxis) return; // degenerate; leave as-is

            var srcRot = Quaternion.LookRotation(thinAxis, tallAxis);       // local +Z→thin, +Y→tall
            var tgtRot = Quaternion.LookRotation(wallNormal, Vector3.up);   // → face the corridor, stand up
            inst.transform.rotation = tgtRot * Quaternion.Inverse(srcRot);
        }

        // ===================== Clad walls with WallTemplate panels =====================
        // One WallTemplate per wall face, scaled to cover the greybox wall (5m × 3m). Greybox stays for
        // collision + navmesh; the panel is just the look. Run 'Fix PSX Model Scale' first isn't required here
        // (we scale to fit regardless).
        private const bool WallPanelFlip = false; // set true if panels face into the wall instead of the corridor

        [MenuItem("ProjectS/Clad Walls (WallTemplate)")]
        public static void CladWalls()
        {
            var maze = GameObject.Find("Maze");
            if (maze == null) { Debug.LogWarning("[Clad] No 'Maze' found — run 'Generate Maze Level' first."); return; }

            // WallTemplate2 is the flat panel; WallTemplate1 is a corner piece (not used for straight cladding).
            var panel = LoadPsxModel("WallTemplate2");
            if (panel == null) { Debug.LogWarning("[Clad] WallTemplate2 not found in Assets/PSXBackrooms/Models/."); return; }

            // Measure the panel once (native, unrotated) to work out axis roles + how many tile across a wall.
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(panel);
            probe.transform.rotation = Quaternion.identity;
            probe.transform.localScale = Vector3.one;
            probe.transform.position = Vector3.zero;
            bool measured = TryWorldBounds(probe, out Bounds pb);
            Vector3 sz = measured ? pb.size : Vector3.one;
            Object.DestroyImmediate(probe);
            if (!measured) return;

            int tall = LargestAxis(sz), thin = SmallestAxis(sz), mid = 3 - tall - thin;
            if (tall == thin) return;

            int count = Mathf.Max(1, Mathf.RoundToInt(CellSize / Mathf.Max(0.1f, sz[mid]))); // panels per wall span
            Vector3 localScale = Vector3.one;
            localScale[tall] = MazeWallH / Mathf.Max(1e-4f, sz[tall]);      // height → wall height
            localScale[mid] = CellSize / (count * Mathf.Max(1e-4f, sz[mid])); // width → fill the span evenly
            Quaternion srcRot = Quaternion.LookRotation(AxisVec(thin), AxisVec(tall));

            var walls = new List<(Vector3 pos, bool vertical)>();
            foreach (Transform child in maze.transform)
            {
                if (child.name.StartsWith("V_")) walls.Add((child.position, true));
                else if (child.name.StartsWith("H_")) walls.Add((child.position, false));
            }

            var old = maze.transform.Find("WallCladding");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var clad = new GameObject("WallCladding");
            Undo.RegisterCreatedObjectUndo(clad, "Clad Walls");
            clad.transform.SetParent(maze.transform);

            int placed = 0;
            foreach (var w in walls)
            {
                Vector3 normal = w.vertical ? Vector3.right : Vector3.forward;
                Vector3 along = w.vertical ? Vector3.forward : Vector3.right;
                foreach (var face in new[] { normal, -normal })
                {
                    Vector3 facing = WallPanelFlip ? -face : face;
                    Quaternion rot = Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Inverse(srcRot);
                    for (int i = 0; i < count; i++)
                    {
                        float t = ((i + 0.5f) / count - 0.5f) * CellSize;
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(panel, clad.transform);
                        Undo.RegisterCreatedObjectUndo(inst, "Clad Walls");
                        inst.transform.localScale = localScale;
                        inst.transform.rotation = rot;
                        inst.transform.position = w.pos + face * (MazeWallThick / 2f + 0.02f) + along * t;
                        if (TryWorldBounds(inst, out Bounds b)) inst.transform.position += Vector3.up * (0f - b.min.y);
                        placed++;
                    }
                }
            }
            Debug.Log($"[Clad] {placed} WallTemplate2 panels tiled ({count}/wall face). If they face into the wall, " +
                      "set WallPanelFlip = true. Check from INSIDE a corridor, not outside.");
        }

        private static int LargestAxis(Vector3 v) => v.x >= v.y && v.x >= v.z ? 0 : (v.y >= v.z ? 1 : 2);
        private static int SmallestAxis(Vector3 v) => v.x <= v.y && v.x <= v.z ? 0 : (v.y <= v.z ? 1 : 2);
        private static Vector3 AxisVec(int i) => i == 0 ? Vector3.right : (i == 1 ? Vector3.up : Vector3.forward);

        private static Material MakeTexturedMaterial(string texPath, string matName, Vector2 tiling)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                Debug.LogWarning($"[Skin] Texture not found: {texPath}");
                return null;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Skin] 'Universal Render Pipeline/Lit' shader not found.");
                return null;
            }

            // Runtime material embedded in the scene — avoids the asset-serialization timing that left the
            // 3rd (ceiling) material shaderless/magenta.
            var mat = new Material(shader) { name = matName };
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetTextureScale("_BaseMap", tiling);
            return mat;
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
