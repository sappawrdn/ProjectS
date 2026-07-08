using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

namespace ProjectS.EditorTools
{
    public class RandomizePropTool
    {
        [MenuItem("ProjectS/Props/Scatter Selected Object (Level 3)")]
        public static void ScatterProp()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("Tolong pilih satu objek dulu (misal: HospitalTray) di Hierarchy!");
                return;
            }

            GameObject level = GameObject.Find("Level3");
            if (level == null)
            {
                Debug.LogWarning("Objek 'Level3' tidak ditemukan di scene. Buka Level3 dulu.");
                return;
            }

            // 1. Hapus grup sebaran yang lama (kalau ada)
            string groupName = "Scattered_" + selected.name;
            Transform oldGroup = level.transform.Find(groupName);
            if (oldGroup != null)
            {
                Undo.DestroyObjectImmediate(oldGroup.gameObject);
            }

            // Bikin grup baru
            GameObject group = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(group, "Scatter Prop");
            group.transform.SetParent(level.transform);

            int targetCount = 6; // Jumlah prop yang mau disebar (dikurangi karena 15 kebanyakan)
            int placed = 0;
            float width = 52f; 
            float depth = 52f * (4f/3f); 

            // 2. Sebar secara acak di lantai (menggunakan NavMesh biar nggak nembus tembok)
            for (int i = 0; i < 500 && placed < targetCount; i++)
            {
                Vector3 probe = new Vector3(Random.Range(2f, width - 2f), 1f, Random.Range(2f, depth - 2f));
                
                if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    // Jarak aman dari tembok (minimal 1.5 meter biar lega)
                    if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, NavMesh.AllAreas))
                    {
                        if (edge.distance < 1.5f) continue;
                    }

                    // Duplicate objek asli yang sudah kamu adjust
                    GameObject clone = Object.Instantiate(selected, group.transform);
                    Undo.RegisterCreatedObjectUndo(clone, "Scatter Prop");
                    clone.name = selected.name + "_" + placed;
                    
                    // Set posisi ke koordinat lantai acak, tapi Y-nya pakai dari objek asli biar rodanya pas lantai
                    clone.transform.position = new Vector3(hit.position.x, selected.transform.position.y, hit.position.z);
                    clone.transform.localScale = selected.transform.localScale;
                    
                    // Rotasi: Pertahankan sumbu X dan Z (biar tetap berdiri), putar sumbu Y acak 360 derajat
                    Vector3 origRot = selected.transform.eulerAngles;
                    clone.transform.eulerAngles = new Vector3(origRot.x, Random.Range(0, 360f), origRot.z);

                    placed++;
                }
            }

            // Sembunyikan objek asli biar nggak numpuk
            selected.SetActive(false);

            Debug.Log($"Berhasil menyebar {placed} {selected.name} secara acak! Objek asli disembunyikan (bisa dihapus kalau mau).");
        }
    }
}
