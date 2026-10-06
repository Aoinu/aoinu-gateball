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
using GateballMalletGrip = Pm.Booth.Aoinu607.Udon.Gateball.GateballMalletGrip;
using GateballDebugDisplay = Pm.Booth.Aoinu607.Udon.Gateball.GateballDebugDisplay;
using GateballNetworkState = Pm.Booth.Aoinu607.Udon.Gateball.GateballNetworkState;
using GateballStrokeRouter = Pm.Booth.Aoinu607.Udon.Gateball.GateballStrokeRouter;
using GateballTestShotController = Pm.Booth.Aoinu607.Udon.Gateball.GateballTestShotController;
using GateballTelemetry = Pm.Booth.Aoinu607.Udon.Gateball.GateballTelemetry;
using UdonSharpEditor;
using UdonSharp;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRCPickup = VRC.SDK3.Components.VRCPickup;
using VRCObjectSync = VRC.SDK3.Components.VRCObjectSync;
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
        CreateUdonProgramAssets();
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
        court.CourtWidth = GateballGeometry.CourtWidth;
        court.CourtLength = GateballGeometry.CourtLength;
        court.OutMargin = GateballGeometry.DefaultOutMargin;

        GateballTelemetry telemetry = CreateUdonObject<GateballTelemetry>(root.transform, "Telemetry");
        GateballNetworkState networkState = CreateUdonObject<GateballNetworkState>(root.transform, "NetworkState");
        networkState.Court = court;
        networkState.Telemetry = telemetry;
        court.Telemetry = telemetry;

        CreatePrimitive("CourtFloor", PrimitiveType.Cube, courtObject.transform, new Vector3(0f, -0.10f, 0f), new Vector3(GateballGeometry.CourtWidth, 0.20f, GateballGeometry.CourtLength), floorMaterial, EnvironmentLayer);
        float halfCourtWidth = GateballGeometry.CourtWidth * 0.5f;
        float halfCourtLength = GateballGeometry.CourtLength * 0.5f;
        CreateBoundary(courtObject.transform, "BoundaryWest", new Vector3(-halfCourtWidth - 0.01f, 0.10f, 0f), new Vector3(0.02f, 0.20f, GateballGeometry.CourtLength + 0.02f), boundaryMaterial);
        CreateBoundary(courtObject.transform, "BoundaryEast", new Vector3(halfCourtWidth + 0.01f, 0.10f, 0f), new Vector3(0.02f, 0.20f, GateballGeometry.CourtLength + 0.02f), boundaryMaterial);
        CreateBoundary(courtObject.transform, "BoundarySouth", new Vector3(0f, 0.10f, -halfCourtLength - 0.01f), new Vector3(GateballGeometry.CourtWidth + 0.02f, 0.20f, 0.02f), boundaryMaterial);
        CreateBoundary(courtObject.transform, "BoundaryNorth", new Vector3(0f, 0.10f, halfCourtLength + 0.01f), new Vector3(GateballGeometry.CourtWidth + 0.02f, 0.20f, 0.02f), boundaryMaterial);

        GateballGate[] gates = new GateballGate[3];
        for (int i = 0; i < gates.Length; i++)
        {
            gates[i] = CreateGate(courtObject.transform, i, new Vector3(0f, 0f, -4f + i * 4f), gateMaterial);
        }

        GameObject poleObject = CreatePrimitive("GoalPole", PrimitiveType.Cylinder, courtObject.transform, new Vector3(0f, GateballGeometry.GoalPoleHeight * 0.5f, 8.7f), new Vector3(GateballGeometry.GoalPoleDiameter, GateballGeometry.GoalPoleHeight * 0.5f, GateballGeometry.GoalPoleDiameter), poleMaterial, EnvironmentLayer);
        GateballGoalPole goalPole = poleObject.AddUdonSharpComponent<GateballGoalPole>();
        goalPole.Radius = GateballGeometry.GoalPoleRadius;
        goalPole.Height = GateballGeometry.GoalPoleHeight;
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
                new Vector3(x, GateballGeometry.BallRadius, z),
                Vector3.one * GateballGeometry.BallDiameter,
                ballId % 2 == 1 ? redMaterial : whiteMaterial,
                PickupLayer);

            SphereCollider sphereCollider = ballObject.GetComponent<SphereCollider>();
            sphereCollider.material = GetOrCreatePhysicsMaterial();
            Rigidbody body = ballObject.AddComponent<Rigidbody>();
            body.mass = GateballGeometry.BallMass;
            body.drag = 0.20f;
            body.angularDrag = 0.05f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.useGravity = true;

            GateballBall ball = ballObject.AddUdonSharpComponent<GateballBall>();
            ball.BallId = ballId;
            ball.Court = court;
            ball.Body = body;
            ball.Radius = GateballGeometry.BallRadius;
            balls[i] = ball;
        }

        GateballStrokeRouter router = CreateUdonObject<GateballStrokeRouter>(root.transform, "StrokeRouter");
        router.Court = court;
        router.NetworkState = networkState;
        router.MaximumImpulse = GateballGeometry.StrongStrokeImpulse;

        GameObject malletObject = new GameObject("MalletController");
        malletObject.transform.SetParent(root.transform, false);
        GateballMallet mallet = malletObject.AddUdonSharpComponent<GateballMallet>();
        mallet.StrokeRouter = router;

        GameObject rigObject = new GameObject("MalletRig");
        rigObject.transform.SetParent(root.transform, false);
        rigObject.transform.SetPositionAndRotation(new Vector3(1.3f, 0.9f, -6.82f), Quaternion.identity);
        Rigidbody rigBody = rigObject.AddComponent<Rigidbody>();
        rigBody.useGravity = false;
        rigBody.isKinematic = true;
        rigObject.AddComponent<VRCObjectSync>();

        GameObject shaftObject = CreatePrimitive(
            "Shaft",
            PrimitiveType.Cylinder,
            rigObject.transform,
            new Vector3(0f, 0.16f, 0f),
            new Vector3(0.035f, 0.52f, 0.035f),
            toolMaterial,
            PickupLayer);
        UnityEngine.Object.DestroyImmediate(shaftObject.GetComponent<Collider>());
        GameObject headObject = CreatePrimitive(
            "Head",
            PrimitiveType.Cube,
            rigObject.transform,
            new Vector3(0f, -0.75f, 0f),
            new Vector3(0.65f, 0.18f, 0.22f),
            toolMaterial,
            PickupLayer);
        BoxCollider headCollider = headObject.GetComponent<BoxCollider>();
        headCollider.isTrigger = true;
        GameObject strikeFaceObject = new GameObject("StrikeFace");
        strikeFaceObject.transform.SetParent(headObject.transform, false);
        strikeFaceObject.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        strikeFaceObject.transform.localRotation = Quaternion.identity;

        GameObject gripAObject = CreatePickupGrip(root.transform, "GripA", 0, mallet);
        GameObject gripBObject = CreatePickupGrip(root.transform, "GripB", 1, mallet);
        gripAObject.transform.SetPositionAndRotation(
            rigObject.transform.TransformPoint(new Vector3(0f, 0.55f, 0f)), rigObject.transform.rotation);
        gripBObject.transform.SetPositionAndRotation(
            rigObject.transform.TransformPoint(new Vector3(0f, -0.15f, 0f)), rigObject.transform.rotation);

        GameObject lineObject = new GameObject("AimPreview");
        lineObject.transform.SetParent(rigObject.transform, false);
        LineRenderer aimLine = lineObject.AddComponent<LineRenderer>();
        aimLine.useWorldSpace = true;
        aimLine.positionCount = 2;
        aimLine.startWidth = 0.015f;
        aimLine.endWidth = 0.008f;
        aimLine.material = toolMaterial;
        aimLine.enabled = false;

        mallet.MalletRig = rigObject.transform;
        mallet.GripA = gripAObject.transform;
        mallet.GripB = gripBObject.transform;
        mallet.Head = headObject.transform;
        mallet.StrikeFace = strikeFaceObject.transform;
        mallet.HeadCollider = headCollider;
        mallet.AimPreview = aimLine;
        mallet.AimLayerMask = (1 << PickupLayer) | (1 << EnvironmentLayer);
        mallet.VelocityWindowSeconds = 0.03f;
        mallet.DeadzoneSpeed = 0.10f;
        mallet.BallSpeedPerMalletSpeed = 1f;
        mallet.MaximumBallSpeed = 6f;
        mallet.AimAssistStrength = 0.75f;
        mallet.MaxAimDeviationDegrees = 12f;
        mallet.InvalidSwingAngleDegrees = 90f;
        EditorUtility.SetDirty(mallet);
        EditorUtility.SetDirty(rigBody);
        EditorUtility.SetDirty(rigObject.GetComponent<VRCObjectSync>());
        EditorUtility.SetDirty(gripAObject.GetComponent<GateballMalletGrip>());
        EditorUtility.SetDirty(gripBObject.GetComponent<GateballMalletGrip>());

        GameObject gripAVisual = CreatePrimitive(
            "GripAVisual", PrimitiveType.Cylinder, rigObject.transform,
            new Vector3(0f, 0.55f, 0f), new Vector3(0.05f, 0.08f, 0.05f), toolMaterial, PickupLayer);
        GameObject gripBVisual = CreatePrimitive(
            "GripBVisual", PrimitiveType.Cylinder, rigObject.transform,
            new Vector3(0f, -0.15f, 0f), new Vector3(0.05f, 0.08f, 0.05f), toolMaterial, PickupLayer);
        UnityEngine.Object.DestroyImmediate(gripAVisual.GetComponent<Collider>());
        UnityEngine.Object.DestroyImmediate(gripBVisual.GetComponent<Collider>());

        GameObject desktopObject = CreatePrimitive("DesktopControls", PrimitiveType.Cube, root.transform, new Vector3(-4.0f, 0.45f, -8.5f), new Vector3(1.6f, 0.8f, 0.25f), toolMaterial, 0);
        GateballDesktopController desktop = desktopObject.AddUdonSharpComponent<GateballDesktopController>();
        desktop.StrokeRouter = router;
        desktop.SelectedBallId = 1;
        desktop.Power = GateballGeometry.NormalStrokeImpulse;
        desktop.MinimumPower = GateballGeometry.WeakStrokeImpulse;
        desktop.MaximumPower = GateballGeometry.StrongStrokeImpulse;

        GameObject testShotObject = CreatePrimitive("TestShotController", PrimitiveType.Cube, root.transform, new Vector3(4.0f, 0.45f, -8.5f), new Vector3(1.6f, 0.8f, 0.25f), toolMaterial, 0);
        GateballTestShotController testShot = testShotObject.AddUdonSharpComponent<GateballTestShotController>();
        testShot.Court = court;
        testShot.StrokeRouter = router;
        testShot.NetworkState = networkState;
        testShot.AutoRunOnStart = false;
        testShot.AutoRunPlayerId = 1;
        testShot.AutoRunDelaySeconds = 2f;

        GameObject debugObject = new GameObject("DebugUI");
        debugObject.transform.SetParent(root.transform, false);
        debugObject.transform.localPosition = new Vector3(-6.8f, 1.8f, -8.8f);
        debugObject.transform.localRotation = Quaternion.identity;
        Canvas debugCanvas = debugObject.AddComponent<Canvas>();
        debugCanvas.renderMode = RenderMode.WorldSpace;
        debugObject.transform.localScale = Vector3.one * 0.01f;
        GameObject debugTextObject = new GameObject("Text");
        debugTextObject.transform.SetParent(debugObject.transform, false);
        RectTransform debugRect = debugTextObject.AddComponent<RectTransform>();
        debugRect.sizeDelta = new Vector2(700f, 320f);
        Text debugText = debugTextObject.AddComponent<Text>();
        debugText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        debugText.alignment = TextAnchor.UpperLeft;
        debugText.fontSize = 28;
        debugText.color = Color.white;
        debugText.horizontalOverflow = HorizontalWrapMode.Wrap;
        debugText.verticalOverflow = VerticalWrapMode.Overflow;
        GateballDebugDisplay debugDisplay = debugTextObject.AddUdonSharpComponent<GateballDebugDisplay>();
        debugDisplay.NetworkState = networkState;
        debugDisplay.Telemetry = telemetry;
        debugDisplay.Text = debugText;

        court.Balls = balls;
        court.Gates = gates;
        EditorUtility.SetDirty(court);
        EditorUtility.SetDirty(router);
        EditorUtility.SetDirty(desktop);
        EditorUtility.SetDirty(testShot);
        EditorUtility.SetDirty(telemetry);
        EditorUtility.SetDirty(networkState);
        EditorUtility.SetDirty(debugDisplay);

        CreateSpawns(root.transform);
        ConfigureSceneDescriptor();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[GateballDevelopmentSceneBuilder] Physics sandbox built with 10 balls, 3 gates, 1 goal pole, two Grip pickups, and Desktop controls.");
    }

    private static GateballGate CreateGate(Transform parent, int gateIndex, Vector3 position, Material material)
    {
        GameObject gateObject = new GameObject("Gate" + (gateIndex + 1).ToString());
        gateObject.transform.SetParent(parent, false);
        gateObject.transform.localPosition = position;
        GateballGate gate = gateObject.AddUdonSharpComponent<GateballGate>();
        gate.GateIndex = gateIndex;
        gate.OpeningWidth = GateballGeometry.GateOpeningWidth;
        gate.OpeningHeight = GateballGeometry.GateOpeningHeight;
        gate.BallRadius = GateballGeometry.BallRadius;

        float postCenterOffset = GateballGeometry.GateOpeningWidth * 0.5f + GateballGeometry.GatePostDiameter * 0.5f;
        CreateGatePost(gateObject.transform, "LeftPost", gateIndex, new Vector3(-postCenterOffset, GateballGeometry.GatePostHeight * 0.5f, 0f), material);
        CreateGatePost(gateObject.transform, "RightPost", gateIndex, new Vector3(postCenterOffset, GateballGeometry.GatePostHeight * 0.5f, 0f), material);
        CreatePrimitive("TopBar", PrimitiveType.Cube, gateObject.transform, new Vector3(0f, GateballGeometry.GateOpeningHeight + GateballGeometry.GatePostDiameter * 0.5f, 0f), new Vector3(GateballGeometry.GateOpeningWidth + GateballGeometry.GatePostDiameter * 2f, GateballGeometry.GatePostDiameter, GateballGeometry.GatePostDiameter), material, EnvironmentLayer);
        return gate;
    }

    private static void CreateGatePost(Transform parent, string name, int gateIndex, Vector3 position, Material material)
    {
        GameObject post = CreatePrimitive(name, PrimitiveType.Cylinder, parent, position, new Vector3(GateballGeometry.GatePostDiameter, GateballGeometry.GatePostHeight * 0.5f, GateballGeometry.GatePostDiameter), material, EnvironmentLayer);
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

    private static GameObject CreatePickupGrip(
        Transform parent,
        string name,
        int gripIndex,
        GateballMallet mallet)
    {
        GameObject gripObject = new GameObject(name);
        gripObject.layer = PickupLayer;
        gripObject.transform.SetParent(parent, false);
        BoxCollider collider = gripObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.14f, 0.14f, 0.14f);
        collider.isTrigger = true;
        Rigidbody body = gripObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        VRCPickup pickup = gripObject.AddComponent<VRCPickup>();
        pickup.pickupable = true;
        pickup.proximity = 3f;
        GateballMalletGrip grip = gripObject.AddUdonSharpComponent<GateballMalletGrip>();
        grip.Mallet = mallet;
        grip.GripIndex = gripIndex;
        return gripObject;
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
