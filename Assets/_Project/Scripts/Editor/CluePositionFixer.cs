using UnityEditor;
using UnityEngine;
using Room0317.Story;

namespace Room0317.Editor
{
    [InitializeOnLoad]
    public static class CluePositionFixer
    {
        static CluePositionFixer()
        {
            EditorApplication.delayCall += ApplyClueFixesInEditor;
        }

        [MenuItem("ROOM 03:17/Fix Bathroom Key & Clue Positions")]
        public static void ApplyClueFixesInEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                return;
            }

            int fixedCount = 0;

            // 1. Bathroom Key
            var allKeys = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allKeys)
            {
                if (go == null || EditorUtility.IsPersistent(go)) continue;

                if (go.name == "Clue_BathroomKey")
                {
                    Undo.RecordObject(go.transform, "Fix Bathroom Key Transform");
                    go.transform.localPosition = new Vector3(-1.35f, 0.65f, 0f);
                    go.transform.localScale = new Vector3(0.065f, 0.065f, 0.065f);

                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Undo.RecordObject(sr, "Fix Bathroom Key SpriteRenderer");
                        sr.sortingLayerName = "Props_Back";
                        sr.sortingOrder = 10;
                    }

                    var trigger = go.GetComponent<StoryInteractableTrigger>();
                    if (trigger != null)
                    {
                        var so = new SerializedObject(trigger);
                        var radiusProp = so.FindProperty("radius");
                        if (radiusProp != null)
                        {
                            radiusProp.floatValue = 2.4f;
                            so.ApplyModifiedProperties();
                        }
                    }

                    EditorUtility.SetDirty(go);
                    fixedCount++;
                }

                if (go.name == "Clue_SecondChair")
                {
                    Undo.RecordObject(go.transform, "Fix Second Chair Transform");
                    go.transform.localPosition = new Vector3(0.55f, -1.85f, 0f);
                    go.transform.localScale = Vector3.one;

                    // Remove physical in-world SpriteRenderer: the chair is part of eerie room art (eerie_kitchen.png)
                    var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
                    foreach (var s in srs)
                    {
                        if (s != null) Undo.DestroyObjectImmediate(s);
                    }

                    var trigger = go.GetComponent<StoryInteractableTrigger>();
                    if (trigger != null)
                    {
                        var so = new SerializedObject(trigger);
                        var radiusProp = so.FindProperty("radius");
                        if (radiusProp != null)
                        {
                            radiusProp.floatValue = 2.4f;
                            so.ApplyModifiedProperties();
                        }
                    }

                    EditorUtility.SetDirty(go);
                    fixedCount++;
                }

                if (go.name == "Clue_KitchenNote")
                {
                    Undo.RecordObject(go.transform, "Fix Kitchen Note Transform");
                    go.transform.localPosition = new Vector3(-2.4f, 1.75f, 0f);
                    go.transform.localScale = new Vector3(0.035f, 0.035f, 0.035f);

                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Undo.RecordObject(sr, "Fix Kitchen Note SpriteRenderer");
                        sr.sortingLayerName = "Props_Back";
                        sr.sortingOrder = 10;
                    }

                    var trigger = go.GetComponent<StoryInteractableTrigger>();
                    if (trigger != null)
                    {
                        var so = new SerializedObject(trigger);
                        var radiusProp = so.FindProperty("radius");
                        if (radiusProp != null)
                        {
                            radiusProp.floatValue = 2.4f;
                            so.ApplyModifiedProperties();
                        }
                    }

                    EditorUtility.SetDirty(go);
                    fixedCount++;
                }
            }

            // 4. Ghost Girl Portraits on DialogueBox
            var dialogueBoxes = Resources.FindObjectsOfTypeAll<Room0317.Dialogue.DialogueBox>();
            foreach (var db in dialogueBoxes)
            {
                if (db == null || EditorUtility.IsPersistent(db)) continue;
                Undo.RecordObject(db, "Bind Ghost Girl Portraits");
                db.LoadGhostGirlPortraits();
                EditorUtility.SetDirty(db);
                fixedCount++;
            }

            // 5. Bind Ingame Noise.wav backsound onto ApartmentManager / AudioManager
            var ingameNoiseClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/Ingame Noise.wav");
            if (ingameNoiseClip != null)
            {
                var aptManagers = Resources.FindObjectsOfTypeAll<Room0317.Apartment.ApartmentManager>();
                foreach (var mgr in aptManagers)
                {
                    if (mgr == null || EditorUtility.IsPersistent(mgr)) continue;
                    var so = new SerializedObject(mgr);
                    var clipProp = so.FindProperty("mainIngameBacksound");
                    if (clipProp != null && clipProp.objectReferenceValue == null)
                    {
                        clipProp.objectReferenceValue = ingameNoiseClip;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(mgr);
                        fixedCount++;
                    }
                }

                var audioManagers = Resources.FindObjectsOfTypeAll<Room0317.Audio.AudioManager>();
                foreach (var am in audioManagers)
                {
                    if (am == null || EditorUtility.IsPersistent(am)) continue;
                    var so = new SerializedObject(am);
                    var droneProp = so.FindProperty("customAmbientDrone");
                    if (droneProp != null && droneProp.objectReferenceValue == null)
                    {
                        droneProp.objectReferenceValue = ingameNoiseClip;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(am);
                        fixedCount++;
                    }
                }
            }

            // 6. Ensure Main Camera has an AudioListener in Apartment scenes
            if (Object.FindFirstObjectByType<AudioListener>() == null)
            {
                var mainCam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
                if (mainCam != null && !EditorUtility.IsPersistent(mainCam))
                {
                    Undo.AddComponent<AudioListener>(mainCam.gameObject);
                    EditorUtility.SetDirty(mainCam.gameObject);
                    fixedCount++;
                }
            }

            if (fixedCount > 0)
            {
                if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                    if (activeScene.isLoaded && !activeScene.isDirty)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                    }
                }
                Debug.Log($"<color=green><b>[ROOM 03:17]</b> Successfully aligned {fixedCount} clue objects (Bathroom Key on Sink, Kitchen Note on Counter)!</color>");
            }
        }
    }
}
