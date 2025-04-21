using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;
using System.IO;

public class AutoObjectGenerator : EditorWindow
{
    private enum ObjectType { Small, Medium, Large, Resource }

    private static readonly Dictionary<string, int> LayerCache = new Dictionary<string, int>();
    private static readonly Dictionary<ObjectType, System.Action<AutoObjectGenerator, string>> PrefabGenerators = 
        new Dictionary<ObjectType, System.Action<AutoObjectGenerator, string>>();
    private delegate void ComponentSetup(GameObject obj);
    private static readonly Dictionary<string, ComponentSetup> ComponentSetups = new Dictionary<string, ComponentSetup>();

    private const string BASE_FOLDER = "Assets/Objects";
    private static readonly Dictionary<ObjectType, string> TypeFolders = new Dictionary<ObjectType, string>()
    {
        { ObjectType.Small, "Small" },
        { ObjectType.Medium, "Medium" },
        { ObjectType.Large, "Large" },
        { ObjectType.Resource, "Resources" }
    };

    private static readonly Dictionary<ObjectType, float[]> LODPresets = new Dictionary<ObjectType, float[]>
    {
        { ObjectType.Small, new float[] { 1f } },
        { ObjectType.Medium, new float[] { 1f } },
        { ObjectType.Large, new float[] { 1f } }
    };

    private static readonly Dictionary<ObjectType, float> DefaultCullingDistances = new Dictionary<ObjectType, float>
    {
        { ObjectType.Small, 50f },
        { ObjectType.Medium, 100f },
        { ObjectType.Large, 200f }
    };

    private static readonly Dictionary<ObjectType, float> DefaultShadowDistances = new Dictionary<ObjectType, float>
    {
        { ObjectType.Small, 25f },
        { ObjectType.Medium, 50f },
        { ObjectType.Large, 100f }
    };

    private ObjectType selectedType;
    private string objectName = "";
    private Mesh mainMesh;
    private Mesh metalMesh;
    private Mesh stumpMesh;
    private Material mainMaterial;
    private PhysicMaterial colliderMaterial;
    private List<Mesh> childMeshes = new List<Mesh>();
    private bool createSkybox;
    private Mesh skyboxMesh;
    private bool configureLOD = false;
    private List<float> lodTransitions = new List<float> { 10f };
    private bool useLODPreset = true;
    private float lodBias = 1.0f;
    private bool useCustomLODSettings = false;
    private float cullingDistance = 100f;
    private float shadowDistance = 50f;
    private SerializedObject serializedObject;
    private readonly ConcurrentDictionary<string, bool> layerExistenceCache = new ConcurrentDictionary<string, bool>();

    static AutoObjectGenerator()
    {
        InitializePrefabGenerators();
        InitializeComponentSetups();
    }

    private static void InitializePrefabGenerators()
    {
        PrefabGenerators[ObjectType.Small] = (generator, path) => generator.GenerateObjectPrefabs(path);
        PrefabGenerators[ObjectType.Medium] = (generator, path) => generator.GenerateObjectPrefabs(path);
        PrefabGenerators[ObjectType.Large] = (generator, path) => generator.GenerateObjectPrefabs(path);
        PrefabGenerators[ObjectType.Resource] = (generator, path) => generator.GenerateResourcePrefab(path);
    }

    private static void InitializeComponentSetups()
    {
        ComponentSetups["NavMesh"] = (obj) =>
        {
            obj.tag = "Navmesh";
            obj.layer = GetOrCreateLayer("Navmesh");
            var collider = obj.AddComponent<MeshCollider>();
            collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | 
                                    MeshColliderCookingOptions.EnableMeshCleaning | 
                                    MeshColliderCookingOptions.WeldColocatedVertices | 
                                    MeshColliderCookingOptions.UseFastMidphase;
            collider.sharedMesh = null;
        };

        ComponentSetups["Object"] = (obj) =>
        {
            var collider = obj.AddComponent<MeshCollider>();
            collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | 
                                    MeshColliderCookingOptions.EnableMeshCleaning | 
                                    MeshColliderCookingOptions.WeldColocatedVertices | 
                                    MeshColliderCookingOptions.UseFastMidphase;
        };
    }

