using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

namespace ProjectS.EditorTools
{
    public class RandomizeClusterTool
    {
        [MenuItem("ProjectS/Props/Scatter Selected Cluster (Level 3)")]
        public static void ScatterCluster()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("Tolong pilih satu atau beberapa objek (misal: Bed, Chair, Tray) bersamaan di Hierarchy!");
                return;
            }

            GameObject level = GameObject.Find("Level3");
            if (level == null)
            {
                Debug.LogWarning("Objek 'Level3' tidak ditemukan di scene.");
                return;
            }

            // Hitung pusat dari cluster (rata-rata posisi dari semua objek yang dipilih)
            Vector3 center = Vector3.zero;
            float minY = float.MaxValue;
            foreach (var go in selectedObjects)
            {
                center += go.transform.position;
                if (go.transform.position.y < minY) minY = go.transform.position.y;
            }
            center /= selectedObjects.Length;
            center.y = minY; // Anggap titik tengah terbawah adalah lantai

            string groupName = "Scattered_Cluster";
            Transform oldGroup = level.transform.Find(groupName);
            if (oldGroup != null)
            {
                Undo.DestroyObjectImmediate(oldGroup.gameObject);
            }

            GameObject group = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(group, "Scatter Cluster");
            group.transform.SetParent(level.transform);

            int targetCount = 6; // Sebar 6 paket cluster
            int placed = 0;
            float width = 52f; 
            float depth = 52f * (4f/3f); 

