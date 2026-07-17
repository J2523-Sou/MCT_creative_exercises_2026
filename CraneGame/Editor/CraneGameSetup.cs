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
        const string CraneModelSourcePath = "Assets/newCrene.fbx";
        const string ModelPath = BasePath + "/Models/newCrene.fbx";
        const string ModelObjectName = "newCrene";
        const string PrizeModelSourcePath = "Assets/magatama.fbx";
        const string PrizeModelPath = BasePath + "/Models/magatama.fbx";
        const float CabinetScale = 1.5f;
        const float FallbackCarriageHeight = 5.25f;
        static readonly Vector2 SafeXLimits = new Vector2(-1.7f, 1.7f);
        // The opened original FBX claws extend about 1.25 units from the carriage
        // along Z. Keep their outer edge inside the cabinet's 1.50-unit inner face.
        static readonly Vector2 SafeZLimits = new Vector2(-0.9f, 0.9f);
        static readonly Vector2 SafeDropXZ = new Vector2(1.7f, -0.9f);
        static readonly Vector3[] SafeRespawnLocals =
        {
            new Vector3(-1.08f, 0.60f, 0.375f),
            new Vector3(0f, 0.60f, 0.63f),
            new Vector3(0.975f, 0.60f, 0.975f)
        };

        [MenuItem("Tools/Crane Game/Build or Repair newCrene Game %&b")]
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
            EnsureCraneModelAsset();
            EnsurePrizeModelAsset();
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
                Debug.LogError("CraneGameSetup: newCrene Prefabを読み込めませんでした。" + ModelPath);
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

            Undo.SetTransformParent(sourceModel.transform, liftAssembly, "Attach newCrene model");
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
            sequence.ConfigureFloorContact(sourceModel.transform,
                FindDeepChild(root.transform, "PlayField").GetComponent<Collider>());
            SetSerializedFloat(sequence, "lowerDistance", 3.65f);
            SetSerializedFloat(sequence, "minimumGripHeight", 0.46f);
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
            ApplyGameplaySafetySettings(root);

            // CraneGameRoot must stay a plain GameObject. cluster rejects an Item that
            // contains the button/prize Items, so only the crane mechanism is an Item.
            ConfigureClusterScript(movementSpace.gameObject, BasePath + "/Cluster/CraneClusterController.js");

            SavePrizePrefab(prizes[0].gameObject);
            SaveRootPrefab(root);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log("CraneGameSetup: newCreneを使用したクレーンゲームを構築しました。", root);
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
                if (FindDeepChild(root.transform, ModelObjectName) == null) errors.Add("newCrene Prefabインスタンスが見つかりません。");
                if (FindDeepChild(root.transform, "HomePosition") == null) errors.Add("HomePosition がありません。");
                if (FindDeepChild(root.transform, "DropPosition") == null) errors.Add("DropPosition がありません。");
                if (FindDeepChild(root.transform, "LeftGripSensor") == null) errors.Add("LeftGripSensor がありません。");
                if (FindDeepChild(root.transform, "RightGripSensor") == null) errors.Add("RightGripSensor がありません。");
                if (root.GetComponentsInChildren<Prize>(true).Length < 1) errors.Add("景品がありません。");
                foreach (Prize prize in root.GetComponentsInChildren<Prize>(true))
                {
                    if (prize.GetComponent<Rigidbody>() == null || prize.GetComponentInChildren<Collider>(true) == null)
                    {
                        errors.Add(prize.name + " にRigidbodyまたはColliderがありません。");
                    }
                }
                MeshCollider[] clawMeshColliders = root.GetComponentsInChildren<MeshCollider>(true);
                if (clawMeshColliders.Length == 0)
                {
                    errors.Add("爪のMeshColliderがありません。");
                }
                foreach (MeshCollider meshCollider in clawMeshColliders)
                {
                    if (!meshCollider.convex)
                    {
                        errors.Add(meshCollider.name + " のMeshColliderがConvexではありません。");
                    }
                }
                ValidateNoNestedClusterItems(root, errors);
                ValidateCraneClearance(root, errors);
                ValidateVerticalPlacement(root, errors);
            }

            if (errors.Count == 0)
            {
                Debug.Log("CraneGame validation PASS: 必須参照、newCrene、景品物理、座標Markerを確認しました。", root);
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
            Transform model = FindDeepChild(root.transform, ModelObjectName);
            Transform playField = FindDeepChild(root.transform, "PlayField");
            Transform top = FindDeepChild(root.transform, "Top");
            List<string> lines = new List<string>();
            AppendTransformDiagnostics(lines, "MODEL", model, root.transform);
            AppendTransformDiagnostics(lines, "PLAYFIELD", playField, root.transform);
            AppendTransformDiagnostics(lines, "CEILING", top, root.transform);
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
            EnsureFolders();
            EnsureCraneModelAsset();
            EnsurePrizeModelAsset();
            string path = BasePath + "/Prefabs/CraneGame.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ReplaceCraneWithNewModel(contents);
                ReplacePrizesWithMagatama(contents);
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
                .FirstOrDefault(x => x.name == ModelObjectName);
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
            instance.name = ModelObjectName;
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate newCrene");
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
            GameObject prizeObject = NewObject(name, parent);
            prizeObject.transform.position = position;
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrizeModelPath);
            if (modelAsset == null)
            {
                throw new InvalidOperationException("magatama model is missing: " + PrizeModelPath);
            }

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, prizeObject.transform);
            visual.name = "MagatamaVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Bounds sourceBounds = CalculateRendererBounds(visual);
            float longest = Mathf.Max(sourceBounds.size.x, Mathf.Max(sourceBounds.size.y, sourceBounds.size.z));
            float scale = longest > 0.0001f ? 0.7f / longest : 1f;
            visual.transform.localScale = Vector3.one * scale;
            Bounds scaledBounds = CalculateRendererBounds(visual);
            visual.transform.position += prizeObject.transform.position - scaledBounds.center;

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }
            ConfigurePrizeMeshColliders(visual);

            Rigidbody body = prizeObject.AddComponent<Rigidbody>();
            body.mass = 0.34f;
            body.linearDamping = 0.35f;
            body.angularDamping = 0.55f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Prize prize = prizeObject.AddComponent<Prize>();
            return prize;
        }

        static void ConfigurePrizeMeshColliders(GameObject visual)
        {
            foreach (Collider existing in visual.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(existing, true);
            }
            foreach (MeshFilter meshFilter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null)
                {
                    continue;
                }
                MeshCollider collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = GetOrCreateColliderMesh(meshFilter.sharedMesh, "Prize_" + meshFilter.name);
                collider.convex = true;
            }
        }

        static Mesh GetOrCreateColliderMesh(Mesh source, string assetName)
        {
            const int maxPoints = 64;
            Vector3[] vertices = source.vertices;
            List<Vector3> points = new List<Vector3>();
            if (vertices.Length > 0)
            {
                int[] extremes = new int[6];
                for (int i = 1; i < vertices.Length; i++)
                {
                    if (vertices[i].x < vertices[extremes[0]].x) extremes[0] = i;
                    if (vertices[i].x > vertices[extremes[1]].x) extremes[1] = i;
                    if (vertices[i].y < vertices[extremes[2]].y) extremes[2] = i;
                    if (vertices[i].y > vertices[extremes[3]].y) extremes[3] = i;
                    if (vertices[i].z < vertices[extremes[4]].z) extremes[4] = i;
                    if (vertices[i].z > vertices[extremes[5]].z) extremes[5] = i;
                }
                foreach (int index in extremes)
                {
                    AddUniquePoint(points, vertices[index]);
                }
                int remaining = Mathf.Max(1, maxPoints - points.Count);
                int stride = Mathf.Max(1, Mathf.CeilToInt(vertices.Length / (float)remaining));
                for (int i = 0; i < vertices.Length && points.Count < maxPoints; i += stride)
                {
                    AddUniquePoint(points, vertices[i]);
                }
            }

            while (points.Count < 4)
            {
                points.Add(points.Count == 0 ? Vector3.zero : points[0] + Vector3.one * (0.0001f * points.Count));
            }
            int[] triangles = new int[(points.Count - 2) * 3];
            for (int i = 0; i < points.Count - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            string safeName = string.Concat(assetName.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
            string path = BasePath + "/Models/Colliders/" + safeName + ".asset";
            Mesh result = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (result == null)
            {
                result = new Mesh { name = safeName };
                AssetDatabase.CreateAsset(result, path);
            }
            else
            {
                result.Clear();
            }
            result.SetVertices(points);
            result.triangles = triangles;
            result.RecalculateBounds();
            EditorUtility.SetDirty(result);
            return result;
        }

        static void AddUniquePoint(List<Vector3> points, Vector3 point)
        {
            if (!points.Contains(point))
            {
                points.Add(point);
            }
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
            foreach (Collider existing in model.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(existing, true);
            }

            // Fit convex mesh colliders only to the existing FBX arm meshes. Convex
            // is required because LiftAssembly owns a Rigidbody and moves at runtime.
            // Decorative meshes stay collider-free so they cannot push prizes away.
            foreach (MeshFilter meshFilter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null ||
                    (!meshFilter.transform.IsChildOf(leftArm) && !meshFilter.transform.IsChildOf(rightArm)))
                {
                    continue;
                }
                MeshCollider physical = meshFilter.gameObject.AddComponent<MeshCollider>();
                physical.sharedMesh = GetOrCreateColliderMesh(meshFilter.sharedMesh, "Claw_" + meshFilter.name);
                physical.convex = true;
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
            EnsureFolders();
            EnsureCraneModelAsset();
            EnsurePrizeModelAsset();
            Transform model = ReplaceCraneWithNewModel(root);
            RepairClusterItemHierarchy(root);
            ApplyGameplaySafetySettings(root);
            EnableClusterPrizeItems(root, true);

            if (model != null)
            {
                Transform anchor = FindDeepChild(root.transform, "GripAnchor");
                Bounds modelBounds = CalculateRendererBounds(model.gameObject);
                if (anchor != null)
                {
                    anchor.position = new Vector3(modelBounds.center.x,
                        modelBounds.min.y + Mathf.Clamp(modelBounds.size.y * 0.13f, 0.08f, 0.28f), modelBounds.center.z);
                }
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

            CraneGrabSequence sequence = root.GetComponent<CraneGrabSequence>();
            Transform playField = FindDeepChild(root.transform, "PlayField");
            if (sequence != null)
            {
                sequence.ConfigureFloorContact(model,
                    playField != null ? playField.GetComponent<Collider>() : null);
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

            EnsurePrizeModelAsset();
            ReplacePrizesWithMagatama(root);
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

        static Transform ReplaceCraneWithNewModel(GameObject root)
        {
            Transform existing = FindDeepChild(root.transform, ModelObjectName);
            if (existing != null)
            {
                return existing;
            }

            Transform oldModel = FindDeepChild(root.transform, "クレーン2");
            Transform lift = FindDeepChild(root.transform, "LiftAssembly");
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (lift == null || asset == null)
            {
                Debug.LogError("CraneGameSetup: newCrene差し替えに必要なLiftAssemblyまたはModelがありません。", root);
                return null;
            }

            Vector3 localPosition = oldModel != null ? oldModel.localPosition : Vector3.zero;
            Quaternion localRotation = oldModel != null ? oldModel.localRotation : Quaternion.identity;
            Vector3 localScale = oldModel != null ? oldModel.localScale : Vector3.one;
            if (oldModel != null)
            {
                UnityEngine.Object.DestroyImmediate(oldModel.gameObject);
            }

            GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(asset, lift);
            replacement.name = ModelObjectName;
            replacement.transform.localPosition = localPosition;
            replacement.transform.localRotation = localRotation;
            replacement.transform.localScale = localScale;
            return replacement.transform;
        }

        static Prize[] ReplacePrizesWithMagatama(GameObject root)
        {
            Prize[] existing = root.GetComponentsInChildren<Prize>(true)
                .OrderBy(prize => prize.name).ToArray();
            if (existing.Length == 0)
            {
                return existing;
            }

            bool alreadyMagatama = existing.Length >= SafeRespawnLocals.Length &&
                existing.All(prize => FindDeepChild(prize.transform, "MagatamaVisual") != null);
            if (alreadyMagatama)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    Prize prize = existing[i];
                    Transform visual = FindDeepChild(prize.transform, "MagatamaVisual");
                    ConfigurePrizeMeshColliders(visual.gameObject);
                    prize.name = "Prize_" + (i + 1);
                    Transform spawn = FindDeepChild(root.transform, "RespawnPosition_" + (i + 1));
                    if (spawn != null)
                    {
                        prize.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                    }
                }
                return existing;
            }

            PrizeRespawner respawner = root.GetComponent<PrizeRespawner>();
            List<Prize> replacements = new List<Prize>();
            foreach (Prize oldPrize in existing)
            {
                Transform parent = oldPrize.transform.parent;
                Vector3 position = oldPrize.transform.position;
                Quaternion rotation = oldPrize.transform.rotation;
                Renderer oldRenderer = oldPrize.GetComponentInChildren<Renderer>(true);
                Material material = oldRenderer != null ? oldRenderer.sharedMaterial : null;
                string prizeName = oldPrize.name;
                UnityEngine.Object.DestroyImmediate(oldPrize.gameObject);

                Prize replacement = CreatePrize(prizeName, position, parent, material);
                replacement.transform.rotation = rotation;
                replacement.Configure(respawner);
                ConfigureClusterPrize(replacement.gameObject);
                replacements.Add(replacement);
            }

            Transform prizesParent = FindDeepChild(root.transform, "Prizes");
            Material fallbackMaterial = AssetDatabase.LoadAssetAtPath<Material>(BasePath + "/Materials/PrizeCoral.mat");
            while (replacements.Count < SafeRespawnLocals.Length && prizesParent != null)
            {
                int index = replacements.Count;
                Transform spawn = FindDeepChild(root.transform, "RespawnPosition_" + (index + 1));
                Vector3 position = spawn != null ? spawn.position : root.transform.TransformPoint(SafeRespawnLocals[index]);
                Prize replacement = CreatePrize("Prize_" + (index + 1), position, prizesParent, fallbackMaterial);
                replacement.Configure(respawner);
                ConfigureClusterPrize(replacement.gameObject);
                replacements.Add(replacement);
            }
            for (int i = 0; i < replacements.Count; i++)
            {
                replacements[i].name = "Prize_" + (i + 1);
                Transform spawn = FindDeepChild(root.transform, "RespawnPosition_" + (i + 1));
                if (spawn != null)
                {
                    replacements[i].transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                }
            }
            return replacements.ToArray();
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

        static void SetSerializedVector3(UnityEngine.Object target, string propertyName, Vector3 value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.vector3Value = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets/MCT_creative_exercises_2026", "CraneGame");
            CreateFolderIfMissing(BasePath, "Materials");
            CreateFolderIfMissing(BasePath, "Models");
            CreateFolderIfMissing(BasePath + "/Models", "Colliders");
            CreateFolderIfMissing(BasePath, "Prefabs");
        }

        static void EnsurePrizeModelAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrizeModelPath) != null)
            {
                EnsureModelReadable(PrizeModelPath);
                return;
            }
            if (System.IO.File.Exists(PrizeModelPath))
            {
                AssetDatabase.ImportAsset(PrizeModelPath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrizeModelPath) != null)
                {
                    EnsureModelReadable(PrizeModelPath);
                    return;
                }
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrizeModelSourcePath) == null)
            {
                throw new InvalidOperationException("magatama source model is missing: " + PrizeModelSourcePath);
            }
            if (!AssetDatabase.CopyAsset(PrizeModelSourcePath, PrizeModelPath))
            {
                throw new InvalidOperationException("Failed to copy magatama model to " + PrizeModelPath);
            }
            AssetDatabase.ImportAsset(PrizeModelPath, ImportAssetOptions.ForceSynchronousImport);
            EnsureModelReadable(PrizeModelPath);
        }

        static void EnsureCraneModelAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null)
            {
                EnsureModelReadable(ModelPath);
                return;
            }
            if (System.IO.File.Exists(ModelPath))
            {
                AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null)
                {
                    EnsureModelReadable(ModelPath);
                    return;
                }
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CraneModelSourcePath) == null)
            {
                throw new InvalidOperationException("newCrene source model is missing: " + CraneModelSourcePath);
            }
            if (!AssetDatabase.CopyAsset(CraneModelSourcePath, ModelPath))
            {
                throw new InvalidOperationException("Failed to copy newCrene model to " + ModelPath);
            }
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            EnsureModelReadable(ModelPath);
        }

        static void EnsureModelReadable(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
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
            ApplyScaledLayoutSettings(root);
            RepairRespawnLayout(root);

            Transform model = FindDeepChild(root.transform, ModelObjectName);
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
                SetSerializedFloat(sequence, "lowerDistance", 3.65f);
                SetSerializedFloat(sequence, "minimumGripHeight", 0.46f);
                SetSerializedFloat(sequence, "floorClearance", 0.01f);
                Transform modelForFloor = FindDeepChild(root.transform, ModelObjectName);
                Transform playField = FindDeepChild(root.transform, "PlayField");
                sequence.ConfigureFloorContact(modelForFloor,
                    playField != null ? playField.GetComponent<Collider>() : null);
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

            PrizeRespawner respawner = root.GetComponent<PrizeRespawner>();
            if (respawner != null)
            {
                SetSerializedVector3(respawner, "localBoundsCenter", new Vector3(0f, 1.8f, 0f));
                SetSerializedVector3(respawner, "localBoundsSize", new Vector3(5.7f, 5.65f, 4.2f));
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
            Transform model = FindDeepChild(root.transform, ModelObjectName);
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

            // Cabinet inner faces are approximately X +/-1.71 and Z +/-1.59
            // before applying the 1.5x cabinet layout scale.
            // Keep an additional margin for collider thickness and network interpolation.
            const float innerX = 1.63f * CabinetScale;
            const float innerZ = 1.50f * CabinetScale;
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

        static void ValidateVerticalPlacement(GameObject root, List<string> errors)
        {
            Transform model = FindDeepChild(root.transform, ModelObjectName);
            Transform ceiling = FindDeepChild(root.transform, "Top");
            Transform playField = FindDeepChild(root.transform, "PlayField");
            Renderer ceilingRenderer = ceiling != null ? ceiling.GetComponent<Renderer>() : null;
            Collider floorCollider = playField != null ? playField.GetComponent<Collider>() : null;
            if (model == null || ceilingRenderer == null || floorCollider == null)
            {
                errors.Add("天井・床到達検証に必要なModel、Top、PlayFieldのいずれかがありません。");
                return;
            }

            Bounds modelBounds = CalculateRendererBounds(model.gameObject);
            float ceilingGap = ceilingRenderer.bounds.min.y - modelBounds.max.y;
            float requiredLowerDistance = modelBounds.min.y - floorCollider.bounds.max.y - 0.01f;
            if (Mathf.Abs(ceilingGap) > 0.02f)
            {
                errors.Add(string.Format("クレーン上端が天井内面と一致しません: gap={0:F3}", ceilingGap));
            }
            if (requiredLowerDistance < 0f || requiredLowerDistance > 3.65f)
            {
                errors.Add(string.Format("爪先を床へ合わせる下降量が範囲外です: distance={0:F3}", requiredLowerDistance));
            }
            if (Mathf.Abs(ceilingGap) <= 0.02f && requiredLowerDistance >= 0f && requiredLowerDistance <= 3.65f)
            {
                Debug.Log(string.Format("Crane vertical placement PASS: ceilingGap={0:F3}, floorTravel={1:F3}",
                    ceilingGap, requiredLowerDistance), root);
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

            // Keep the portable prefab root at (1,1,1). Scaling this self-contained
            // group enlarges both the cabinet meshes and their BoxColliders together.
            cabinet.localScale = Vector3.one * CabinetScale;
        }

        static void ApplyScaledLayoutSettings(GameObject root)
        {
            Transform controls = FindDeepChild(root.transform, "Controls");
            if (controls != null)
            {
                controls.localPosition = new Vector3(0f, 0f, -1.58f * CabinetScale);
                // Keep the controls at their original vertical size so the top stays
                // within a comfortable desktop/VR sight line.
                controls.localScale = new Vector3(CabinetScale, 1f, CabinetScale);
            }

            Transform chute = FindDeepChild(root.transform, "PrizeChute");
            if (chute != null)
            {
                chute.localPosition = new Vector3(SafeDropXZ.x, 0f, SafeDropXZ.y);
                chute.localScale = Vector3.one * CabinetScale;
            }

            Transform guide = FindDeepChild(root.transform, "GuideDisplay");
            if (guide != null)
            {
                guide.localPosition = new Vector3(0f, 2.82f * CabinetScale, -1.34f * CabinetScale);
                guide.localScale = Vector3.one * CabinetScale;
            }

            Transform lightTransform = FindDeepChild(root.transform, "CraneInteriorLight");
            if (lightTransform != null)
            {
                lightTransform.localPosition = new Vector3(0f, 2.95f * CabinetScale, -0.2f * CabinetScale);
                Light light = lightTransform.GetComponent<Light>();
                if (light != null)
                {
                    light.range = 6f * CabinetScale;
                }
            }

            SetLocalY(FindDeepChild(root.transform, "Carriage"), FallbackCarriageHeight);
            SetLocalY(FindDeepChild(root.transform, "HomePosition"), FallbackCarriageHeight);
            SetLocalY(FindDeepChild(root.transform, "DropPosition"), FallbackCarriageHeight);
            AlignCraneToCeiling(root);
        }

        static void AlignCraneToCeiling(GameObject root)
        {
            Transform carriage = FindDeepChild(root.transform, "Carriage");
            Transform model = FindDeepChild(root.transform, ModelObjectName);
            Transform ceiling = FindDeepChild(root.transform, "Top");
            if (carriage == null || model == null || ceiling == null)
            {
                return;
            }

            Renderer ceilingRenderer = ceiling.GetComponent<Renderer>();
            if (ceilingRenderer == null)
            {
                return;
            }
            Bounds modelBounds = CalculateRendererBounds(model.gameObject);
            float deltaY = ceilingRenderer.bounds.min.y - modelBounds.max.y;
            Vector3 carriageLocal = carriage.localPosition;
            carriageLocal.y += deltaY;
            carriage.localPosition = carriageLocal;
            SetLocalY(FindDeepChild(root.transform, "HomePosition"), carriageLocal.y);
            SetLocalY(FindDeepChild(root.transform, "DropPosition"), carriageLocal.y);
        }

        static void SetLocalY(Transform target, float y)
        {
            if (target == null)
            {
                return;
            }
            Vector3 local = target.localPosition;
            local.y = y;
            target.localPosition = local;
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
