using System;
using System.IO;
using GateballBall = Pm.Booth.Aoinu607.Udon.Gateball.GateballBall;
using GateballBoundary = Pm.Booth.Aoinu607.Udon.Gateball.GateballBoundary;
using GateballCourt = Pm.Booth.Aoinu607.Udon.Gateball.GateballCourt;
using GateballDesktopController = Pm.Booth.Aoinu607.Udon.Gateball.GateballDesktopController;
using GateballGate = Pm.Booth.Aoinu607.Udon.Gateball.GateballGate;
using GateballGatePost = Pm.Booth.Aoinu607.Udon.Gateball.GateballGatePost;
using GateballGeometry = Pm.Booth.Aoinu607.Udon.Gateball.GateballGeometry;
using GateballGoalPole = Pm.Booth.Aoinu607.Udon.Gateball.GateballGoalPole;
using GateballMallet = Pm.Booth.Aoinu607.Udon.Gateball.GateballMallet;
using GateballStrokeRouter = Pm.Booth.Aoinu607.Udon.Gateball.GateballStrokeRouter;
using GateballTestShotController = Pm.Booth.Aoinu607.Udon.Gateball.GateballTestShotController;
using UdonSharpEditor;
using UdonSharp;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRCPickup = VRC.SDK3.Components.VRCPickup;
using VRCSceneDescriptor = VRC.SDK3.Components.VRCSceneDescriptor;

public static class GateballDevelopmentSceneBuilder
{
    private const string RootName = "GateballDevelopment";
    private const string RuntimeFolder = "Packages/pm.booth.aoinu607.udon.gateball/Runtime";
    private const string MaterialFolder = "Assets/Aoinu Works/Gateball/Materials";
    private const string SceneFolder = "Assets/Aoinu Works/Gateball/Scenes";
    private const int EnvironmentLayer = 11;
    private const int PickupLayer = 13;

    [MenuItem("Aoinu Gateball/Create Udon Program Assets")]
    public static void CreateUdonProgramAssets()
    {
        CreateUdonAssemblyDefinition();
        string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { RuntimeFolder });
        int createdCount = 0;
        for (int i = 0; i < scriptGuids.Length; i++)
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            Type scriptClass = script == null ? null : script.GetClass();
            if (scriptClass == null
                || scriptClass.IsAbstract
                || !typeof(UdonSharpBehaviour).IsAssignableFrom(scriptClass))
            {
                continue;
            }

            if (UdonSharpEditorUtility.GetUdonSharpProgramAsset(scriptClass) != null)
            {
                continue;
            }

            string programAssetPath = Path.ChangeExtension(scriptPath, ".asset").Replace('\\', '/');
            if (AssetDatabase.LoadMainAssetAtPath(programAssetPath) != null)
            {
                continue;
            }

