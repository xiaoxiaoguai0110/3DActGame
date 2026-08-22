using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 将中世纪主菜单生成到 0-GameMenu，将战斗 HUD 生成到 1-GameScene。
/// </summary>
public static class PolishUIEditor
{
    private const string MenuScenePath = "Assets/Scenes/0-GameMenu.unity";
    private const string GameScenePath = "Assets/Scenes/1-GameScene.unity";

    private static readonly Color Ink = new(0.025f, 0.032f, 0.027f, 1f);
    private static readonly Color Panel = new(0.055f, 0.062f, 0.052f, 0.96f);
    private static readonly Color PanelSoft = new(0.095f, 0.105f, 0.085f, 0.92f);
    private static readonly Color Accent = new(0.62f, 0.47f, 0.23f, 1f);
    private static readonly Color Moss = new(0.27f, 0.32f, 0.24f, 1f);
    private static readonly Color Parchment = new(0.79f, 0.75f, 0.64f, 1f);
    private static readonly Color TextPrimary = new(0.88f, 0.86f, 0.78f, 1f);
    private static readonly Color TextSecondary = new(0.57f, 0.58f, 0.51f, 1f);

    [InitializeOnLoadMethod]
    private static void ScheduleThemeMigration()
    {
        // 编辑器正处于 Play Mode 时不会安全重建场景；退出播放并完成脚本重载后再尝试。
        EditorApplication.delayCall += TryApplyThemeMigration;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += TryApplyThemeMigration;
    }

    private static void TryApplyThemeMigration()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if ((activeScene.path != MenuScenePath && activeScene.path != GameScenePath) || activeScene.isDirty)
            return;