    [MenuItem("Tools/Object & Resource Generator")]
    public static void ShowWindow() => GetWindow<AutoObjectGenerator>("Object & Resource Generator");

    private void OnEnable()
    {
        serializedObject = new SerializedObject(this);
        CreateFolderStructure();
    }

    private void CreateFolderStructure()
    {
        if (!AssetDatabase.IsValidFolder(BASE_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets", "Objects");
            AssetDatabase.Refresh();
        }

        foreach (var folder in TypeFolders.Values)
        {
            string fullPath = Path.Combine(BASE_FOLDER, folder).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(BASE_FOLDER, folder);
                AssetDatabase.Refresh();
            }
        }
    }

    private void OnGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.Space(10);
        DrawTypeSelection();
        EditorGUILayout.Space(10);
        DrawMainSettings();
        EditorGUILayout.Space(10);
        DrawChildModels();
        EditorGUILayout.Space(10);
        DrawOptionalFeatures();
        EditorGUILayout.Space(10);
        DrawGenerateButton();

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox($"Objects will be saved to: {TypeFolders[selectedType]}", MessageType.Info);
        EditorGUILayout.HelpBox("Due to limitations, slots for doors and windows cannot be generated for you", MessageType.Warning);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTypeSelection()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("Type");
            selectedType = (ObjectType)EditorGUILayout.EnumPopup(selectedType);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("Name");
            objectName = EditorGUILayout.TextField(objectName);
        }
    }

    private void DrawMainSettings()
    {
        mainMesh = (Mesh)EditorGUILayout.ObjectField("Main Mesh", mainMesh, typeof(Mesh), false);
        
        if (selectedType == ObjectType.Resource)
        {
            metalMesh = (Mesh)EditorGUILayout.ObjectField("Metal Mesh", metalMesh, typeof(Mesh), false);
            stumpMesh = (Mesh)EditorGUILayout.ObjectField("Stump Mesh", stumpMesh, typeof(Mesh), false);
        }
        
        mainMaterial = (Material)EditorGUILayout.ObjectField("Material", mainMaterial, typeof(Material), false);
        colliderMaterial = (PhysicMaterial)EditorGUILayout.ObjectField("Physics Material", colliderMaterial, typeof(PhysicMaterial), false);
    }

    private void DrawChildModels()
    {
        if (selectedType != ObjectType.Resource)
        {
            EditorGUILayout.LabelField("Child Models", EditorStyles.boldLabel);
            
            for (int i = 0; i < childMeshes.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    childMeshes[i] = (Mesh)EditorGUILayout.ObjectField($"Model_{i + 1}", childMeshes[i], typeof(Mesh), false);
                    if (GUILayout.Button("×", GUILayout.Width(20))) childMeshes.RemoveAt(i--);
                }
            }

            if (GUILayout.Button("Add Model")) childMeshes.Add(null);
        }
    }

    private void DrawOptionalFeatures()
    {
        switch (selectedType)
        {
            case ObjectType.Resource:
                break;
            default:
                createSkybox = EditorGUILayout.Toggle("Create Skybox", createSkybox);
                if (createSkybox)
                {
                    EditorGUI.indentLevel++;
                    skyboxMesh = (Mesh)EditorGUILayout.ObjectField("Skybox Mesh", skyboxMesh, typeof(Mesh), false);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);
                configureLOD = EditorGUILayout.Toggle("Configure LOD", configureLOD);
                if (configureLOD)
                {
                    EditorGUI.indentLevel++;
                    
                    useLODPreset = EditorGUILayout.Toggle("Use LOD Preset", useLODPreset);
                    if (useLODPreset)
                    {
                        EditorGUILayout.HelpBox($"Using default preset: LOD_0 (Model_0) with 1% transition", MessageType.Info);
                        lodBias = EditorGUILayout.Slider("LOD Bias", lodBias, 0.1f, 2f);
                        
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.FloatField("LOD_0 Transition (%)", 1f * lodBias);
                        EditorGUI.EndDisabledGroup();
                    }
                    else
                    {
                        useCustomLODSettings = EditorGUILayout.Toggle("Custom LOD Settings", useCustomLODSettings);
                        if (useCustomLODSettings)
                        {
                            cullingDistance = EditorGUILayout.FloatField("Culling Distance", cullingDistance);
                            shadowDistance = EditorGUILayout.FloatField("Shadow Distance", shadowDistance);
                        }
                        else
                        {
                            cullingDistance = DefaultCullingDistances[selectedType];
                            shadowDistance = DefaultShadowDistances[selectedType];
                        }

                        while (lodTransitions.Count < 1)
                            lodTransitions.Add(1f);

                        lodTransitions[0] = EditorGUILayout.FloatField("LOD_0 Transition (%)", lodTransitions[0]);

                        for (int i = 1; i < lodTransitions.Count; i++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            lodTransitions[i] = EditorGUILayout.FloatField($"LOD_{i} Transition (%)", lodTransitions[i]);
                            if (GUILayout.Button("×", GUILayout.Width(20)))
                            {
                                lodTransitions.RemoveAt(i);
                                i--;
                            }
                            EditorGUILayout.EndHorizontal();
                        }

                        if (GUILayout.Button("Add LOD Level"))
                        {
                            float lastTransition = lodTransitions.Count > 0 ? lodTransitions[lodTransitions.Count - 1] : 1f;
                            lodTransitions.Add(Mathf.Max(lastTransition / 2, 0.01f));
                        }
                    }

                    EditorGUI.indentLevel--;
                }
                break;
        }
    }

    private void DrawGenerateButton()
    {
        using (new EditorGUI.DisabledScope(!IsValid()))
        {
            if (GUILayout.Button("Generate"))
            {
                EditorApplication.delayCall += () => GeneratePrefabsAsync();
            }
        }
    }

    private bool IsValid()
    {
        if (mainMesh == null || mainMaterial == null) return false;
        if (selectedType == ObjectType.Resource && (metalMesh == null || stumpMesh == null)) return false;
        return true;
    }

    private void GeneratePrefabsAsync()
    {
        string typeFolder = Path.Combine(BASE_FOLDER, TypeFolders[selectedType]).Replace("\\", "/");
        string targetFolder = typeFolder;

        try
        {
            CreateFolderStructure();

            if (!string.IsNullOrEmpty(objectName))
            {
                targetFolder = Path.Combine(typeFolder, objectName).Replace("\\", "/");
                if (!AssetDatabase.IsValidFolder(targetFolder))
                {
                    AssetDatabase.CreateFolder(typeFolder, objectName);
                    AssetDatabase.Refresh();
                }
            }

            switch (selectedType)
            {
                case ObjectType.Resource:
                    GenerateResourcePrefab(targetFolder);
                    break;
                default:
                    GenerateObjectPrefabs(targetFolder);
                    break;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Prefabs generated successfully in {targetFolder}!", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error generating prefabs: {e.Message}\nPath: {targetFolder}\nStack: {e.StackTrace}");
            EditorUtility.DisplayDialog("Error", $"Failed to generate prefabs: {e.Message}", "OK");
        }
    }

    private void GenerateObjectPrefabs(string folderPath)
    {
        var navPrefab = new GameObject("Nav");
        navPrefab.tag = "Navmesh";
        navPrefab.layer = LayerMask.NameToLayer("Navmesh");
        var navCollider = navPrefab.AddComponent<MeshCollider>();
        navCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | 
                                    MeshColliderCookingOptions.EnableMeshCleaning | 
                                    MeshColliderCookingOptions.WeldColocatedVertices | 
                                    MeshColliderCookingOptions.UseFastMidphase;
        navCollider.sharedMesh = mainMesh;
        SavePrefab(navPrefab, folderPath, "Nav");

        var objectPrefab = new GameObject("Object");
        objectPrefab.tag = selectedType.ToString();
        objectPrefab.layer = LayerMask.NameToLayer(selectedType.ToString());
        
        var objCollider = objectPrefab.AddComponent<MeshCollider>();
        objCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | 
                                    MeshColliderCookingOptions.EnableMeshCleaning | 
                                    MeshColliderCookingOptions.WeldColocatedVertices | 
                                    MeshColliderCookingOptions.UseFastMidphase;
        objCollider.sharedMesh = mainMesh;
        objCollider.sharedMaterial = colliderMaterial;

        SetupLODGroup(objectPrefab);

        SavePrefab(objectPrefab, folderPath, "Object");

        if (createSkybox)
        {
            var skyboxPrefab = new GameObject("Skybox");
            skyboxPrefab.tag = selectedType.ToString();
            skyboxPrefab.layer = LayerMask.NameToLayer(selectedType.ToString());

            var model = new GameObject("Model_0");
            model.transform.SetParent(skyboxPrefab.transform);
            model.tag = skyboxPrefab.tag;
            model.layer = skyboxPrefab.layer;
            
            var meshFilter = model.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = skyboxMesh != null ? skyboxMesh : mainMesh;
            
            var meshRenderer = model.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = mainMaterial;

            SavePrefab(skyboxPrefab, folderPath, "Skybox");
        }
    }

    private void GenerateResourcePrefab(string folderPath)
    {
        var prefab = new GameObject("Resource");
        prefab.tag = "Resource";
        prefab.layer = LayerMask.NameToLayer("Resource");

        var collider = prefab.AddComponent<MeshCollider>();
        collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | 
                                 MeshColliderCookingOptions.EnableMeshCleaning | 
                                 MeshColliderCookingOptions.WeldColocatedVertices | 
                                 MeshColliderCookingOptions.UseFastMidphase;
        collider.sharedMesh = mainMesh;
        collider.sharedMaterial = colliderMaterial;

        var model0Container = new GameObject("Model_0");
        model0Container.transform.SetParent(prefab.transform);
        model0Container.tag = prefab.tag;
        model0Container.layer = prefab.layer;

        var metal0 = new GameObject("Metal_0");
        metal0.transform.SetParent(model0Container.transform);
        metal0.tag = prefab.tag;
        metal0.layer = prefab.layer;
        var metalMeshFilter = metal0.AddComponent<MeshFilter>();
        metalMeshFilter.sharedMesh = metalMesh != null ? metalMesh : mainMesh;
        var metalMeshRenderer = metal0.AddComponent<MeshRenderer>();
        metalMeshRenderer.sharedMaterial = mainMaterial;

        var model0 = new GameObject("Model_0");
        model0.transform.SetParent(model0Container.transform);
        model0.tag = prefab.tag;
        model0.layer = prefab.layer;
        var meshFilter = model0.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mainMesh;
        var meshRenderer = model0.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = mainMaterial;

        var lodGroup = prefab.AddComponent<LODGroup>();
        LOD[] lods = new LOD[1];
        
        lods[0] = new LOD(0.01f, new[] { metalMeshRenderer, meshRenderer });
        
        lodGroup.SetLODs(lods);
        lodGroup.fadeMode = LODFadeMode.None;
        lodGroup.RecalculateBounds();

        SavePrefab(prefab, folderPath, "Resource");

        GenerateStumpPrefab(folderPath);
    }

    private void GenerateStumpPrefab(string folderPath)
    {
        var prefab = new GameObject("Stump");
        prefab.tag = "Resource";
        prefab.layer = LayerMask.NameToLayer("Resource");

        var collider = prefab.AddComponent<MeshCollider>();
        collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | 
                                 MeshColliderCookingOptions.EnableMeshCleaning | 
                                 MeshColliderCookingOptions.WeldColocatedVertices | 
                                 MeshColliderCookingOptions.UseFastMidphase;
        collider.sharedMesh = stumpMesh;
        collider.sharedMaterial = colliderMaterial;

        var model0Container = new GameObject("Model_0");
        model0Container.transform.SetParent(prefab.transform);
        model0Container.tag = prefab.tag;
        model0Container.layer = prefab.layer;

        var model0 = new GameObject("Model_0");
        model0.transform.SetParent(model0Container.transform);
        model0.tag = prefab.tag;
        model0.layer = prefab.layer;
        var meshFilter = model0.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = stumpMesh;
        var meshRenderer = model0.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = mainMaterial;

        var lodGroup = prefab.AddComponent<LODGroup>();
        LOD[] lods = new LOD[1];
        
        lods[0] = new LOD(0.01f, new[] { meshRenderer });
        
        lodGroup.SetLODs(lods);
        lodGroup.fadeMode = LODFadeMode.None;
        lodGroup.RecalculateBounds();

        SavePrefab(prefab, folderPath, "Stump");
    }

    private void SetupLODGroup(GameObject parent)
    {
        var lodGroup = parent.AddComponent<LODGroup>();
        
        var model0 = new GameObject("Model_0");
        model0.transform.SetParent(parent.transform);
        model0.tag = parent.tag;
        model0.layer = parent.layer;

        var meshFilter = model0.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mainMesh;
        var meshRenderer = model0.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = mainMaterial;

        var lods = new List<LOD>();
        var renderers = new Dictionary<int, Renderer>();
        renderers[0] = meshRenderer;

        if (configureLOD)
        {
            List<float> transitions;
            if (useLODPreset)
            {
                transitions = LODPresets[selectedType]
                    .Select(t => Mathf.Clamp(t * lodBias, 0.01f, 100f))
                    .OrderByDescending(t => t)
                    .ToList();
            }
            else
            {
                transitions = lodTransitions.OrderByDescending(t => t).ToList();
            }

            for (int i = 0; i < transitions.Count - 1; i++)
            {
                if (i < childMeshes.Count && childMeshes[i] != null)
                {
                    var childModel = new GameObject($"Model_{i + 1}");
                    childModel.transform.SetParent(parent.transform);
                    childModel.tag = parent.tag;
                    childModel.layer = parent.layer;

                    var childMeshFilter = childModel.AddComponent<MeshFilter>();
                    childMeshFilter.sharedMesh = childMeshes[i];
                    var childRenderer = childModel.AddComponent<MeshRenderer>();
                    childRenderer.sharedMaterial = mainMaterial;
                    
                    childRenderer.shadowCastingMode = (i + 1) <= 2 ? 
                        UnityEngine.Rendering.ShadowCastingMode.On : 
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    
                    childRenderer.receiveShadows = (i + 1) <= 2;
                    renderers[i + 1] = childRenderer;
                }
            }

            for (int i = 0; i < transitions.Count; i++)
            {
                var currentRenderers = renderers.TryGetValue(i, out var renderer) 
                    ? new[] { renderer } 
                    : new Renderer[0];
                    
                if (currentRenderers.Length > 0)
                {
                    foreach (var r in currentRenderers)
                    {
                        r.shadowCastingMode = i <= 2 ? 
                            UnityEngine.Rendering.ShadowCastingMode.On : 
                            UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = i <= 2;
                    }
                }
                
                lods.Add(new LOD(transitions[i] / 100f, currentRenderers));
            }
        }
        else
        {
            lods.Add(new LOD(0.1f, new[] { meshRenderer }));
        }

        lods.Add(new LOD(0f, new Renderer[0]));
        
        lodGroup.SetLODs(lods.ToArray());
        lodGroup.fadeMode = LODFadeMode.None;
        
        float actualCullingDistance = useCustomLODSettings ? 
            cullingDistance : 
            DefaultCullingDistances[selectedType];
        lodGroup.size = actualCullingDistance;
        
        lodGroup.RecalculateBounds();
    }

    private void SavePrefab(GameObject prefab, string folderPath, string prefabName)
    {
        string prefabPath = Path.Combine(folderPath, $"{prefabName}.prefab").Replace("\\", "/");
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        if (savedPrefab == null)
        {
            throw new System.Exception($"Failed to save prefab at path: {prefabPath}");
        }
        DestroyImmediate(prefab);
    }

    private static int GetOrCreateLayer(string layerName)
    {
        if (LayerCache.TryGetValue(layerName, out int layer)) return layer;
        
        layer = LayerMask.NameToLayer(layerName);
        LayerCache[layerName] = layer;
        return layer;
    }
}
    