            UdonSharpProgramAsset programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            programAsset.sourceCsScript = script;
            AssetDatabase.CreateAsset(programAsset, programAssetPath);
            createdCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GateballDevelopmentSceneBuilder] Created " + createdCount + " UdonSharp Program Assets.");
    }

    private static void CreateUdonAssemblyDefinition()
    {
        const string unityAssemblyPath = RuntimeFolder + "/Pm.Booth.Aoinu607.Udon.Gateball.asmdef";
        const string udonAssemblyPath = RuntimeFolder + "/Pm.Booth.Aoinu607.Udon.Gateball.asmdef.asset";
        AssemblyDefinitionAsset sourceAssembly = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(unityAssemblyPath);
        if (sourceAssembly == null || AssetDatabase.LoadAssetAtPath<UdonSharpAssemblyDefinition>(udonAssemblyPath) != null)
        {
            return;
        }

        UdonSharpAssemblyDefinition udonAssembly = ScriptableObject.CreateInstance<UdonSharpAssemblyDefinition>();
        udonAssembly.sourceAssembly = sourceAssembly;
        AssetDatabase.CreateAsset(udonAssembly, udonAssemblyPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Aoinu Gateball/Build Development Scene")]
    public static void BuildDevelopmentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
        {
            Undo.DestroyObjectImmediate(oldRoot);
        }

        GameObject legacyFloor = GameObject.Find("Floor");
        if (legacyFloor != null && legacyFloor.transform.parent == null)
        {
            legacyFloor.SetActive(false);
            EditorUtility.SetDirty(legacyFloor);
        }

        EnsureFolder("Assets/Aoinu Works");
        EnsureFolder("Assets/Aoinu Works/Gateball");
        EnsureFolder(MaterialFolder);
        EnsureFolder(SceneFolder);

        Material floorMaterial = GetOrCreateMaterial("CourtFloor", new Color(0.20f, 0.42f, 0.18f));
        Material boundaryMaterial = GetOrCreateMaterial("Boundary", new Color(0.16f, 0.16f, 0.16f));
        Material gateMaterial = GetOrCreateMaterial("Gate", new Color(0.95f, 0.68f, 0.10f));
        Material poleMaterial = GetOrCreateMaterial("GoalPole", new Color(0.85f, 0.12f, 0.08f));
        Material redMaterial = GetOrCreateMaterial("BallRed", new Color(0.80f, 0.08f, 0.06f));
        Material whiteMaterial = GetOrCreateMaterial("BallWhite", new Color(0.92f, 0.92f, 0.92f));
        Material toolMaterial = GetOrCreateMaterial("DevelopmentTool", new Color(0.20f, 0.45f, 0.85f));

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Gateball Development Scene");

        GameObject courtObject = new GameObject("Court");
        courtObject.transform.SetParent(root.transform, false);
        GateballCourt court = courtObject.AddUdonSharpComponent<GateballCourt>();
        court.CourtWidth = 15f;
        court.CourtLength = 20f;
        court.OutMargin = 0.35f;

        CreatePrimitive("CourtFloor", PrimitiveType.Cube, courtObject.transform, new Vector3(0f, -0.10f, 0f), new Vector3(15f, 0.20f, 20f), floorMaterial, EnvironmentLayer);
        CreateBoundary(courtObject.transform, "BoundaryWest", new Vector3(-7.65f, 0.20f, 0f), new Vector3(0.30f, 0.60f, 20.60f), boundaryMaterial);
        CreateBoundary(courtObject.transform, "BoundaryEast", new Vector3(7.65f, 0.20f, 0f), new Vector3(0.30f, 0.60f, 20.60f), boundaryMaterial);
        CreateBoundary(courtObject.transform, "BoundarySouth", new Vector3(0f, 0.20f, -10.15f), new Vector3(15.60f, 0.60f, 0.30f), boundaryMaterial);
        CreateBoundary(courtObject.transform, "BoundaryNorth", new Vector3(0f, 0.20f, 10.15f), new Vector3(15.60f, 0.60f, 0.30f), boundaryMaterial);

        GateballGate[] gates = new GateballGate[3];
        for (int i = 0; i < gates.Length; i++)
        {
            gates[i] = CreateGate(courtObject.transform, i, new Vector3(0f, 0f, -4f + i * 4f), gateMaterial);
        }

        GameObject poleObject = CreatePrimitive("GoalPole", PrimitiveType.Cylinder, courtObject.transform, new Vector3(0f, 0.65f, 8.7f), new Vector3(0.16f, 0.65f, 0.16f), poleMaterial, EnvironmentLayer);
        GateballGoalPole goalPole = poleObject.AddUdonSharpComponent<GateballGoalPole>();
        goalPole.Radius = 0.08f;
        goalPole.Height = 1.3f;
        court.GoalPole = goalPole;

        GameObject ballsRoot = new GameObject("Balls");
        ballsRoot.transform.SetParent(courtObject.transform, false);
        GateballBall[] balls = new GateballBall[GateballGeometry.BallCount];
        for (int i = 0; i < balls.Length; i++)
        {
            int ballId = i + 1;
            float x = (i % 5 - 2) * 0.65f;
            float z = -7.0f + (i / 5) * 0.70f;
            GameObject ballObject = CreatePrimitive(
                "Ball" + ballId.ToString("00"),
                PrimitiveType.Sphere,
                ballsRoot.transform,
                new Vector3(x, 0.16f, z),
                Vector3.one * 0.32f,
                ballId % 2 == 1 ? redMaterial : whiteMaterial,
                PickupLayer);

            SphereCollider sphereCollider = ballObject.GetComponent<SphereCollider>();
            sphereCollider.material = GetOrCreatePhysicsMaterial();
            Rigidbody body = ballObject.AddComponent<Rigidbody>();
            body.mass = 0.45f;
            body.drag = 0.20f;
            body.angularDrag = 0.05f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.useGravity = true;

            GateballBall ball = ballObject.AddUdonSharpComponent<GateballBall>();
            ball.BallId = ballId;
            ball.Court = court;
            ball.Body = body;
            balls[i] = ball;
        }

        GateballStrokeRouter router = CreateUdonObject<GateballStrokeRouter>(root.transform, "StrokeRouter");
        router.Court = court;
        router.MaximumImpulse = 7f;

        GameObject malletObject = CreatePrimitive("VRMallet", PrimitiveType.Cylinder, root.transform, new Vector3(3f, 1.3f, -7f), new Vector3(0.12f, 0.75f, 0.12f), toolMaterial, PickupLayer);
        Collider malletCollider = malletObject.GetComponent<Collider>();
        malletCollider.isTrigger = true;
        Rigidbody malletBody = malletObject.AddComponent<Rigidbody>();
        malletBody.useGravity = false;
        malletBody.isKinematic = false;
        VRCPickup pickup = malletObject.AddComponent<VRCPickup>();
        pickup.pickupable = true;
        pickup.proximity = 3f;

        GameObject headObject = CreatePrimitive("Head", PrimitiveType.Cube, malletObject.transform, new Vector3(0f, -0.75f, 0f), new Vector3(0.65f, 0.18f, 0.22f), toolMaterial, PickupLayer);
        headObject.GetComponent<Collider>().isTrigger = true;
        GameObject proxyObject = new GameObject("PhysicsProxy");
        proxyObject.transform.SetParent(malletObject.transform, false);
        proxyObject.transform.localPosition = new Vector3(0f, -0.75f, 0f);
        proxyObject.transform.localScale = Vector3.one * 0.18f;
        SphereCollider proxyCollider = proxyObject.AddComponent<SphereCollider>();
        proxyCollider.isTrigger = true;
        GateballMallet mallet = malletObject.AddUdonSharpComponent<GateballMallet>();
        mallet.StrokeRouter = router;
        mallet.Head = headObject.transform;
        mallet.PhysicsProxy = proxyCollider;
        mallet.ProxyRadius = 0.09f;
        mallet.BallLayer = PickupLayer;
        mallet.StrikeScale = 0.45f;
        mallet.MaximumImpulse = 7f;

        GameObject desktopObject = CreatePrimitive("DesktopControls", PrimitiveType.Cube, root.transform, new Vector3(-4.0f, 0.45f, -8.5f), new Vector3(1.6f, 0.8f, 0.25f), toolMaterial, 0);
        GateballDesktopController desktop = desktopObject.AddUdonSharpComponent<GateballDesktopController>();
        desktop.StrokeRouter = router;
        desktop.SelectedBallId = 1;
        desktop.Power = 2.5f;

        GameObject testShotObject = CreatePrimitive("TestShotController", PrimitiveType.Cube, root.transform, new Vector3(4.0f, 0.45f, -8.5f), new Vector3(1.6f, 0.8f, 0.25f), toolMaterial, 0);
        GateballTestShotController testShot = testShotObject.AddUdonSharpComponent<GateballTestShotController>();
        testShot.Court = court;
        testShot.StrokeRouter = router;

        court.Balls = balls;
        court.Gates = gates;
        EditorUtility.SetDirty(court);
        EditorUtility.SetDirty(router);
        EditorUtility.SetDirty(desktop);
        EditorUtility.SetDirty(testShot);

        CreateSpawns(root.transform);
        ConfigureSceneDescriptor();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[GateballDevelopmentSceneBuilder] Development scene built with 10 balls, 3 gates, 1 goal pole, and VR/Desktop controls.");
    }

    private static GateballGate CreateGate(Transform parent, int gateIndex, Vector3 position, Material material)
    {
        GameObject gateObject = new GameObject("Gate" + (gateIndex + 1).ToString());
        gateObject.transform.SetParent(parent, false);
        gateObject.transform.localPosition = position;
        GateballGate gate = gateObject.AddUdonSharpComponent<GateballGate>();
        gate.GateIndex = gateIndex;
        gate.OpeningWidth = 1.2f;
        gate.OpeningHeight = 0.45f;
        gate.BallRadius = 0.12f;

        CreateGatePost(gateObject.transform, "LeftPost", gateIndex, new Vector3(-0.66f, 0.22f, 0f), material);
        CreateGatePost(gateObject.transform, "RightPost", gateIndex, new Vector3(0.66f, 0.22f, 0f), material);
        CreatePrimitive("TopBar", PrimitiveType.Cube, gateObject.transform, new Vector3(0f, 0.46f, 0f), new Vector3(1.45f, 0.08f, 0.10f), material, EnvironmentLayer);
        return gate;
    }

    private static void CreateGatePost(Transform parent, string name, int gateIndex, Vector3 position, Material material)
    {
        GameObject post = CreatePrimitive(name, PrimitiveType.Cylinder, parent, position, new Vector3(0.14f, 0.22f, 0.14f), material, EnvironmentLayer);
        GateballGatePost gatePost = post.AddUdonSharpComponent<GateballGatePost>();
        gatePost.GateIndex = gateIndex;
    }

    private static void CreateBoundary(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject boundary = CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, material, EnvironmentLayer);
        boundary.AddUdonSharpComponent<GateballBoundary>();
    }

    private static T CreateUdonObject<T>(Transform parent, string name) where T : UdonSharp.UdonSharpBehaviour
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject.AddUdonSharpComponent<T>();
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, int layer)
    {
        GameObject gameObject = GameObject.CreatePrimitive(type);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localScale = scale;
        gameObject.layer = layer;
        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        return gameObject;
    }

    private static void CreateSpawns(Transform parent)
    {
        Vector3[] positions =
        {
            new Vector3(-3f, 0.1f, -8.5f),
            new Vector3(3f, 0.1f, -8.5f),
            new Vector3(-3f, 0.1f, 8.5f),
            new Vector3(3f, 0.1f, 8.5f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject spawn = new GameObject("Spawn" + (i + 1).ToString());
            spawn.transform.SetParent(parent, false);
            spawn.transform.localPosition = positions[i];
            spawn.transform.localRotation = Quaternion.LookRotation(-spawn.transform.localPosition.normalized, Vector3.up);
        }
    }

    private static void ConfigureSceneDescriptor()
    {
        GameObject world = GameObject.Find("VRCWorld");
        GameObject mainCamera = GameObject.Find("Main Camera");
        if (world == null)
        {
            return;
        }

        VRCSceneDescriptor descriptor = world.GetComponent<VRCSceneDescriptor>();
        if (descriptor == null)
        {
            return;
        }

        SerializedObject serializedDescriptor = new SerializedObject(descriptor);
        SerializedProperty spawns = serializedDescriptor.FindProperty("spawns");
        GameObject root = GameObject.Find(RootName);
        if (spawns != null && root != null)
        {
            Transform[] spawnTransforms = root.GetComponentsInChildren<Transform>(true);
            int spawnCount = 0;
            for (int i = 0; i < spawnTransforms.Length; i++)
            {
                if (spawnTransforms[i].name.StartsWith("Spawn", StringComparison.Ordinal))
                {
                    spawnCount++;
                }
            }

            spawns.arraySize = spawnCount;
            int spawnIndex = 0;
            for (int i = 0; i < spawnTransforms.Length; i++)
            {
                if (!spawnTransforms[i].name.StartsWith("Spawn", StringComparison.Ordinal))
                {
                    continue;
                }

                spawns.GetArrayElementAtIndex(spawnIndex).objectReferenceValue = spawnTransforms[i];
                spawnIndex++;
            }
        }

        SetFloat(serializedDescriptor, "RespawnHeightY", -100f);
        SetInt(serializedDescriptor, "capacity", 8);
        SetEnum(serializedDescriptor, "spawnOrder", 1);
        if (mainCamera != null)
        {
            SetObject(serializedDescriptor, "ReferenceCamera", mainCamera);
        }

        serializedDescriptor.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(descriptor);
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetEnum(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.enumValueIndex = value;
        }
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static PhysicMaterial GetOrCreatePhysicsMaterial()
    {
        const string path = MaterialFolder + "/GateballBallPhysics.physicMaterial";
        PhysicMaterial material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
        if (material == null)
        {
            material = new PhysicMaterial("GateballBallPhysics");
            AssetDatabase.CreateAsset(material, path);
        }

        material.dynamicFriction = 0.35f;
        material.staticFriction = 0.35f;
        material.bounciness = 0.05f;
        material.frictionCombine = PhysicMaterialCombine.Multiply;
        material.bounceCombine = PhysicMaterialCombine.Minimum;
        EditorUtility.SetDirty(material);
        return material;
    }
}