        bool menuNeedsMigration = activeScene.path == MenuScenePath && FindSceneObject(MenuScenePath, "MenuUIRoot") == null;
        bool gameNeedsMigration = activeScene.path == GameScenePath
            && (FindSceneObject(GameScenePath, "PortfolioUIRoot") != null || FindSceneObject(GameScenePath, "MainMenuUI") != null);
        if (menuNeedsMigration || gameNeedsMigration)
            Apply();
    }

    [MenuItem("Tools/3DActGame/Polish Game UI")]
    public static void Apply()
    {
        BuildGameHudScene();

        // UI 拆场景时顺便确保前一阶段的敌人生成器已经真正写入战斗场景。
        if (Object.FindObjectOfType<EnemySpawner>() == null)
            EnemySpawnerEditor.Apply();

        BuildMenuScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[PolishUIEditor] 菜单已迁移到 0-GameMenu，1-GameScene 仅保留战斗 HUD。");
    }

    [MenuItem("Tools/3DActGame/Validate Game UI")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        GameObject menuUiRoot = FindSceneObject(MenuScenePath, "MenuUIRoot");
        MainMenuUI menuController = Object.FindObjectOfType<MainMenuUI>();
        if (menuUiRoot == null || menuController == null)
            throw new MissingReferenceException("0-GameMenu 缺少 MenuUIRoot 或 MainMenuUI。");

        Button[] buttons = menuUiRoot.GetComponentsInChildren<Button>(true);
        if (buttons.Length != 2)
            throw new UnityException($"菜单按钮数量异常：Buttons={buttons.Length}。");
        foreach (Button button in buttons)
        {
            if (button.onClick.GetPersistentEventCount() != 1)
                throw new UnityException($"按钮 {button.name} 没有正确绑定唯一事件。");
        }

        SerializedObject serializedMenu = new(menuController);
        GameObject menuRoot = serializedMenu.FindProperty("m_MenuRoot").objectReferenceValue as GameObject;
        if (menuRoot == null || menuRoot.name != "MenuRoot")
            throw new MissingReferenceException("MainMenuUI.m_MenuRoot 没有指向 MenuRoot。");

        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        GameObject gameHudRoot = FindSceneObject(GameScenePath, "GameHUDRoot");
        if (gameHudRoot == null)
            throw new MissingReferenceException("1-GameScene 缺少 GameHUDRoot。");

        CombatHUDController controller = gameHudRoot.GetComponent<CombatHUDController>();
        if (controller == null)
            throw new MissingComponentException("GameHUDRoot 缺少 CombatHUDController。");

        SerializedObject serializedHud = new(controller);
        RequireReference(serializedHud, "m_HudRoot");
        RequireReference(serializedHud, "m_LegacyHudRoot");

        GameObject legacyHudRoot = serializedHud.FindProperty("m_LegacyHudRoot").objectReferenceValue as GameObject;
        if (legacyHudRoot == null || legacyHudRoot.GetComponentInChildren<HealthyUI>(true) == null)
            throw new MissingComponentException("原版血量 HUD 缺少 HealthyUI，玩家血条无法更新。");

        if (Object.FindObjectOfType<MainMenuUI>() != null)
            throw new UnityException("1-GameScene 不应再包含 MainMenuUI。");
        if (Object.FindObjectOfType<GameSceneBootstrap>() == null)
            throw new MissingComponentException("1-GameScene 缺少 GameSceneBootstrap。");

        Debug.Log("[PolishUIEditor] 双场景 UI 验证通过：菜单与战斗 HUD 职责已分离。");
    }

    [MenuItem("Tools/3DActGame/Capture UI Previews")]
    public static void CapturePreviews()
    {
        Directory.CreateDirectory("Logs");

        EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        GameObject menuUiRoot = FindSceneObject(MenuScenePath, "MenuUIRoot");
        if (menuUiRoot == null)
            throw new MissingReferenceException("0-GameMenu 缺少 MenuUIRoot。");
        CaptureSingleCanvas(menuUiRoot.GetComponent<Canvas>(), "Logs/UI_Menu_Preview.png");

        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        GameObject gameHudRoot = FindSceneObject(GameScenePath, "GameHUDRoot");
        GameObject legacyHudRoot = FindSceneObject(GameScenePath, "LegacyCanvasHUD");
        if (legacyHudRoot == null)
            throw new MissingReferenceException("没有找到 LegacyCanvasHUD，无法生成包含原版血条的预览。");

        Canvas canvas = gameHudRoot.GetComponent<Canvas>();
        Canvas legacyCanvas = legacyHudRoot.GetComponent<Canvas>();
        CaptureHudCanvas(canvas, legacyCanvas, legacyHudRoot, "Logs/UI_HUD_Preview.png");
        Debug.Log("[PolishUIEditor] UI 预览已输出到 Logs/UI_Menu_Preview.png 和 Logs/UI_HUD_Preview.png。");
    }

    private static void BuildMenuScene()
    {
        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        DestroySceneObject(MenuScenePath, "MenuUIRoot");

        MainMenuUI menuController = Object.FindObjectOfType<MainMenuUI>();
        if (menuController == null)
            menuController = new GameObject("MainMenuUI").AddComponent<MainMenuUI>();

        GameObject uiRoot = CreateCanvasRoot("MenuUIRoot");
        GameObject menuRoot = BuildMainMenu(uiRoot.transform, menuController);
        CreateUIObject("MedievalThemeMarker", uiRoot.transform, typeof(RectTransform));
        ReplaceMainMenuRoot(menuController, menuRoot);
        EnsureEventSystem();

        Camera camera = Object.FindObjectOfType<Camera>();
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildGameHudScene()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        DestroySceneObject(GameScenePath, "PortfolioUIRoot");
        DestroySceneObject(GameScenePath, "GameHUDRoot");
        DestroySceneObject(GameScenePath, "LegacyMenuRoot_Disabled");
        DestroySceneObject(GameScenePath, "GameSceneBootstrap");

        foreach (MainMenuUI oldMenuController in Object.FindObjectsOfType<MainMenuUI>(true))
            Object.DestroyImmediate(oldMenuController.gameObject);

        GameObject legacyHudRoot = ConfigureLegacyHud();
        GameObject uiRoot = CreateCanvasRoot("GameHUDRoot");
        HudReferences hud = BuildHud(uiRoot.transform);
        BindRuntimeController(uiRoot, hud, legacyHudRoot);

        GameSceneBootstrap bootstrap = new GameObject("GameSceneBootstrap").AddComponent<GameSceneBootstrap>();
        SerializedObject serializedBootstrap = new(bootstrap);
        serializedBootstrap.FindProperty("m_PlayerIntro").objectReferenceValue = Object.FindObjectOfType<Player>();
        serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject ConfigureLegacyHud()
    {
        // GameObject.Find 看不到未激活物体；Resources 查询可找回之前作为备份关闭的 Canvas。
        GameObject oldHud = FindSceneObject(GameScenePath, "LegacyCanvasHUD_Disabled", "CanvasHUD", "LegacyCanvasHUD");
        if (oldHud == null)
            throw new MissingReferenceException("场景中没有找到原版 CanvasHUD，无法恢复原版血量 UI。");

        oldHud.name = "LegacyCanvasHUD";
        oldHud.SetActive(true);
        return oldHud;
    }

    private static void DestroySceneObject(string scenePath, string objectName)
    {
        GameObject target = FindSceneObject(scenePath, objectName);
        if (target != null)
            Object.DestroyImmediate(target);
    }

    private static GameObject FindSceneObject(string scenePath, params string[] names)
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!candidate.scene.IsValid() || candidate.scene.path != scenePath)
                continue;

            foreach (string objectName in names)
            {
                if (candidate.name == objectName)
                    return candidate;
            }
        }

        return null;
    }

    private static GameObject CreateCanvasRoot(string rootName)
    {
        GameObject root = new(rootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return root;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static GameObject BuildMainMenu(Transform parent, MainMenuUI menuController)
    {
        GameObject menuRoot = CreatePanel("MenuRoot", parent, new Color(Ink.r, Ink.g, Ink.b, 0.88f));
        Stretch(menuRoot.GetComponent<RectTransform>());

        GameObject leftShade = CreatePanel("LeftShade", menuRoot.transform, new Color(0f, 0f, 0f, 0.28f));
        SetRect(leftShade.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0.22f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
        GameObject rightShade = CreatePanel("RightShade", menuRoot.transform, new Color(0f, 0f, 0f, 0.28f));
        SetRect(rightShade.GetComponent<RectTransform>(), new Vector2(0.78f, 0f), Vector2.one, Vector2.zero, Vector2.zero, new Vector2(1f, 0.5f));

        GameObject chapterPanel = CreateFramedPanel("ChapterPanel", menuRoot.transform, new Color(Panel.r, Panel.g, Panel.b, 0.82f), new Color(Accent.r, Accent.g, Accent.b, 0.58f));
        SetRect(chapterPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(820f, 850f), new Vector2(0.5f, 0.5f));

        TMP_Text chapter = CreateText("Chapter", chapterPanel.transform, "CHAPTER  I", 17f, FontStyles.Bold, Accent, TextAlignmentOptions.Center);
        chapter.characterSpacing = 9f;
        SetRect(chapter.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -66f), new Vector2(-80f, 32f), new Vector2(0.5f, 1f));

        TMP_Text title = CreateText("Title", chapterPanel.transform, "THE FLOODED\nGROUNDS", 70f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.Center);
        title.characterSpacing = 2f;
        title.lineSpacing = -12f;
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -132f), new Vector2(-92f, 178f), new Vector2(0.5f, 1f));

        CreateOrnamentDivider(chapterPanel.transform, -326f, 430f);

        TMP_Text subtitle = CreateText("Subtitle", chapterPanel.transform, "A KNIGHT'S TRIAL", 23f, FontStyles.Italic, Parchment, TextAlignmentOptions.Center);
        subtitle.characterSpacing = 6f;
        SetRect(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -356f), new Vector2(-120f, 40f), new Vector2(0.5f, 1f));

        TMP_Text description = CreateText("Description", chapterPanel.transform,
            "Beyond the drowned road, an old enemy waits beneath the trees.\nTake up your blade and endure the encounter.",
            20f, FontStyles.Normal, TextSecondary, TextAlignmentOptions.Center);
        description.lineSpacing = 8f;
        SetRect(description.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -414f), new Vector2(-130f, 88f), new Vector2(0.5f, 1f));

        Button startButton = CreateButton("StartButton_Medieval", chapterPanel.transform, "BEGIN JOURNEY", Parchment, Ink);
        SetRect(startButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 214f), new Vector2(430f, 64f), new Vector2(0.5f, 0f));
        UnityEventTools.AddPersistentListener(startButton.onClick, menuController.OnStartGame);

        Button quitButton = CreateButton("QuitButton_Medieval", chapterPanel.transform, "LEAVE THE REALM", PanelSoft, TextPrimary);
        SetRect(quitButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 132f), new Vector2(430f, 54f), new Vector2(0.5f, 0f));
        UnityEventTools.AddPersistentListener(quitButton.onClick, menuController.OnQuitGame);

        TMP_Text controls = CreateText("Controls", chapterPanel.transform, "WASD  MOVE     LMB  ATTACK     MMB  LOCK ON     SHIFT  SPRINT", 14f, FontStyles.Bold, TextSecondary, TextAlignmentOptions.Center);
        controls.characterSpacing = 2f;
        SetRect(controls.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 52f), new Vector2(-90f, 28f), new Vector2(0.5f, 0f));

        TMP_Text footer = CreateText("Footer", menuRoot.transform, "A THIRD-PERSON MEDIEVAL ACTION PROTOTYPE", 13f, FontStyles.Bold, new Color(TextSecondary.r, TextSecondary.g, TextSecondary.b, 0.72f), TextAlignmentOptions.Center);
        footer.characterSpacing = 5f;
        SetRect(footer.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 22f), new Vector2(-80f, 24f), new Vector2(0.5f, 0f));
        return menuRoot;
    }

    private static HudReferences BuildHud(Transform parent)
    {
        GameObject hudRoot = CreateUIObject("HudRoot", parent, typeof(RectTransform));
        Stretch(hudRoot.GetComponent<RectTransform>());

        GameObject topLine = CreatePanel("TopAccentLine", hudRoot.transform, new Color(Accent.r, Accent.g, Accent.b, 0.42f));
        SetRect(topLine.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 3f), new Vector2(0.5f, 1f));

        GameObject objectiveCard = CreateFramedPanel("ObjectiveCard", hudRoot.transform, new Color(Panel.r, Panel.g, Panel.b, 0.84f), new Color(Accent.r, Accent.g, Accent.b, 0.38f));
        SetRect(objectiveCard.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -34f), new Vector2(390f, 88f), new Vector2(1f, 1f));
        TMP_Text objectiveTag = CreateText("ObjectiveTag", objectiveCard.transform, "CURRENT OBJECTIVE", 15f, FontStyles.Bold, Accent, TextAlignmentOptions.Left);
        SetRect(objectiveTag.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -14f), new Vector2(-40f, 24f), new Vector2(0f, 1f));
        TMP_Text objectiveText = CreateText("ObjectiveText", objectiveCard.transform, "SURVIVE THE ENCOUNTER", 20f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.Left);
        SetRect(objectiveText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 16f), new Vector2(-40f, 30f), new Vector2(0f, 0f));

        GameObject controlsCard = CreateFramedPanel("CombatControls", hudRoot.transform, new Color(Panel.r, Panel.g, Panel.b, 0.78f), new Color(Moss.r, Moss.g, Moss.b, 0.62f));
        SetRect(controlsCard.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 34f), new Vector2(420f, 98f), new Vector2(1f, 0f));
        TMP_Text controlsText = CreateText("Controls", controlsCard.transform, "LMB  ATTACK     MMB  LOCK ON     SHIFT  SPRINT", 15f, FontStyles.Bold, TextSecondary, TextAlignmentOptions.Center);
        Stretch(controlsText.rectTransform, 18f, 18f, 18f, 18f);

        GameObject crosshairH = CreatePanel("CrosshairH", hudRoot.transform, new Color(Parchment.r, Parchment.g, Parchment.b, 0.68f));
        SetRect(crosshairH.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 2f), new Vector2(0.5f, 0.5f));
        GameObject crosshairV = CreatePanel("CrosshairV", hudRoot.transform, new Color(Parchment.r, Parchment.g, Parchment.b, 0.68f));
        SetRect(crosshairV.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 18f), new Vector2(0.5f, 0.5f));

        return new HudReferences
        {
            Root = hudRoot
        };
    }

    private static void BindRuntimeController(GameObject root, HudReferences hud, GameObject legacyHudRoot)
    {
        CombatHUDController controller = root.AddComponent<CombatHUDController>();
        SerializedObject serialized = new(controller);
        serialized.FindProperty("m_HudRoot").objectReferenceValue = hud.Root;
        serialized.FindProperty("m_LegacyHudRoot").objectReferenceValue = legacyHudRoot;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ReplaceMainMenuRoot(MainMenuUI menuController, GameObject menuRoot)
    {
        SerializedObject serialized = new(menuController);
        serialized.FindProperty("m_MenuRoot").objectReferenceValue = menuRoot;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(menuController);
    }

    private static void RequireReference(SerializedObject serialized, string propertyName)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == null)
            throw new MissingReferenceException($"{serialized.targetObject.name}.{propertyName} 未绑定。");
    }

    private static void CaptureSingleCanvas(Canvas canvas, string outputPath)
    {
        CaptureCanvases(canvas, null, null, outputPath);
    }

    private static void CaptureHudCanvas(Canvas canvas, Canvas legacyCanvas, GameObject legacyHudRoot, string outputPath)
    {
        CaptureCanvases(canvas, legacyCanvas, legacyHudRoot, outputPath);
    }

    private static void CaptureCanvases(Canvas canvas, Canvas secondaryCanvas, GameObject secondaryRoot, string outputPath)
    {
        RenderMode originalMode = canvas.renderMode;
        Camera originalCamera = canvas.worldCamera;
        RenderMode originalSecondaryMode = secondaryCanvas != null ? secondaryCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
        Camera originalSecondaryCamera = secondaryCanvas != null ? secondaryCanvas.worldCamera : null;
        bool originalSecondaryState = secondaryRoot != null && secondaryRoot.activeSelf;

        GameObject cameraObject = new("UI_Preview_Camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.015f, 0.02f, 0.03f, 1f);
        camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
        camera.orthographic = true;

        RenderTexture renderTexture = new(1920, 1080, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = renderTexture;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        if (secondaryCanvas != null)
        {
            secondaryCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            secondaryCanvas.worldCamera = camera;
            secondaryCanvas.planeDistance = 1f;
        }
        if (secondaryRoot != null)
            secondaryRoot.SetActive(true);

        Canvas.ForceUpdateCanvases();
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D screenshot = new(1920, 1080, TextureFormat.RGBA32, false);
        screenshot.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
        screenshot.Apply();
        File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());

        RenderTexture.active = previous;
        canvas.renderMode = originalMode;
        canvas.worldCamera = originalCamera;
        if (secondaryCanvas != null)
        {
            secondaryCanvas.renderMode = originalSecondaryMode;
            secondaryCanvas.worldCamera = originalSecondaryCamera;
        }
        if (secondaryRoot != null)
            secondaryRoot.SetActive(originalSecondaryState);
        Object.DestroyImmediate(screenshot);
        Object.DestroyImmediate(renderTexture);
        Object.DestroyImmediate(cameraObject);
    }

    private static GameObject CreateUIObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject gameObject = new(name, components);
        gameObject.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = CreateUIObject(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private static GameObject CreateFramedPanel(string name, Transform parent, Color backgroundColor, Color borderColor)
    {
        GameObject panel = CreatePanel(name, parent, backgroundColor);
        CreateBorderLine("BorderTop", panel.transform, borderColor, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f), new Vector2(0f, 2f));
        CreateBorderLine("BorderBottom", panel.transform, borderColor, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, 2f));
        CreateBorderLine("BorderLeft", panel.transform, borderColor, Vector2.zero, new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(2f, 0f));
        CreateBorderLine("BorderRight", panel.transform, borderColor, new Vector2(1f, 0f), Vector2.one, new Vector2(-1f, 0f), new Vector2(2f, 0f));
        return panel;
    }

    private static void CreateBorderLine(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject line = CreatePanel(name, parent, color);
        SetRect(line.GetComponent<RectTransform>(), anchorMin, anchorMax, position, size, new Vector2(0.5f, 0.5f));
    }

    private static void CreateOrnamentDivider(Transform parent, float y, float width)
    {
        GameObject line = CreatePanel("OrnamentLine", parent, new Color(Accent.r, Accent.g, Accent.b, 0.7f));
        SetRect(line.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(width, 2f), new Vector2(0.5f, 0.5f));

        GameObject diamond = CreatePanel("OrnamentDiamond", parent, Parchment);
        RectTransform diamondRect = diamond.GetComponent<RectTransform>();
        SetRect(diamondRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(12f, 12f), new Vector2(0.5f, 0.5f));
        diamondRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color normalColor, Color textColor)
    {
        GameObject buttonObject = CreateUIObject(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        Image image = buttonObject.GetComponent<Image>();
        image.color = normalColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text text = CreateText("Label", buttonObject.transform, label, 20f, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
        text.characterSpacing = 5f;
        Stretch(text.rectTransform);
        return button;
    }

    private static void CreateLabelPair(Transform parent, string label, string key, float x, float y)
    {
        TMP_Text labelText = CreateText(label + "Label", parent, label, 13f, FontStyles.Bold, TextSecondary, TextAlignmentOptions.Left);
        SetRect(labelText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, y), new Vector2(150f, 22f), new Vector2(0f, 1f));
        TMP_Text keyText = CreateText(label + "Key", parent, key, 22f, FontStyles.Bold, TextPrimary, TextAlignmentOptions.Left);
        SetRect(keyText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, y - 32f), new Vector2(150f, 32f), new Vector2(0f, 1f));
    }

    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private sealed class HudReferences
    {
        public GameObject Root;
    }
}