            for (int i = 0; i < 500 && placed < targetCount; i++)
            {
                Vector3 probe = new Vector3(Random.Range(2f, width - 2f), 1f, Random.Range(2f, depth - 2f));
                
                if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    // Butuh jarak aman lebih jauh dari tembok karena ini 1 paket besar (minimal 2.5 meter)
                    if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, NavMesh.AllAreas))
                    {
                        if (edge.distance < 2.5f) continue;
                    }

                    // 1. Buat folder penampung untuk 1 paket ini di lokasi acak (rotasi awal 0)
                    GameObject clusterRoot = new GameObject("Cluster_" + placed);
                    clusterRoot.transform.SetParent(group.transform);
                    clusterRoot.transform.position = new Vector3(hit.position.x, center.y, hit.position.z);
                    clusterRoot.transform.rotation = Quaternion.identity;
                    Undo.RegisterCreatedObjectUndo(clusterRoot, "Scatter Cluster");

                    // 2. Copy semua objek yang dipilih ke dalam folder ini
                    foreach (var go in selectedObjects)
                    {
                        GameObject clone = Object.Instantiate(go, clusterRoot.transform);
                        Undo.RegisterCreatedObjectUndo(clone, "Scatter Cluster");
                        clone.name = go.name;
                        
                        // Hitung jarak asli objek dari titik tengah cluster
                        Vector3 offset = go.transform.position - center;
                        
                        // Taruh clone di posisi baru + offset
                        clone.transform.position = clusterRoot.transform.position + offset;
                        clone.transform.rotation = go.transform.rotation;
                        clone.transform.localScale = go.transform.localScale;
                    }
                    
                    // 3. Putar folder penampungnya secara acak.
                    // Karena objek-objek tadi sudah jadi anak (child), mereka akan ikut berputar mengelilingi titik tengah!
                    clusterRoot.transform.eulerAngles = new Vector3(0, Random.Range(0, 360f), 0);

                    placed++;
                }
            }

            // Sembunyikan objek-objek aslinya
            foreach (var go in selectedObjects)
            {
                go.SetActive(false);
            }

            Debug.Log($"Berhasil menyebar {placed} paket cluster secara acak! Objek asli disembunyikan.");
        }

        [MenuItem("ProjectS/Props/Replace Old Beds with P_BedBedding")]
        public static void ReplaceOldBeds()
        {
            var level = GameObject.Find("Level3") ?? GameObject.Find("PlacedObjects") ?? GameObject.FindObjectOfType<Light>()?.gameObject.scene.GetRootGameObjects()[0];
            if (level == null) return;

            var bedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_BedBedding.prefab");
            if (bedPrefab == null) { Debug.LogWarning("P_BedBedding prefab tidak ditemukan!"); return; }

            int count = 0;
            Transform[] allTransforms = level.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (t == null) continue;
                if (t.name.Contains("HospitalBed") || t.name == "Bed")
                {
                    Vector3 pos = t.position;
                    Quaternion rot = t.rotation;
                    Transform parent = t.parent;

                    GameObject newBed = (GameObject)PrefabUtility.InstantiatePrefab(bedPrefab, parent);
                    Undo.RegisterCreatedObjectUndo(newBed, "Replace Bed");
                    newBed.transform.position = pos;
                    
                    // Putar -90 derajat untuk menyesuaikan orientasi bed baru (biasanya sumbu Y)
                    newBed.transform.rotation = rot;
                    newBed.transform.Rotate(0, -90f, 0, Space.Self);
                    
                    Undo.DestroyObjectImmediate(t.gameObject);
                    count++;
                }
            }
            Debug.Log($"Berhasil me-replace {count} kasur lama dengan P_BedBedding!");
        }

        [MenuItem("ProjectS/Props/Replace Keys with lalve2")]
        public static void ReplaceKeysWithLalve2()
        {
            var valvePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Keys/lalve2.fbx");
            if (valvePrefab == null) { Debug.LogWarning("lalve2.fbx tidak ditemukan di Assets/Keys!"); return; }

            int count = 0;
            ProjectS.Key[] keys = Object.FindObjectsByType<ProjectS.Key>(FindObjectsSortMode.None);
            foreach (var key in keys)
            {
                // Hapus visual bola kuning (Sphere) lama biar gak numpuk
                var meshFilter = key.GetComponent<MeshFilter>();
                var meshRenderer = key.GetComponent<MeshRenderer>();
                if (meshFilter) Undo.DestroyObjectImmediate(meshFilter);
                if (meshRenderer) Undo.DestroyObjectImmediate(meshRenderer);

                // Pasang aset lalve2 ke dalam objek kunci
                if (key.transform.Find(valvePrefab.name) == null)
                {
                    GameObject valve = (GameObject)PrefabUtility.InstantiatePrefab(valvePrefab, key.transform);
                    Undo.RegisterCreatedObjectUndo(valve, "Replace Key");
                    valve.name = valvePrefab.name;
                    valve.transform.localPosition = Vector3.zero;
                    valve.transform.localRotation = Quaternion.identity;
                    count++;
                }
            }
            Debug.Log($"Berhasil me-replace {count} bola kuning dengan lalve2!");
        }

        [MenuItem("ProjectS/Props/Replace Old Chairs with New Chair")]
        public static void ReplaceOldChairs()
        {
            string dir = "Assets/hospital-chair/source/model/";
            
            // 1. Setup Material URP biar ga warna pink (Magenta)
            string matPath = dir + "ChairMat.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture>(dir + "textures/lambert1_albedo.jpg"));
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture>(dir + "textures/lambert1_metallic.jpg"));
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture>(dir + "textures/lambert1_normal.jpg"));
                mat.SetFloat("_Smoothness", 0.3f);
                AssetDatabase.CreateAsset(mat, matPath);
                
                // Pastikan normal map di-import sebagai Normal Map
                var importer = AssetImporter.GetAtPath(dir + "textures/lambert1_normal.jpg") as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
            }

            // 2. Bikin Prefab yang udah dikasih material
            string prefabPath = dir + "Chair_Ready.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                GameObject dae = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "model.dae");
                if (dae == null) { Debug.LogWarning("model.dae gak ketemu!"); return; }
                
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(dae);
                Renderer[] rs = inst.GetComponentsInChildren<Renderer>();
                foreach (var r in rs) r.sharedMaterial = mat;
                prefab = PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
                Object.DestroyImmediate(inst);
            }

            // 3. Replace semua kursi di level
            var level = GameObject.Find("Level3") ?? GameObject.Find("PlacedObjects") ?? GameObject.FindObjectOfType<Light>()?.gameObject.scene.GetRootGameObjects()[0];
            if (level == null) return;
            
            int count = 0;
            Transform[] all = level.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t != null && t.name.Contains("HospitalChair"))
                {
                    GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t.parent);
                    Undo.RegisterCreatedObjectUndo(newObj, "Replace Chair");
                    newObj.transform.position = t.position;
                    
                    // DAe biasanya tiduran (X=-90), jadi kita sesuaikan
                    newObj.transform.rotation = t.rotation;
                    newObj.transform.Rotate(-90f, 180f, 0, Space.Self); 
                    
                    Undo.DestroyObjectImmediate(t.gameObject);
                    count++;
                }
            }
            Debug.Log($"Berhasil me-replace {count} kursi lama dengan Chair_Ready!");
        }

        [MenuItem("ProjectS/Props/Replace Old Trays with P_Med_Box_01")]
        public static void ReplaceOldTrays()
        {
            var level = GameObject.Find("Level3") ?? GameObject.Find("PlacedObjects") ?? GameObject.FindObjectOfType<Light>()?.gameObject.scene.GetRootGameObjects()[0];
            if (level == null) return;

            var trayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Med_box_01.prefab");
            if (trayPrefab == null) { Debug.LogWarning("P_Med_box_01 prefab tidak ditemukan!"); return; }

            int count = 0;
            Transform[] all = level.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t != null && t.name.Contains("HospitalTray"))
                {
                    Vector3 pos = t.position;
                    Quaternion rot = t.rotation;
                    Transform parent = t.parent;

                    GameObject newTray = (GameObject)PrefabUtility.InstantiatePrefab(trayPrefab, parent);
                    Undo.RegisterCreatedObjectUndo(newTray, "Replace Tray");
                    newTray.transform.position = pos;
                    newTray.transform.rotation = rot;
                    
                    Undo.DestroyObjectImmediate(t.gameObject);
                    count++;
                }
            }
            Debug.Log($"Berhasil me-replace {count} tray lama dengan P_Med_box_01!");
        }

        [MenuItem("ProjectS/Props/Replace Old Doors with dnk_dev (Level 3)")]
        public static void ReplaceOldDoors()
        {
            var level = GameObject.Find("Level3") ?? GameObject.Find("PlacedObjects") ?? GameObject.FindObjectOfType<Light>()?.gameObject.scene.GetRootGameObjects()[0];
            if (level == null) return;

            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Door_01_Base.prefab");
            if (doorPrefab == null) { Debug.LogWarning("P_Door_01_Base prefab tidak ditemukan!"); return; }

            int count = 0;
            Transform doorsRoot = level.transform.Find("Doors");
            if (doorsRoot == null)
            {
                Transform[] all = level.GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                {
                    if (t != null && t.name.Contains("DoorType"))
                    {
                        ReplaceDoor(t, doorPrefab);
                        count++;
                    }
                }
            }
            else
            {
                Transform[] children = new Transform[doorsRoot.childCount];
                for (int i = 0; i < doorsRoot.childCount; i++) children[i] = doorsRoot.GetChild(i);
                foreach (var t in children)
                {
                    ReplaceDoor(t, doorPrefab);
                    count++;
                }
            }
            Debug.Log($"Berhasil me-replace {count} pintu lama dengan dnk_dev door!");
        }

        private static void ReplaceDoor(Transform oldDoor, GameObject newPrefab)
        {
            GameObject newDoor = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab, oldDoor.parent);
            Undo.RegisterCreatedObjectUndo(newDoor, "Replace Door");
            newDoor.transform.position = oldDoor.position;
            
            // Pintu lama (DoorType1) mungkin punya scale 1.3f (dari DoorScale), kita kembalikan ke 1
            newDoor.transform.localScale = Vector3.one; 
            
            // Samakan rotasi
            newDoor.transform.rotation = oldDoor.rotation;
            
            Undo.DestroyObjectImmediate(oldDoor.gameObject);
        }

        [MenuItem("ProjectS/Props/Dress Exit Door (dnk_dev + Neon)")]
        public static void DressExitDoor()
        {
            GameObject exitObj = GameObject.Find("Exit");
            if (exitObj == null) { Debug.LogWarning("Objek 'Exit' tidak ditemukan di scene!"); return; }

            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Door_01_.prefab");
            var signPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PSXBackrooms/Models/ExitSign.fbx");
            
            if (doorPrefab == null || signPrefab == null) { Debug.LogWarning("Prefab pintu atau neon tidak ditemukan!"); return; }

            // Hapus visual kubus hijau lama (tapi biarkan BoxCollider-nya sebagai trigger)
            var meshFilter = exitObj.GetComponent<MeshFilter>();
            var meshRenderer = exitObj.GetComponent<MeshRenderer>();
            if (meshFilter) Undo.DestroyObjectImmediate(meshFilter);
            if (meshRenderer) Undo.DestroyObjectImmediate(meshRenderer);
            
            // Tambahkan pintu
            if (exitObj.transform.Find(doorPrefab.name) == null)
            {
                GameObject newDoor = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, exitObj.transform);
                Undo.RegisterCreatedObjectUndo(newDoor, "Dress Exit");
                newDoor.name = doorPrefab.name;
                
                // Pastikan pintu napak di lantai (y = 0)
                newDoor.transform.position = new Vector3(exitObj.transform.position.x, 0, exitObj.transform.position.z);
                newDoor.transform.localRotation = Quaternion.identity;
                
                // Putar 90 derajat biar sejajar tembok exit-nya
                newDoor.transform.Rotate(0, 90f, 0, Space.Self);
            }

            // Tambahkan Neon Sign di atas pintu
            if (exitObj.transform.Find("ExitNeon") == null)
            {
                GameObject neon = (GameObject)PrefabUtility.InstantiatePrefab(signPrefab, exitObj.transform);
                Undo.RegisterCreatedObjectUndo(neon, "Dress Exit Neon");
                neon.name = "ExitNeon";
                
                // Letakkan di atas pintu (tinggi 2.3 meter)
                neon.transform.position = new Vector3(exitObj.transform.position.x, 2.3f, exitObj.transform.position.z);
                neon.transform.localRotation = Quaternion.identity;
                neon.transform.Rotate(0, -90f, 0, Space.Self);
                neon.transform.localScale = Vector3.one * 1.5f;
                
                // Set material neon biar nyala (pakai ExitSignRedTex)
                var neonRenderer = neon.GetComponentInChildren<MeshRenderer>();
                if (neonRenderer)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    var tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/PSXBackrooms/Textures/ExitSignRedTex.png");
                    if (tex)
                    {
                        mat.mainTexture = tex;
                        mat.SetTexture("_EmissionMap", tex);
                        mat.SetColor("_EmissionColor", Color.white * 2.5f);
                        mat.EnableKeyword("_EMISSION");
                    }
                    neonRenderer.sharedMaterial = mat;
                }
            }
            
            Debug.Log("Pintu Exit berhasil didandani jadi super horor (Pintu dnk_dev + Lampu Neon Merah)!");
        }
    }
}
