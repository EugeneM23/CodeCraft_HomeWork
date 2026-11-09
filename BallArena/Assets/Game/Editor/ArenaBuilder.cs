using System.Collections.Generic;
using System.IO;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BallArena.EditorTools
{
    /// <summary>
    /// Собирает сцену, префабы и материалы из кода. Запускается из меню или из
    /// командной строки через -executeMethod BallArena.EditorTools.ArenaBuilder.Build.
    ///
    /// Почему из кода, а не руками: сборка повторяема, и ровно те настройки, без которых
    /// Fusion молча не работает, проставляются в одном проверяемом месте.
    /// </summary>
    public static class ArenaBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/Game.unity";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Player.prefab";
        private const string BallPrefabPath = "Assets/Game/Prefabs/Ball.prefab";
        private const string MatchPrefabPath = "Assets/Game/Prefabs/Match.prefab";
        private const string MaterialsFolder = "Assets/Game/Materials";

        private const float FieldHalfX = 12f;
        private const float FieldHalfZ = 7f;
        private const float WallHeight = 2f;
        private const float WallThickness = 0.5f;
        private const float GoalHalfWidth = 3f;
        private const float GoalDepth = 2f;

        private static readonly Vector3 GoalZoneHalfExtents = new Vector3(1f, 2f, 3.2f);

        [MenuItem("BallArena/Собрать арену")]
        public static void Build()
        {
            EnsureFolders();

            Material floorMaterial = CreateMaterial("Floor", new Color(0.18f, 0.42f, 0.22f));
            Material wallMaterial = CreateMaterial("Wall", new Color(0.78f, 0.78f, 0.80f));
            Material goalLeftMaterial = CreateMaterial("GoalLeft", new Color(0.25f, 0.55f, 1f));
            Material goalRightMaterial = CreateMaterial("GoalRight", new Color(1f, 0.45f, 0.2f));
            Material ballMaterial = CreateMaterial("Ball", new Color(0.95f, 0.95f, 0.95f));
            Material bodyMaterial = CreateMaterial("Body", Color.white);

            GameObject playerPrefab = BuildPlayerPrefab(bodyMaterial);
            GameObject ballPrefab = BuildBallPrefab(ballMaterial);
            GameObject matchPrefab = BuildMatchPrefab(ballPrefab);

            // Таблица сетевых префабов строится по ЯРЛЫКУ ассета, который ставит эта
            // утилита. Префаб, созданный кодом, ярлыка не получает, и Runner.Spawn
            // падает с «guid failed to be translated into a prefab id».
            NetworkProjectConfigUtilities.RebuildPrefabTable();

            BuildScene(playerPrefab, matchPrefab, floorMaterial, wallMaterial, goalLeftMaterial, goalRightMaterial);

            ConfigureProject();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ArenaBuilder] Готово.");
        }

        // ------------------------------------------------------------------ папки

        private static void EnsureFolders()
        {
            foreach (string path in new[]
                     {
                         "Assets/Game", "Assets/Game/Scenes", "Assets/Game/Prefabs", MaterialsFolder,
                     })
            {
                if (!AssetDatabase.IsValidFolder(path))
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
            }
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ----------------------------------------------------------------- префабы

        private static GameObject BuildPlayerPrefab(Material bodyMaterial)
        {
            var root = new GameObject("Player");

            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.5f;
            collider.height = 2f;
            collider.center = Vector3.zero;

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1.2f;
            body.linearDamping = 1.5f;
            body.angularDamping = 1f;
            body.useGravity = true;
            body.isKinematic = false;
            // Сглаживание делает NetworkRigidbody3D через InterpolationTarget, не Unity.
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            root.AddComponent<NetworkObject>();

            GameObject visual = CreatePrimitiveVisual("Visual", PrimitiveType.Capsule, root.transform, bodyMaterial);

            GameObject nameplateRoot = new GameObject("Nameplate");
            nameplateRoot.transform.SetParent(visual.transform, false);
            nameplateRoot.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            TextMesh textMesh = CreateTextMesh(nameplateRoot);
            var nameplate = nameplateRoot.AddComponent<Nameplate>();
            SetPrivateField(nameplate, "_text", textMesh);

            // NetworkRigidbody3D вместо NetworkTransform: NetworkRigidbodyBase сам носитель
            // TRSP-состояния, второй компонент был бы лишним.
            var networkBody = root.AddComponent<NetworkRigidbody3D>();
            networkBody.InterpolationTarget = visual.transform;

            var motor = root.AddComponent<PlayerMotor>();
            SetPrivateField(motor, "_rigidbody", body);

            var identity = root.AddComponent<PlayerIdentity>();
            SetPrivateField(identity, "_bodyRenderer", visual.GetComponent<MeshRenderer>());
            SetPrivateField(identity, "_nameplate", nameplate);

            return SavePrefab(root, PlayerPrefabPath);
        }

        private static GameObject BuildBallPrefab(Material ballMaterial)
        {
            var root = new GameObject("Ball");

            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.5f;

            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.linearDamping = 0.35f;
            body.angularDamping = 0.35f;
            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            root.AddComponent<NetworkObject>();

            GameObject visual = CreatePrimitiveVisual("Visual", PrimitiveType.Sphere, root.transform, ballMaterial);

            var networkBody = root.AddComponent<NetworkRigidbody3D>();
            networkBody.InterpolationTarget = visual.transform;

            var ball = root.AddComponent<Ball>();
            SetPrivateField(ball, "_rigidbody", body);
            SetPrivateField(ball, "_networkRigidbody", networkBody);

            return SavePrefab(root, BallPrefabPath);
        }

        private static GameObject BuildMatchPrefab(GameObject ballPrefab)
        {
            var root = new GameObject("Match");
            root.AddComponent<NetworkObject>();

            var match = root.AddComponent<MatchManager>();
            SetPrivateField(match, "_ballPrefab", ballPrefab.GetComponent<NetworkObject>());
            SetPrivateField(match, "_goalHalfExtents", GoalZoneHalfExtents);

            return SavePrefab(root, MatchPrefabPath);
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            // Реимпорт запускает NetworkObjectPostprocessor: он проставляет
            // NetworkObjectPrefabData (guid, по которому Spawn находит префаб) и
            // перепекает список NetworkedBehaviours. Без этого Spawn молча не найдёт префаб.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // -------------------------------------------------------------------- сцена

        private static void BuildScene(
            GameObject playerPrefab,
            GameObject matchPrefab,
            Material floorMaterial,
            Material wallMaterial,
            Material goalLeftMaterial,
            Material goalRightMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- свет и камера

            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(0f, 19f, -13f);
            cameraObject.transform.LookAt(Vector3.zero);

            // --- арена

            var arena = new GameObject("[Arena]");

            CreateBox(arena.transform, "Floor",
                new Vector3(0f, -0.25f, 0f),
                new Vector3(FieldHalfX * 2f, 0.5f, FieldHalfZ * 2f),
                floorMaterial);

            // Длинные борта
            for (int sign = -1; sign <= 1; sign += 2)
            {
                CreateBox(arena.transform, sign < 0 ? "Wall -Z" : "Wall +Z",
                    new Vector3(0f, WallHeight * 0.5f, sign * (FieldHalfZ + WallThickness * 0.5f)),
                    new Vector3(FieldHalfX * 2f + WallThickness * 2f, WallHeight, WallThickness),
                    wallMaterial);
            }

            // Короткие борта с проёмом под ворота + коробка ворот за проёмом
            Transform goalLeft = BuildGoal(arena.transform, -1, wallMaterial, goalLeftMaterial);
            Transform goalRight = BuildGoal(arena.transform, +1, wallMaterial, goalRightMaterial);

            // --- точки старта

            var points = new GameObject("[SpawnPoints]");
            Transform pointLeft = CreatePoint(points.transform, "Player 1", new Vector3(-6f, 1f, 0f), Quaternion.Euler(0f, 90f, 0f));
            Transform pointRight = CreatePoint(points.transform, "Player 2", new Vector3(6f, 1f, 0f), Quaternion.Euler(0f, -90f, 0f));
            Transform ballPoint = CreatePoint(points.transform, "Ball", new Vector3(0f, 0.6f, 0f), Quaternion.identity);

            var spawnPoints = points.AddComponent<SpawnPointService>();
            SetPrivateField(spawnPoints, "_pointLeft", pointLeft);
            SetPrivateField(spawnPoints, "_pointRight", pointRight);

            // Маркер сцены: префаб матча не может ссылаться на объекты сцены,
            // поэтому находит их через этот компонент.
            var markers = points.AddComponent<ArenaMarkers>();
            SetPrivateField(markers, "_ballSpawnPoint", ballPoint);
            SetPrivateField(markers, "_goalLeft", goalLeft);
            SetPrivateField(markers, "_goalRight", goalRight);
            SetPrivateField(markers, "_spawnPoints", spawnPoints);

            // --- UI

            UiRefs ui = BuildUi();

            var scoreView = ui.ScoreLabel.gameObject.AddComponent<ScoreView>();
            SetPrivateField(scoreView, "_label", ui.ScoreLabel);

            // --- системный стек

            var system = new GameObject("[System]");
            var runner = system.AddComponent<NetworkRunner>();
            runner.ProvideInput = true;

            var sceneManager = system.AddComponent<NetworkSceneManagerDefault>();

            // NetworkEvents обязан лежать на том же GameObject, что NetworkRunner —
            // это документированный контракт Fusion. Перенесёшь — ввод и спавн
            // молча перестанут работать.
            var events = system.AddComponent<NetworkEvents>();

            ConfigurePhysicsRunner(system);

            var entryPoint = system.AddComponent<EntryPoint>();
            SetPrivateField(entryPoint, "_runner", runner);
            SetPrivateField(entryPoint, "_sceneManager", sceneManager);
            SetPrivateField(entryPoint, "_connectPanel", ui.ConnectPanel);
            SetPrivateField(entryPoint, "_roomCodeField", ui.RoomCodeField);
            SetPrivateField(entryPoint, "_nickField", ui.NickField);
            SetPrivateField(entryPoint, "_playButton", ui.PlayButton);
            SetPrivateField(entryPoint, "_statusText", ui.StatusText);
            SetPrivateField(entryPoint, "_hudText", ui.HudText);

            var accumulator = system.AddComponent<InputAccumulator>();
            SetPrivateField(accumulator, "_networkEvents", events);

            var spawner = system.AddComponent<PlayerSpawner>();
            SetPrivateField(spawner, "_networkEvents", events);
            SetPrivateField(spawner, "_spawnPoints", spawnPoints);
            SetPrivateField(spawner, "_playerPrefab", playerPrefab.GetComponent<NetworkObject>());
            SetPrivateField(spawner, "_matchPrefab", matchPrefab.GetComponent<NetworkObject>());

            // Сохранение сцены запускает OnSceneSaving → бейк сценовых NetworkObject.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Transform BuildGoal(Transform parent, int sign, Material wallMaterial, Material goalMaterial)
        {
            float wallX = sign * (FieldHalfX + WallThickness * 0.5f);
            float segmentLength = FieldHalfZ - GoalHalfWidth;
            float segmentCenter = GoalHalfWidth + segmentLength * 0.5f;
            string side = sign < 0 ? "Left" : "Right";

            for (int zSign = -1; zSign <= 1; zSign += 2)
            {
                CreateBox(parent, $"Goal {side} / Post {(zSign < 0 ? "-Z" : "+Z")}",
                    new Vector3(wallX, WallHeight * 0.5f, zSign * segmentCenter),
                    new Vector3(WallThickness, WallHeight, segmentLength),
                    wallMaterial);
            }

            // Коробка за проёмом, чтобы мяч не укатился в бесконечность.
            float backX = sign * (FieldHalfX + GoalDepth + WallThickness * 0.5f);

            CreateBox(parent, $"Goal {side} / Back",
                new Vector3(backX, WallHeight * 0.5f, 0f),
                new Vector3(WallThickness, WallHeight, GoalHalfWidth * 2f + WallThickness * 2f),
                goalMaterial);

            for (int zSign = -1; zSign <= 1; zSign += 2)
            {
                CreateBox(parent, $"Goal {side} / Side {(zSign < 0 ? "-Z" : "+Z")}",
                    new Vector3(sign * (FieldHalfX + GoalDepth * 0.5f + WallThickness * 0.5f), WallHeight * 0.5f, zSign * (GoalHalfWidth + WallThickness * 0.5f)),
                    new Vector3(GoalDepth, WallHeight, WallThickness),
                    goalMaterial);
            }

            // Центр зоны зачёта: заведомо ЗА плоскостью короткого борта, поэтому
            // попасть туда можно только через проём.
            return CreatePoint(parent, $"Goal {side} / Zone",
                new Vector3(sign * (FieldHalfX + GoalDepth * 0.65f), 1f, 0f),
                Quaternion.identity);
        }

        private static void CreateBox(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            box.isStatic = true;
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent, false);
            point.transform.localPosition = position;
            point.transform.localRotation = rotation;
            return point.transform;
        }

        private static GameObject CreatePrimitiveVisual(string name, PrimitiveType type, Transform parent, Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);

            // Коллайдер живёт на корне: он должен стоять в тиковой позиции,
            // а визуал интерполируется.
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            return visual;
        }

        private static TextMesh CreateTextMesh(GameObject host)
        {
            var textMesh = host.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textMesh.font = font;
            textMesh.fontSize = 64;
            textMesh.characterSize = 0.12f;
            textMesh.anchor = TextAnchor.LowerCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
            textMesh.text = "Игрок";

            host.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return textMesh;
        }

        private static void ConfigurePhysicsRunner(GameObject system)
        {
            // Этот компонент ставится руками. Не положить его — NetworkRigidbody добавит
            // свой с ClientPhysicsSimulation = Disabled, и клиентское предсказание
            // физики умрёт молча.
            var physics = system.AddComponent<RunnerSimulatePhysics3D>();
            physics.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateAlways;
            physics.DeltaTimeMultiplier = 1f;
            physics.SetUnityFixedTimestep = false;

            var serialized = new SerializedObject(physics);
            serialized.FindProperty("_physicsAuthority").enumValueIndex = (int)PhysicsAuthorities.Fusion;
            serialized.FindProperty("_physicsTiming").enumValueIndex = (int)PhysicsTimings.FixedUpdateNetwork;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----------------------------------------------------------------------- UI

        private sealed class UiRefs
        {
            public GameObject ConnectPanel;
            public InputField RoomCodeField;
            public InputField NickField;
            public Button PlayButton;
            public Text StatusText;
            public Text ScoreLabel;
            public Text HudText;
        }

        private static UiRefs BuildUi()
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasObject.AddComponent<GraphicRaycaster>();

            var refs = new UiRefs();

            // --- счёт

            refs.ScoreLabel = CreateText(canvasObject.transform, "ScoreLabel", "0 : 0", 56, TextAnchor.UpperCenter);
            Anchor(refs.ScoreLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(400f, 90f));

            // --- строка состояния поверх игры
            // Прямой ребёнок Canvas, а НЕ ConnectPanel: панель после входа гасится,
            // и всё, что лежит внутри неё, игрок больше никогда не увидит.
            refs.HudText = CreateText(canvasObject.transform, "HudText", string.Empty, 22, TextAnchor.LowerCenter);
            refs.HudText.color = new Color(1f, 0.85f, 0.45f);
            Anchor(refs.HudText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(900f, 40f));

            // --- панель подключения

            var panel = new GameObject("ConnectPanel");
            panel.transform.SetParent(canvasObject.transform, false);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.72f);
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 400f));
            refs.ConnectPanel = panel;

            Text title = CreateText(panel.transform, "Title", $"BallArena {EntryPoint.Version}", 40, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(460f, 60f));

            Text hint = CreateText(panel.transform, "Hint", "WASD — движение, Shift — рывок", 20, TextAnchor.MiddleCenter);
            hint.color = new Color(0.75f, 0.75f, 0.75f);
            Anchor(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(460f, 40f));

            refs.NickField = CreateInputField(panel.transform, "NickField", "Твоё имя");
            Anchor(refs.NickField.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(420f, 50f));

            refs.RoomCodeField = CreateInputField(panel.transform, "RoomCodeField", "Код комнаты — одинаковый у обоих");
            Anchor(refs.RoomCodeField.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(420f, 50f));

            refs.PlayButton = CreateButton(panel.transform, "PlayButton", "Играть");
            Anchor(refs.PlayButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -275f), new Vector2(420f, 56f));

            // Снизу, не залезая на кнопку: кнопка занимает -131…-75 по локальной Y.
            refs.StatusText = CreateText(panel.transform, "StatusText", string.Empty, 18, TextAnchor.UpperCenter);
            refs.StatusText.color = new Color(1f, 0.8f, 0.4f);
            Anchor(refs.StatusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(470f, 58f));

            return refs;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, anchor.y);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            // Подписи не должны ловить клики: Text по умолчанию raycast target, и
            // надпись, лежащая поверх кнопки, молча забирает все нажатия себе.
            text.raycastTarget = false;

            return text;
        }

        private static InputField CreateInputField(Transform parent, string name, string placeholder)
        {
            var fieldObject = new GameObject(name);
            fieldObject.transform.SetParent(parent, false);
            var image = fieldObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.92f);

            var field = fieldObject.AddComponent<InputField>();

            Text placeholderText = CreateText(fieldObject.transform, "Placeholder", placeholder, 18, TextAnchor.MiddleLeft);
            placeholderText.color = new Color(0.45f, 0.45f, 0.45f);
            Stretch(placeholderText.rectTransform, 12f);

            Text valueText = CreateText(fieldObject.transform, "Text", string.Empty, 20, TextAnchor.MiddleLeft);
            valueText.color = new Color(0.08f, 0.08f, 0.08f);
            valueText.supportRichText = false;
            Stretch(valueText.rectTransform, 12f);

            field.targetGraphic = image;
            field.textComponent = valueText;
            field.placeholder = placeholderText;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 24;
            return field;
        }

        private static Button CreateButton(Transform parent, string name, string caption)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.62f, 0.95f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text label = CreateText(buttonObject.transform, "Text", caption, 24, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 0f);

            return button;
        }

        private static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, 0f);
            rect.offsetMax = new Vector2(-padding, 0f);
        }

        // ------------------------------------------------------------ настройки проекта

        private static void ConfigureProject()
        {
            // Сцена обязана быть в Build Settings: без неё SceneRef.FromIndex(buildIndex)
            // не сработает.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "BallArena";
            PlayerSettings.productName = "BallArena";

            // Поле Version в Player Settings. В свойствах exe его НЕ видно: Windows
            // показывает там версию Unity (проверено — FileVersion 6000.0.38.*).
            // Единственное надёжное место, где игрок видит номер сборки, — экран.
            PlayerSettings.bundleVersion = EntryPoint.Version.TrimStart('v');

            // Два окна на одной машине: без runInBackground неактивное окно встанет.
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
        }

        // ------------------------------------------------------------------ рефлексия

        private static void SetPrivateField(Object target, string fieldName, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[ArenaBuilder] У {target.GetType().Name} нет поля {fieldName}.");
                return;
            }

            switch (value)
            {
                case Object unityObject:
                    property.objectReferenceValue = unityObject;
                    break;
                case Vector3 vector:
                    property.vector3Value = vector;
                    break;
                default:
                    Debug.LogError($"[ArenaBuilder] Не умею присваивать {value?.GetType().Name} в {fieldName}.");
                    return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
