#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MCT.CraneGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MCT.CraneGame.Editor
{
    public static class CraneGameSetup
    {
        const string RootName = "CraneGameRoot";
        const string BasePath = "Assets/MCT_creative_exercises_2026/CraneGame";
        const string ModelPath = "Assets/MCT_creative_exercises_2026/クレーン2.fbx";
        static readonly Vector2 SafeXLimits = new Vector2(-0.95f, 0.95f);
        // The opened original FBX claws extend about 1.25 units from the carriage
        // along Z. Keep their outer edge inside the cabinet's 1.50-unit inner face.
        static readonly Vector2 SafeZLimits = new Vector2(-0.22f, 0.22f);
        static readonly Vector2 SafeDropXZ = new Vector2(0.95f, -0.22f);
        static readonly Vector3[] SafeRespawnLocals =
        {
            new Vector3(-0.72f, 0.52f, 0.25f),
            new Vector3(0f, 0.52f, 0.42f),
            new Vector3(0.65f, 0.52f, 0.65f)
        };

        [MenuItem("Tools/Crane Game/Build or Repair Crane2 Game %&b")]
        public static void BuildOrRepair()
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                RepairExisting(existing);
                Selection.activeGameObject = existing;
                Debug.Log("CraneGameSetup: 既存の CraneGameRoot を維持し、ColliderとPrefabを修復しました。", existing);
                ValidateScene();
                return;
            }

            EnsureFolders();
            Material frameMaterial = GetOrCreateMaterial("FrameBlue", new Color(0.055f, 0.24f, 0.48f), 0.45f);
            Material panelMaterial = GetOrCreateMaterial("PanelWhite", new Color(0.88f, 0.93f, 0.98f), 0.2f);
            Material playfieldMaterial = GetOrCreateMaterial("Playfield", new Color(0.15f, 0.3f, 0.38f), 0.15f);
            Material chuteMaterial = GetOrCreateMaterial("DropYellow", new Color(1f, 0.55f, 0.04f), 0.35f);
            Material prizeMaterialA = GetOrCreateMaterial("PrizeCoral", new Color(1f, 0.18f, 0.32f), 0.25f);
            Material prizeMaterialB = GetOrCreateMaterial("PrizeMint", new Color(0.05f, 0.85f, 0.62f), 0.25f);
            Material darkMaterial = GetOrCreateMaterial("ControlDark", new Color(0.025f, 0.035f, 0.065f), 0.3f);

            GameObject sourceModel = FindOrCreateModelInstance();
            if (sourceModel == null)
            {
                Debug.LogError("CraneGameSetup: クレーン2 Prefabを読み込めませんでした。" + ModelPath);
                return;
            }

            Bounds sourceBounds = CalculateRendererBounds(sourceModel);
            float desiredGripHeight = 2.25f;
            float currentGripHeight = sourceBounds.min.y + Mathf.Clamp(sourceBounds.size.y * 0.13f, 0.08f, 0.28f);
            sourceModel.transform.position += Vector3.up * (desiredGripHeight - currentGripHeight);
            sourceBounds = CalculateRendererBounds(sourceModel);

            Vector3 rootPosition = new Vector3(sourceBounds.center.x, 0f, sourceBounds.center.z);
            GameObject root = NewObject(RootName, null);
            root.transform.position = rootPosition;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Transform cabinet = NewObject("Cabinet", root.transform).transform;
            CreateCabinet(cabinet, frameMaterial, panelMaterial, playfieldMaterial);

            Transform movementSpace = NewObject("MovementSpace", root.transform).transform;
            Transform carriage = NewObject("Carriage", movementSpace).transform;
            carriage.position = new Vector3(rootPosition.x, sourceModel.transform.position.y, rootPosition.z);
            Transform liftAssembly = NewObject("LiftAssembly", carriage).transform;
            liftAssembly.localPosition = Vector3.zero;

            Undo.SetTransformParent(sourceModel.transform, liftAssembly, "Attach Crane2 model");
            sourceModel.transform.SetSiblingIndex(0);

            Rigidbody liftBody = liftAssembly.gameObject.AddComponent<Rigidbody>();
            liftBody.isKinematic = true;
            liftBody.useGravity = false;
            liftBody.interpolation = RigidbodyInterpolation.Interpolate;
            liftBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            RepairClawOpenPose(sourceModel.transform);
            RestoreOriginalClawAssembly(liftAssembly, sourceModel.transform);
            Transform leftClaw = FindDeepChild(sourceModel.transform, "claw_armL");
            Transform rightClaw = FindDeepChild(sourceModel.transform, "claw_armL.001");
            CraneClawController claw = liftAssembly.gameObject.AddComponent<CraneClawController>();
            claw.Configure(leftClaw, rightClaw, Vector3.right);

            Bounds adjustedBounds = CalculateRendererBounds(sourceModel);
            GameObject anchorObject = NewObject("GripAnchor", liftAssembly);
            anchorObject.transform.position = new Vector3(adjustedBounds.center.x,
                adjustedBounds.min.y + Mathf.Clamp(adjustedBounds.size.y * 0.13f, 0.08f, 0.28f), adjustedBounds.center.z);
            ConfigureClawColliders(sourceModel.transform, anchorObject.transform);
            PrizeGripAssist gripAssist = liftAssembly.gameObject.AddComponent<PrizeGripAssist>();
            gripAssist.Configure(anchorObject.transform,
                FindDeepChild(sourceModel.transform, "LeftGripSensor").GetComponent<ClawContactSensor>(),
                FindDeepChild(sourceModel.transform, "RightGripSensor").GetComponent<ClawContactSensor>());

            Transform home = NewMarker("HomePosition", root.transform, carriage.position);
            Transform drop = NewMarker("DropPosition", root.transform,
                root.transform.TransformPoint(new Vector3(SafeDropXZ.x, carriage.position.y, SafeDropXZ.y)));

            PrizeRespawner respawner = root.AddComponent<PrizeRespawner>();
            CraneController controller = root.AddComponent<CraneController>();
            CraneGrabSequence sequence = root.AddComponent<CraneGrabSequence>();
            KeyboardCraneInputAdapter keyboard = root.AddComponent<KeyboardCraneInputAdapter>();

            sequence.Configure(controller, carriage, liftAssembly, claw, gripAssist, home, drop);
            SetSerializedFloat(sequence, "lowerDistance", 1.55f);
            controller.Configure(carriage, movementSpace, sequence, respawner);
            controller.SetLimits(SafeXLimits, SafeZLimits);
            keyboard.Configure(controller);

            Transform prizesContainer = NewObject("Prizes", root.transform).transform;
            Transform[] spawnPoints = CreateSpawnPoints(root.transform);
            respawner.Configure(root.transform, spawnPoints);

            List<Prize> prizes = new List<Prize>();
            for (int i = 0; i < 3; i++)
            {
                Material material = i % 2 == 0 ? prizeMaterialA : prizeMaterialB;
                Prize prize = CreatePrize("Prize_" + (i + 1), spawnPoints[i].position, prizesContainer, material);
                prize.Configure(respawner);
                ConfigureClusterPrize(prize.gameObject);
                prizes.Add(prize);
            }

            CreateDropZone(root.transform, chuteMaterial);
            CreateControls(root.transform, controller, darkMaterial, chuteMaterial, frameMaterial);
            CreateLightingAndSigns(root.transform, sequence, panelMaterial);

            // CraneGameRoot must stay a plain GameObject. cluster rejects an Item that
            // contains the button/prize Items, so only the crane mechanism is an Item.
            ConfigureClusterScript(movementSpace.gameObject, BasePath + "/Cluster/CraneClusterController.js");

            SavePrizePrefab(prizes[0].gameObject);
            SaveRootPrefab(root);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log("CraneGameSetup: クレーン2を使用したクレーンゲームを構築しました。", root);
            ValidateScene();
        }

        [MenuItem("Tools/Crane Game/Validate Crane Game %&v")]
        public static void ValidateScene()
        {
            GameObject root = GameObject.Find(RootName);
            List<string> errors = new List<string>();
            if (root == null)
            {
                errors.Add("CraneGameRoot がありません。");
            }
            else
            {
                RequireComponent<CraneController>(root, errors);
                RequireComponent<CraneGrabSequence>(root, errors);
                RequireComponent<PrizeRespawner>(root, errors);
                RequireComponent<KeyboardCraneInputAdapter>(root, errors);
                if (root.transform.localScale != Vector3.one) errors.Add("CraneGameRoot のScaleが (1,1,1) ではありません。");
                if (FindDeepChild(root.transform, "クレーン2") == null) errors.Add("クレーン2 Prefabインスタンスが見つかりません。");
                if (FindDeepChild(root.transform, "HomePosition") == null) errors.Add("HomePosition がありません。");
                if (FindDeepChild(root.transform, "DropPosition") == null) errors.Add("DropPosition がありません。");
                if (FindDeepChild(root.transform, "LeftGripSensor") == null) errors.Add("LeftGripSensor がありません。");
                if (FindDeepChild(root.transform, "RightGripSensor") == null) errors.Add("RightGripSensor がありません。");
                if (root.GetComponentsInChildren<Prize>(true).Length < 1) errors.Add("景品がありません。");
                foreach (Prize prize in root.GetComponentsInChildren<Prize>(true))
                {
                    if (prize.GetComponent<Rigidbody>() == null || prize.GetComponent<Collider>() == null)
                    {
                        errors.Add(prize.name + " にRigidbodyまたはColliderがありません。");
                    }
                }
                ValidateNoNestedClusterItems(root, errors);
                ValidateCraneClearance(root, errors);
            }

            if (errors.Count == 0)
            {
                Debug.Log("CraneGame validation PASS: 必須参照、クレーン2、景品物理、座標Markerを確認しました。", root);
            }
            else
            {
                Debug.LogError("CraneGame validation FAILED\n- " + string.Join("\n- ", errors), root);
            }
        }

        [MenuItem("Tools/Crane Game/Log Claw Diagnostics %&d")]
        public static void LogClawDiagnostics()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.LogError("Crane claw diagnostics: CraneGameRoot がありません。");
                return;
            }

            Transform left = FindDeepChild(root.transform, "claw_armL");
            Transform right = FindDeepChild(root.transform, "claw_armL.001");
            Transform anchor = FindDeepChild(root.transform, "GripAnchor");
            List<string> lines = new List<string>();
            AppendTransformDiagnostics(lines, "LEFT", left, root.transform);
            AppendTransformDiagnostics(lines, "RIGHT", right, root.transform);
            AppendTransformDiagnostics(lines, "GRIP", anchor, root.transform);

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.IndexOf("claw", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    child != left && child != right)
                {
                    AppendTransformDiagnostics(lines, "CLAW_NODE", child, root.transform);
                }
            }

            foreach (Prize prize in root.GetComponentsInChildren<Prize>(true))
            {
                lines.Add("PRIZE " + prize.name + " world=" + FormatVector(prize.transform.position) +
                    " held=" + prize.IsHeld + " acquired=" + prize.IsAcquired);
            }
            Debug.Log("Crane claw diagnostics\n" + string.Join("\n", lines), root);
        }

        static void AppendTransformDiagnostics(List<string> lines, string label, Transform target, Transform root)
        {
            if (target == null)
            {
                lines.Add(label + " <missing>");
                return;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default(Bounds);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            lines.Add(label + " path=" + AnimationUtility.CalculateTransformPath(target, root) +
                " parent=" + (target.parent != null ? target.parent.name : "<none>") +
                " localPos=" + FormatVector(target.localPosition) +
                " localEuler=" + FormatVector(target.localEulerAngles) +
                " localScale=" + FormatVector(target.localScale) +
                " worldPos=" + FormatVector(target.position) +
                (hasBounds ? " boundsCenter=" + FormatVector(bounds.center) + " boundsSize=" + FormatVector(bounds.size) : ""));
        }

        static string FormatVector(Vector3 value)
        {
            return string.Format("({0:F4},{1:F4},{2:F4})", value.x, value.y, value.z);
        }

        [MenuItem("Tools/Crane Game/Repair Cluster Item Hierarchy In Prefab")]
        public static void RepairClusterItemHierarchyInPrefab()
        {
            string path = BasePath + "/Prefabs/CraneGame.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RepairClusterItemHierarchy(contents);
                ApplyGameplaySafetySettings(contents);
                EnableClusterPrizeItems(contents, false);
                List<string> errors = new List<string>();
                ValidateNoNestedClusterItems(contents, errors);
                if (errors.Count > 0)
                {
                    throw new InvalidOperationException("cluster Item hierarchy repair failed:\n- " +
                        string.Join("\n- ", errors));
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                AssetDatabase.SaveAssets();
                Debug.Log("CraneGameSetup: Prefabのcluster Item入れ子を解消しました。", contents);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            GameObject sceneRoot = GameObject.Find(RootName);
            if (sceneRoot != null)
            {
                ApplyGameplaySafetySettings(sceneRoot);
                EnableClusterPrizeItems(sceneRoot, true);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }
        }

        static GameObject FindOrCreateModelInstance()
        {
            GameObject sceneModel = SceneManager.GetActiveScene().GetRootGameObjects()
                .FirstOrDefault(x => x.name == "クレーン2");
            if (sceneModel != null)
            {
                return sceneModel;
            }

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (asset == null)
            {
                return null;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.name = "クレーン2";
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Crane2");
            return instance;
        }

        static void CreateCabinet(Transform parent, Material frame, Material panel, Material playfield)
        {
            CreateCube("PlayField", parent, new Vector3(0f, 0.08f, 0f), new Vector3(3.5f, 0.16f, 3.25f), playfield);
            CreateCube("BackPanel", parent, new Vector3(0f, 1.65f, 1.65f), new Vector3(3.65f, 3.3f, 0.12f), panel);
            CreateCube("Top", parent, new Vector3(0f, 3.3f, 0f), new Vector3(3.7f, 0.16f, 3.45f), frame);
            float[] xs = { -1.78f, 1.78f };
            float[] zs = { -1.65f, 1.65f };
            foreach (float x in xs)
            {
                foreach (float z in zs)
                {
                    CreateCube("FramePost", parent, new Vector3(x, 1.65f, z), new Vector3(0.14f, 3.3f, 0.14f), frame);
                }
            }
            foreach (float x in xs)
            {
                CreateCube("SideBase", parent, new Vector3(x, 0.22f, 0f), new Vector3(0.14f, 0.28f, 3.35f), frame);
            }
            CreateCube("FrontHeader", parent, new Vector3(0f, 3.08f, -1.65f), new Vector3(3.65f, 0.35f, 0.14f), frame);
        }

        static Transform[] CreateSpawnPoints(Transform root)
        {
            Transform group = NewObject("RespawnPositions", root).transform;
            Transform[] result = new Transform[SafeRespawnLocals.Length];
            for (int i = 0; i < SafeRespawnLocals.Length; i++)
            {
                result[i] = NewMarker("RespawnPosition_" + (i + 1), group,
                    root.TransformPoint(SafeRespawnLocals[i]));
            }
            return result;
        }

        static Prize CreatePrize(string name, Vector3 position, Transform parent, Material material)
        {
            GameObject prizeObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(prizeObject, "Create prize");
            prizeObject.name = name;
            prizeObject.transform.SetParent(parent, true);
            prizeObject.transform.position = position;
            prizeObject.transform.localScale = new Vector3(0.52f, 0.38f, 0.52f);
            prizeObject.GetComponent<Renderer>().sharedMaterial = material;
            Rigidbody body = prizeObject.AddComponent<Rigidbody>();
            body.mass = 0.34f;
            body.linearDamping = 0.35f;
            body.angularDamping = 0.55f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Prize prize = prizeObject.AddComponent<Prize>();
            return prize;
        }

        static void CreateDropZone(Transform root, Material chuteMaterial)
        {
            Transform chute = NewObject("PrizeChute", root).transform;
            chute.localPosition = new Vector3(SafeDropXZ.x, 0f, SafeDropXZ.y);
            CreateCube("ChuteBase", chute, new Vector3(0f, 0.13f, 0f), new Vector3(0.75f, 0.18f, 0.75f), chuteMaterial);
            CreateCube("ChuteBack", chute, new Vector3(0f, 0.35f, 0.34f), new Vector3(0.75f, 0.5f, 0.08f), chuteMaterial);
            GameObject trigger = NewObject("DropZone", chute);
            trigger.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            BoxCollider collider = trigger.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(0.72f, 1.05f, 0.72f);
            trigger.AddComponent<PrizeDropZone>();
            CreateText("DROP", chute, new Vector3(0f, 0.32f, -0.39f), 0.13f, Color.black);
        }

        static void CreateControls(Transform root, CraneController controller, Material consoleMaterial, Material grabMaterial, Material moveMaterial)
        {
            Transform controls = NewObject("Controls", root).transform;
            controls.localPosition = new Vector3(0f, 0f, -1.58f);
            CreateCube("ControlConsole", controls, new Vector3(0f, 0.62f, 0f), new Vector3(2.75f, 0.45f, 0.52f), consoleMaterial);
            CreateButton("LeftButton", "◀", CraneWorldButton.Command.MoveLeft, new Vector3(-0.92f, 0.9f, 0f), controls, controller, moveMaterial, "CraneButtonLeft.js");
            CreateButton("ForwardButton", "▲", CraneWorldButton.Command.MoveForward, new Vector3(-0.46f, 0.9f, 0f), controls, controller, moveMaterial, "CraneButtonForward.js");
            CreateButton("BackButton", "▼", CraneWorldButton.Command.MoveBack, new Vector3(0f, 0.9f, 0f), controls, controller, moveMaterial, "CraneButtonBack.js");
            CreateButton("RightButton", "▶", CraneWorldButton.Command.MoveRight, new Vector3(0.46f, 0.9f, 0f), controls, controller, moveMaterial, "CraneButtonRight.js");
            CreateButton("GrabButton", "GRAB", CraneWorldButton.Command.Grab, new Vector3(1.02f, 0.9f, 0f), controls, controller, grabMaterial, "CraneButtonGrab.js", new Vector3(0.42f, 0.16f, 0.36f));
            CreateButton("ResetButton", "RESET", CraneWorldButton.Command.Reset, new Vector3(1.42f, 0.9f, 0f), controls, controller, grabMaterial, "CraneButtonReset.js", new Vector3(0.28f, 0.14f, 0.3f));
            CreateText("MOVE     /     GRAB", controls, new Vector3(0f, 1.16f, -0.27f), 0.09f, Color.white);
        }

        static void CreateButton(string name, string label, CraneWorldButton.Command command, Vector3 localPosition,
            Transform parent, CraneController controller, Material material, string clusterScript, Vector3? scale = null)
        {
            GameObject button = CreateCube(name, parent, localPosition, scale ?? new Vector3(0.32f, 0.14f, 0.32f), material);
            CraneWorldButton worldButton = button.AddComponent<CraneWorldButton>();
            worldButton.Configure(controller, command);
            CreateText(label, button.transform, new Vector3(0f, 0.58f, 0f), 0.08f, Color.white, new Vector3(90f, 0f, 0f));
            ConfigureClusterScript(button, BasePath + "/Cluster/" + clusterScript);
        }

        static void CreateLightingAndSigns(Transform root, CraneGrabSequence sequence, Material panelMaterial)
        {
            GameObject lightObject = NewObject("CraneInteriorLight", root);
            lightObject.transform.localPosition = new Vector3(0f, 2.95f, -0.2f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 2.2f;
            light.color = new Color(0.8f, 0.9f, 1f);

            Transform sign = NewObject("GuideDisplay", root).transform;
            sign.localPosition = new Vector3(0f, 2.82f, -1.34f);
            CreateCube("GuidePanel", sign, Vector3.zero, new Vector3(2.45f, 0.42f, 0.08f), panelMaterial);
            TextMesh label = CreateText("READY  /  WASD + SPACE", sign, new Vector3(0f, 0f, -0.06f), 0.11f, new Color(0.03f, 0.15f, 0.3f), new Vector3(0f, 180f, 0f));
            CraneStatusDisplay display = sign.gameObject.AddComponent<CraneStatusDisplay>();
            display.Configure(sequence, label);
        }

        static void ConfigureClawColliders(Transform model, Transform gripAnchor)
        {
            Transform leftArm = FindDeepChild(model, "claw_armL");
            Transform rightArm = FindDeepChild(model, "claw_armL.001");
            Transform leftJoint = FindDeepChild(model, "claw_jointL");
            Transform rightJoint = FindDeepChild(model, "claw_jointR");
            if (leftArm == null || rightArm == null || leftJoint == null || rightJoint == null)
            {
                Debug.LogWarning("CraneGameSetup: claw collider構築に必要な既存アームがありません。", model);
                return;
            }

            DestroyChildIfPresent(model, "LeftGripSensor");
            DestroyChildIfPresent(model, "RightGripSensor");
            foreach (BoxCollider existing in model.GetComponentsInChildren<BoxCollider>(true))
            {
                UnityEngine.Object.DestroyImmediate(existing, true);
            }

            // Fit physical colliders only to the existing FBX arm meshes. The former
            // implementation also boxed the dome and decorative meshes.
            foreach (MeshFilter meshFilter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null ||
                    (!meshFilter.transform.IsChildOf(leftArm) && !meshFilter.transform.IsChildOf(rightArm)))
                {
                    continue;
                }
                BoxCollider physical = meshFilter.gameObject.AddComponent<BoxCollider>();
                physical.center = meshFilter.sharedMesh.bounds.center;
                physical.size = meshFilter.sharedMesh.bounds.size;
            }

            CreateGripSensor(leftJoint, gripAnchor, "LeftGripSensor");
            CreateGripSensor(rightJoint, gripAnchor, "RightGripSensor");
        }

        static void CreateGripSensor(Transform joint, Transform gripAnchor, string sensorName)
        {
            MeshFilter mesh = joint.GetComponent<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null)
            {
                Debug.LogWarning("CraneGameSetup: " + joint.name + " にセンサー基準Meshがありません。", joint);
                return;
            }

            GameObject sensor = NewObject(sensorName, joint);
            sensor.transform.localPosition = Vector3.zero;
            sensor.transform.localRotation = Quaternion.identity;
            sensor.transform.localScale = Vector3.one;
            BoxCollider trigger = sensor.AddComponent<BoxCollider>();
            Bounds meshBounds = mesh.sharedMesh.bounds;
            if (gripAnchor != null)
            {
                Vector3 anchorInJoint = joint.InverseTransformPoint(gripAnchor.position);
                Vector3 closestTip = meshBounds.ClosestPoint(anchorInJoint);
                trigger.center = Vector3.Lerp(meshBounds.center, closestTip, 0.95f);
            }
            else
            {
                trigger.center = meshBounds.center;
            }
            trigger.size = Vector3.Scale(meshBounds.size, new Vector3(0.9f, 0.9f, 0.9f));
            trigger.isTrigger = true;
            sensor.AddComponent<ClawContactSensor>();

            // Optional cluster marker. Reflection keeps the common runtime free from
            // a hard dependency on Creator Kit.
            Type detectorType = FindType("ClusterVR.CreatorKit.Item.Implements.OverlapDetectorShape");
            if (detectorType != null)
            {
                sensor.AddComponent(detectorType);
            }
        }

        static void DestroyChildIfPresent(Transform root, string childName)
        {
            Transform child = FindDeepChild(root, childName);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject, true);
            }
        }

        static void RepairExisting(GameObject root)
        {
            RepairClusterItemHierarchy(root);
            ApplyGameplaySafetySettings(root);
            EnableClusterPrizeItems(root, true);

            Transform model = FindDeepChild(root.transform, "クレーン2");
            if (model != null)
            {
                RepairClawOpenPose(model);
                foreach (MeshCollider meshCollider in model.GetComponentsInChildren<MeshCollider>(true))
                {
                    UnityEngine.Object.DestroyImmediate(meshCollider);
                }
                ConfigureClawColliders(model, FindDeepChild(root.transform, "GripAnchor"));
            }

            CraneClawController clawController = root.GetComponentInChildren<CraneClawController>(true);
            if (clawController != null && model != null)
            {
                clawController.Configure(FindDeepChild(model, "claw_armL"),
                    FindDeepChild(model, "claw_armL.001"), Vector3.right);
            }

            foreach (TextMesh text in root.GetComponentsInChildren<TextMesh>(true))
            {
                Transform parent = text.transform.parent;
                bool isButtonLabel = parent != null && parent.name.EndsWith("Button", StringComparison.Ordinal);
                bool isGuideLabel = parent != null && parent.name == "GuideDisplay";
                text.characterSize = isGuideLabel ? 0.03f : isButtonLabel ? 0.018f : 0.02f;
                text.fontSize = 48;
                text.transform.localRotation = isButtonLabel
                    ? Quaternion.Euler(-90f, 0f, 0f)
                    : Quaternion.identity;
            }

            PrizeGripAssist gripAssist = root.GetComponentInChildren<PrizeGripAssist>(true);
            if (gripAssist != null)
            {
                Transform anchor = FindDeepChild(root.transform, "GripAnchor");
                Transform leftSensor = FindDeepChild(root.transform, "LeftGripSensor");
                Transform rightSensor = FindDeepChild(root.transform, "RightGripSensor");
                gripAssist.Configure(anchor,
                    leftSensor != null ? leftSensor.GetComponent<ClawContactSensor>() : null,
                    rightSensor != null ? rightSensor.GetComponent<ClawContactSensor>() : null);
                SetSerializedFloat(gripAssist, "maximumFollowDistance", 1.1f);
                SetSerializedFloat(gripAssist, "slipChancePerSecond", 0.04f);
                SetSerializedFloat(gripAssist, "jointBreakForce", 5000f);
                SetSerializedFloat(gripAssist, "jointBreakTorque", 5000f);
            }

            RepairPrizePrefabAsset();
            foreach (Prize prize in root.GetComponentsInChildren<Prize>(true))
            {
                ConfigureClusterPrize(prize.gameObject);
            }

            Transform controls = FindDeepChild(root.transform, "Controls");
            CraneController controller = root.GetComponent<CraneController>();
            if (controls != null && controller != null && FindDeepChild(controls, "ResetButton") == null)
            {
                Transform console = FindDeepChild(controls, "ControlConsole");
                if (console != null)
                {
                    Vector3 scale = console.localScale;
                    scale.x = Mathf.Max(scale.x, 3.15f);
                    console.localScale = scale;
                }
                Material resetMaterial = AssetDatabase.LoadAssetAtPath<Material>(BasePath + "/Materials/DropYellow.mat");
                CreateButton("ResetButton", "RESET", CraneWorldButton.Command.Reset, new Vector3(1.42f, 0.9f, 0f),
                    controls, controller, resetMaterial, "CraneButtonReset.js", new Vector3(0.28f, 0.14f, 0.3f));
            }

            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(cube, "Create " + name);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        static TextMesh CreateText(string text, Transform parent, Vector3 localPosition, float characterSize, Color color, Vector3? localEuler = null)
        {
            GameObject textObject = NewObject(text + "_Label", parent);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localEulerAngles = localEuler ?? new Vector3(0f, 180f, 0f);
            TextMesh mesh = textObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = characterSize * 0.1f;
            mesh.fontSize = 48;
            mesh.color = color;
            return mesh;
        }

        static Transform NewMarker(string name, Transform parent, Vector3 worldPosition)
        {
            Transform marker = NewObject(name, parent).transform;
            marker.position = worldPosition;
            marker.rotation = parent.rotation;
            return marker;
        }

        static GameObject NewObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(result, "Create " + name);
            if (parent != null)
            {
                result.transform.SetParent(parent, false);
            }
            return result;
        }

        static Bounds CalculateRendererBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(target.transform.position, Vector3.one);
            }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static Transform FindDeepChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        static void RepairClawOpenPose(Transform model)
        {
            Transform leftArm = FindDeepChild(model, "claw_armL");
            Transform rightArm = FindDeepChild(model, "claw_armL.001");
            Transform leftJoint = FindDeepChild(model, "claw_jointL");
            Transform rightJoint = FindDeepChild(model, "claw_jointR");
            if (leftArm == null || rightArm == null || leftJoint == null || rightJoint == null)
            {
                Debug.LogWarning("CraneGameSetup: 既存FBXの左右アームまたはJointが見つかりません。", model);
                return;
            }

            RestoreTransformFromFbx(leftArm);
            RestoreTransformFromFbx(rightArm);
            RestoreTransformFromFbx(leftJoint);
            RestoreTransformFromFbx(rightJoint);

            // FBX source pose has both tips touching (closed). Store a symmetric open
            // pose in the prefab; runtime close deltas return it to the source pose.
            leftArm.localRotation = Quaternion.AngleAxis(18f, Vector3.right) * leftArm.localRotation;
            rightArm.localRotation = Quaternion.AngleAxis(-18f, Vector3.right) * rightArm.localRotation;
        }

        static void RestoreTransformFromFbx(Transform target)
        {
            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Transform source = sourceModel != null ? FindDeepChild(sourceModel.transform, target.name) : null;
            if (source == null)
            {
                Debug.LogWarning("CraneGameSetup: FBX原本から " + target.name + " を復元できません。", target);
                return;
            }
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        static void RestoreOriginalClawAssembly(Transform liftAssembly, Transform model)
        {
            Transform generated = FindDeepChild(liftAssembly, "SymmetricClawAssembly");
            if (generated != null)
            {
                UnityEngine.Object.DestroyImmediate(generated.gameObject);
            }

            Transform[] originalRoots =
            {
                FindDeepChild(model, "claw_armL"),
                FindDeepChild(model, "claw_armL.001")
            };
            foreach (Transform originalRoot in originalRoots)
            {
                if (originalRoot == null) continue;
                foreach (Renderer renderer in originalRoot.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = true;
                }
                foreach (Collider collider in originalRoot.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = true;
                }
            }
        }

        static void RequireComponent<T>(GameObject target, List<string> errors) where T : Component
        {
            if (target.GetComponent<T>() == null) errors.Add(typeof(T).Name + " がありません。");
        }

        static void SetSerializedFloat(UnityEngine.Object target, string propertyName, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets/MCT_creative_exercises_2026", "CraneGame");
            CreateFolderIfMissing(BasePath, "Materials");
            CreateFolderIfMissing(BasePath, "Prefabs");
        }

        static void CreateFolderIfMissing(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        static Material GetOrCreateMaterial(string name, Color color, float smoothness)
        {
            string path = BasePath + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void SavePrizePrefab(GameObject prize)
        {
            string path = BasePath + "/Prefabs/Prize.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(prize, path, InteractionMode.AutomatedAction);
        }

        static void SaveRootPrefab(GameObject root)
        {
            string path = BasePath + "/Prefabs/CraneGame.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        }

        static void ConfigureClusterScript(GameObject target, string scriptPath)
        {
            Type scriptableItemType = FindType("ClusterVR.CreatorKit.Item.Implements.ScriptableItem");
            UnityEngine.Object scriptAsset = AssetDatabase.LoadMainAssetAtPath(scriptPath);
            if (scriptableItemType == null || scriptAsset == null)
            {
                return;
            }

            Component component = target.GetComponent(scriptableItemType);
            if (component == null)
            {
                component = target.AddComponent(scriptableItemType);
            }
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty source = serialized.FindProperty("sourceCodeAsset");
            if (source != null)
            {
                source.objectReferenceValue = scriptAsset;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void ConfigureClusterPrize(GameObject target)
        {
            ConfigureClusterScript(target, BasePath + "/Cluster/CraneClusterPrize.js");
            Type movableItemType = FindType("ClusterVR.CreatorKit.Item.Implements.MovableItem");
            if (movableItemType != null && target.GetComponent(movableItemType) == null)
            {
                target.AddComponent(movableItemType);
            }
        }

        static void RepairClusterItemHierarchy(GameObject root)
        {
            Transform movementSpace = FindDeepChild(root.transform, "MovementSpace");
            if (movementSpace == null)
            {
                Debug.LogError("CraneGameSetup: MovementSpace がないためcluster Item階層を修復できません。", root);
                return;
            }

            ConfigureClusterScript(movementSpace.gameObject, BasePath + "/Cluster/CraneClusterController.js");

            // Remove the legacy controller Item from CraneGameRoot after its script has
            // been installed on MovementSpace. Buttons and prizes remain sibling Items.
            Type scriptableItemType = FindType("ClusterVR.CreatorKit.Item.Implements.ScriptableItem");
            Type itemType = FindType("ClusterVR.CreatorKit.Item.Implements.Item");
            RemoveComponentIfPresent(root, scriptableItemType);
            RemoveComponentIfPresent(root, itemType);
        }

        static void RemoveComponentIfPresent(GameObject target, Type componentType)
        {
            if (componentType == null)
            {
                return;
            }
            Component component = target.GetComponent(componentType);
            if (component != null)
            {
                UnityEngine.Object.DestroyImmediate(component, true);
            }
        }

        static void ValidateNoNestedClusterItems(GameObject root, List<string> errors)
        {
            Type itemType = FindType("ClusterVR.CreatorKit.Item.Implements.Item");
            if (itemType == null)
            {
                return;
            }

            foreach (Component item in root.GetComponentsInChildren(itemType, true))
            {
                Behaviour behaviour = item as Behaviour;
                if (behaviour != null && !behaviour.enabled)
                {
                    errors.Add(item.name + " のcluster Itemが無効です。");
                }
                Transform parent = item.transform.parent;
                while (parent != null && parent.IsChildOf(root.transform))
                {
                    if (parent.GetComponent(itemType) != null)
                    {
                        errors.Add(item.name + " がItem " + parent.name + " の子になっています。");
                        break;
                    }
                    parent = parent.parent;
                }
            }
        }

        static void ApplyGameplaySafetySettings(GameObject root)
        {
            ApplyCabinetClearanceSettings(root);
            RepairRespawnLayout(root);

            Transform model = FindDeepChild(root.transform, "クレーン2");
            if (model != null)
            {
                RepairClawOpenPose(model);
                Transform lift = FindDeepChild(root.transform, "LiftAssembly");
                if (lift != null)
                {
                    RestoreOriginalClawAssembly(lift, model);
                }
                CraneClawController claw = root.GetComponentInChildren<CraneClawController>(true);
                if (claw != null)
                {
                    claw.Configure(FindDeepChild(model, "claw_armL"),
                        FindDeepChild(model, "claw_armL.001"), Vector3.right);
                }
            }

            CraneController controller = root.GetComponent<CraneController>();
            if (controller != null)
            {
                controller.SetLimits(SafeXLimits, SafeZLimits);
            }

            CraneGrabSequence sequence = root.GetComponent<CraneGrabSequence>();
            if (sequence != null)
            {
                SetSerializedFloat(sequence, "lowerDistance", 1.72f);
            }

            Transform drop = FindDeepChild(root.transform, "DropPosition");
            if (drop != null)
            {
                Vector3 local = drop.localPosition;
                local.x = SafeDropXZ.x;
                local.z = SafeDropXZ.y;
                drop.localPosition = local;
            }

            Transform chute = FindDeepChild(root.transform, "PrizeChute");
            if (chute != null)
            {
                Vector3 local = chute.localPosition;
                local.x = SafeDropXZ.x;
                local.z = SafeDropXZ.y;
                chute.localPosition = local;
            }

            Transform carriage = FindDeepChild(root.transform, "Carriage");
            if (carriage != null)
            {
                Vector3 local = carriage.localPosition;
                local.x = Mathf.Clamp(local.x, SafeXLimits.x, SafeXLimits.y);
                local.z = Mathf.Clamp(local.z, SafeZLimits.x, SafeZLimits.y);
                carriage.localPosition = local;
            }
        }

        static void RepairRespawnLayout(GameObject root)
        {
            Transform[] points = new Transform[SafeRespawnLocals.Length];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = FindDeepChild(root.transform, "RespawnPosition_" + (i + 1));
                if (points[i] != null)
                {
                    points[i].position = root.transform.TransformPoint(SafeRespawnLocals[i]);
                    points[i].rotation = root.transform.rotation;
                }
            }

            Prize[] prizes = root.GetComponentsInChildren<Prize>(true)
                .OrderBy(prize => prize.name).ToArray();
            for (int i = 0; i < prizes.Length && i < points.Length; i++)
            {
                if (points[i] != null && !Application.isPlaying)
                {
                    prizes[i].transform.SetPositionAndRotation(points[i].position, points[i].rotation);
                }
            }
        }

        static void ValidateCraneClearance(GameObject root, List<string> errors)
        {
            Transform model = FindDeepChild(root.transform, "クレーン2");
            Transform movementSpace = FindDeepChild(root.transform, "MovementSpace");
            Transform carriage = FindDeepChild(root.transform, "Carriage");
            if (model == null || movementSpace == null || carriage == null)
            {
                errors.Add("Crane clearance検証に必要なModel、MovementSpace、Carriageのいずれかがありません。");
                return;
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                errors.Add("Crane clearance検証対象のRendererがありません。");
                return;
            }

            Vector3 localMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 localMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (Renderer renderer in renderers)
            {
                Bounds bounds = renderer.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 world = new Vector3(x == 0 ? min.x : max.x,
                                y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                            Vector3 local = movementSpace.InverseTransformPoint(world);
                            localMin = Vector3.Min(localMin, local);
                            localMax = Vector3.Max(localMax, local);
                        }
                    }
                }
            }

            Vector3 carriageLocal = carriage.localPosition;
            Vector3 relativeMin = localMin - carriageLocal;
            Vector3 relativeMax = localMax - carriageLocal;
            float left = SafeXLimits.x + relativeMin.x;
            float right = SafeXLimits.y + relativeMax.x;
            float front = SafeZLimits.x + relativeMin.z;
            float back = SafeZLimits.y + relativeMax.z;

            // Cabinet inner faces are approximately X +/-1.71 and Z +/-1.59.
            // Keep an additional margin for collider thickness and network interpolation.
            const float innerX = 1.63f;
            const float innerZ = 1.50f;
            if (left < -innerX || right > innerX || front < -innerZ || back > innerZ)
            {
                errors.Add(string.Format("Crane可動域が筐体内寸を超えます: X[{0:F2}, {1:F2}] Z[{2:F2}, {3:F2}]",
                    left, right, front, back));
            }
            else
            {
                Debug.Log(string.Format("Crane clearance PASS: X[{0:F2}, {1:F2}] Z[{2:F2}, {3:F2}]",
                    left, right, front, back), root);
            }
        }

        static void ApplyCabinetClearanceSettings(GameObject root)
        {
            Transform cabinet = FindDeepChild(root.transform, "Cabinet");
            if (cabinet == null)
            {
                return;
            }

            foreach (Transform child in cabinet.GetComponentsInChildren<Transform>(true))
            {
                Vector3 position = child.localPosition;
                Vector3 scale = child.localScale;
                if (child.name == "PlayField")
                {
                    scale.z = 3.25f;
                }
                else if (child.name == "BackPanel")
                {
                    position.z = 1.65f;
                }
                else if (child.name == "Top")
                {
                    scale.z = 3.45f;
                }
                else if (child.name == "FramePost")
                {
                    position.z = Mathf.Sign(position.z) * 1.65f;
                }
                else if (child.name == "SideBase")
                {
                    scale.z = 3.35f;
                }
                else if (child.name == "FrontHeader")
                {
                    position.z = -1.65f;
                }
                child.localPosition = position;
                child.localScale = scale;
            }
        }

        static void EnableClusterPrizeItems(GameObject root, bool revertSceneOverrides)
        {
            Type itemType = FindType("ClusterVR.CreatorKit.Item.Implements.Item");
            if (itemType == null)
            {
                return;
            }

            foreach (Prize prize in root.GetComponentsInChildren<Prize>(true))
            {
                Component item = prize.GetComponent(itemType);
                if (item == null)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(item);
                SerializedProperty enabled = serialized.FindProperty("m_Enabled");
                if (enabled == null)
                {
                    continue;
                }

                if (revertSceneOverrides && enabled.prefabOverride)
                {
                    PrefabUtility.RevertPropertyOverride(enabled, InteractionMode.AutomatedAction);
                    serialized.Update();
                    enabled = serialized.FindProperty("m_Enabled");
                }
                enabled.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void RepairPrizePrefabAsset()
        {
            string path = BasePath + "/Prefabs/Prize.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                return;
            }
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureClusterPrize(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
#endif
