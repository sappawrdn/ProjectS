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
    }
}
