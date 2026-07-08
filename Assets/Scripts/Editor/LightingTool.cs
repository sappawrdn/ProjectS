using UnityEngine;
using UnityEditor;

namespace ProjectS.EditorTools
{
    public class LightingTool
    {
        [MenuItem("ProjectS/Lighting/Turn Off All Lights Except Flashlight")]
        public static void TurnOffAllLights()
        {
            int disabledCount = 0;
            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            
            foreach (var light in allLights)
            {
                // Check if it's the flashlight (usually attached to the player or named Flashlight)
                bool isFlashlight = false;
                Transform parent = light.transform;
                while (parent != null)
                {
                    if (parent.name == "Player" || parent.name == "Flashlight" || parent.GetComponent<PlayerController>() != null)
                    {
                        isFlashlight = true;
                        break;
                    }
                    parent = parent.parent;
                }

                if (!isFlashlight && light.enabled)
                {
                    Undo.RecordObject(light, "Disable Light");
                    light.enabled = false;
                    disabledCount++;
                }
            }

            // Turn off ambient lighting
            Undo.RecordObject(RenderSettings.sun, "Disable Sun");
            if (RenderSettings.sun != null) RenderSettings.sun.enabled = false;
            
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            
            // Mark scene as dirty so the user can save it
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"[LightingTool] Disabled {disabledCount} environment lights and set ambient light to completely black.");
        }
    }
}
