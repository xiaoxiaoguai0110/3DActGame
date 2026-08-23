using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 作品集 P0 配置工具：固化玩家受击根节点、有限遭遇流程和结算 UI，
/// 同时移除非必要调试脚本；第三方 Prefab 缺失时只报告，不擅自删除场景对象。
/// </summary>
public static class PortfolioP0Editor
{
    private const string GameScenePath = "Assets/Scenes/1-GameScene.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const int DefaultEncounterEnemyCount = 8;

    private static readonly string[] OptionalDependencyGuids =
    {
        "0629b26a98ce01a46b7bf9348c41bb30",
        "04c36f15bbecd854cab50deedb5c594f",
        "02a0e42f36bf76d4c946b93fc70c3cee",
        "00941ab5dd3500748ac6e339132b3596",
        "eadf148176475c9469704f4d4213f736"
    };

    [MenuItem("Tools/3DActGame/Portfolio P0/Apply")]
    public static void Apply()
    {
        ConfigurePlayerPrefab();

        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        RemoveOptionalDebugObject();
        ConfigureGameFlow(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Validate();
        Debug.Log("[PortfolioP0Editor] P0 配置完成：受击硬直、有限遭遇和胜负结算已启用。");
    }

    [MenuItem("Tools/3DActGame/Portfolio P0/Validate")]
    public static void Validate()
    {
        ValidatePlayerPrefab();
        ValidateScene();
        ValidateOptionalDependencies();
        Debug.Log("[PortfolioP0Editor] P0 验证通过：核心流程引用完整。可选第三方资源按依赖策略单独检查。");
    }

    private static void ConfigurePlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Player player = root.GetComponent<Player>();
            if (player == null)
                throw new MissingComponentException("Player.prefab 缺少 Player。");

            Transform visualRoot = FindVisualRoot(root.transform);
            if (visualRoot == null)
                throw new MissingReferenceException("Player.prefab 中找不到可用于受击硬直的模型根节点。");

            SerializedObject serializedPlayer = new(player);
            serializedPlayer.FindProperty("m_HitReactionRoot").objectReferenceValue = visualRoot;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform FindVisualRoot(Transform playerRoot)
    {
        Renderer renderer = playerRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (renderer == null)
            renderer = playerRoot.GetComponentInChildren<MeshRenderer>(true);
        if (renderer == null)
            return null;

        Transform candidate = renderer.transform;
        while (candidate.parent != null && candidate.parent != playerRoot)
            candidate = candidate.parent;

        return candidate.parent == playerRoot ? candidate : null;
    }

    private static void RemoveOptionalDebugObject()
    {
        GameObject fpsDisplay = FindSceneObject("FPSDisplay");
        if (fpsDisplay != null)
            UnityEngine.Object.DestroyImmediate(fpsDisplay);
    }

    private static void ConfigureGameFlow(Scene scene)
    {
        Player player = UnityEngine.Object.FindObjectOfType<Player>();
        EnemySpawner spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>();
        if (player == null || spawner == null)
            throw new MissingComponentException("1-GameScene 必须包含 Player 和 EnemySpawner。");

        SerializedObject serializedSpawner = new(spawner);
        serializedSpawner.FindProperty("m_TotalEnemyCount").intValue = DefaultEncounterEnemyCount;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        GameFlowController flow = UnityEngine.Object.FindObjectOfType<GameFlowController>();
        if (flow == null)
        {
            GameObject flowObject = new("GameFlowController");
            SceneManager.MoveGameObjectToScene(flowObject, scene);
            flow = flowObject.AddComponent<GameFlowController>();
        }

        TMP_Text objectiveText = FindSceneComponent<TMP_Text>("ObjectiveText");
        Canvas canvas = objectiveText != null ? objectiveText.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            canvas = UnityEngine.Object.FindObjectsOfType<Canvas>(true).FirstOrDefault(candidate => candidate.isRootCanvas);
        if (canvas == null)
            throw new MissingComponentException("战斗场景缺少可承载结算界面的 Canvas。");

        ResultUi resultUi = FindOrCreateResultUi(canvas.transform, objectiveText?.font);
        SerializedObject serializedFlow = new(flow);
        serializedFlow.FindProperty("m_Player").objectReferenceValue = player;
        serializedFlow.FindProperty("m_EnemySpawner").objectReferenceValue = spawner;
        serializedFlow.FindProperty("m_ObjectiveText").objectReferenceValue = objectiveText;
        serializedFlow.FindProperty("m_ResultPanel").objectReferenceValue = resultUi.Panel;
        serializedFlow.FindProperty("m_ResultTitle").objectReferenceValue = resultUi.Title;
        serializedFlow.FindProperty("m_ResultDescription").objectReferenceValue = resultUi.Description;
        serializedFlow.ApplyModifiedPropertiesWithoutUndo();

        ConfigureButton(resultUi.RetryButton, flow.OnRetry);
        ConfigureButton(resultUi.MenuButton, flow.OnReturnToMenu);
        resultUi.Panel.SetActive(false);
    }

    private static ResultUi FindOrCreateResultUi(Transform canvas, TMP_FontAsset fallbackFont)
    {
        Transform existing = canvas.Find("P0_ResultPanel");
        if (existing != null)
        {
            return new ResultUi(
                existing.gameObject,
                existing.Find("Frame/Title")?.GetComponent<TMP_Text>(),
                existing.Find("Frame/Description")?.GetComponent<TMP_Text>(),
                existing.Find("Frame/RetryButton")?.GetComponent<Button>(),
                existing.Find("Frame/MenuButton")?.GetComponent<Button>());
        }

        TMP_FontAsset font = fallbackFont != null ? fallbackFont : TMP_Settings.defaultFontAsset;
        GameObject panel = CreateUiObject("P0_ResultPanel", canvas, typeof(Image));
        Stretch(panel.GetComponent<RectTransform>());
        panel.GetComponent<Image>().color = new Color(0.015f, 0.018f, 0.016f, 0.88f);

        GameObject frame = CreateUiObject("Frame", panel.transform, typeof(Image));
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(620f, 360f);
        frame.GetComponent<Image>().color = new Color(0.08f, 0.075f, 0.06f, 0.98f);

        TMP_Text title = CreateText("Title", frame.transform, font, 42f, FontStyles.Bold);
        SetRect(title.rectTransform, new Vector2(0f, 95f), new Vector2(540f, 70f));
        TMP_Text description = CreateText("Description", frame.transform, font, 21f, FontStyles.Normal);
        SetRect(description.rectTransform, new Vector2(0f, 25f), new Vector2(520f, 70f));

        Button retry = CreateButton("RetryButton", frame.transform, "TRY AGAIN", font, new Vector2(-145f, -105f));
        Button menu = CreateButton("MenuButton", frame.transform, "RETURN TO MENU", font, new Vector2(145f, -105f));
        return new ResultUi(panel, title, description, retry, menu);
    }

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] extraComponents)
    {
        Type[] components = new[] { typeof(RectTransform), typeof(CanvasRenderer) }
            .Concat(extraComponents).Distinct().ToArray();
        GameObject gameObject = new(name, components);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style)
    {
        GameObject gameObject = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.92f, 0.86f, 0.68f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, TMP_FontAsset font, Vector2 position)
    {
        GameObject gameObject = CreateUiObject(name, parent, typeof(Image), typeof(Button));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(250f, 58f);
        gameObject.GetComponent<Image>().color = new Color(0.24f, 0.19f, 0.11f, 1f);

        TMP_Text text = CreateText("Label", gameObject.transform, font, 19f, FontStyles.Bold);
        text.text = label;
        Stretch(text.rectTransform);
        return gameObject.GetComponent<Button>();
    }

    private static void ConfigureButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null)
            throw new MissingComponentException("结算界面按钮缺失。");

        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, callback);
        EditorUtility.SetDirty(button);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void ValidatePlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Player player = root.GetComponent<Player>();
            SerializedObject serializedPlayer = new(player);
            if (serializedPlayer.FindProperty("m_HitReactionRoot").objectReferenceValue == null)
                throw new MissingReferenceException("Player.prefab 未绑定受击硬直模型根节点。");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateScene()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        GameFlowController flow = UnityEngine.Object.FindObjectOfType<GameFlowController>();
        EnemySpawner spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>();
        if (flow == null || spawner == null)
            throw new MissingComponentException("战斗场景缺少 GameFlowController 或 EnemySpawner。");

        SerializedObject serializedFlow = new(flow);
        string[] requiredReferences =
        {
            "m_Player", "m_EnemySpawner", "m_ObjectiveText",
            "m_ResultPanel", "m_ResultTitle", "m_ResultDescription"
        };
        foreach (string propertyName in requiredReferences)
        {
            if (serializedFlow.FindProperty(propertyName).objectReferenceValue == null)
                throw new MissingReferenceException($"GameFlowController.{propertyName} 未绑定。");
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    throw new MissingReferenceException($"场景对象 {transform.name} 存在 Missing Script。");
            }
        }

        if (FindSceneObject("FPSDisplay") != null)
            throw new UnityException("战斗场景不应保留依赖第三方脚本的 FPSDisplay。");
    }

    private static void ValidateOptionalDependencies()
    {
        int missingCount = OptionalDependencyGuids.Count(guid => string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)));
        if (missingCount > 0)
        {
            Debug.LogWarning($"[PortfolioP0Editor] 本机缺少 {missingCount} 个可选第三方资源依赖；" +
                "公开仓库不会上传这些资源，请参考 THIRD_PARTY_ASSETS.md 后合法导入。");
        }
    }

    private static GameObject FindSceneObject(string name)
    {
        return UnityEngine.Object.FindObjectsOfType<GameObject>(true)
            .FirstOrDefault(candidate => candidate.scene.IsValid() && candidate.name == name);
    }

    private static T FindSceneComponent<T>(string name) where T : Component
    {
        GameObject gameObject = FindSceneObject(name);
        return gameObject != null ? gameObject.GetComponent<T>() : null;
    }

    private sealed class ResultUi
    {
        public GameObject Panel { get; }
        public TMP_Text Title { get; }
        public TMP_Text Description { get; }
        public Button RetryButton { get; }
        public Button MenuButton { get; }

        public ResultUi(GameObject panel, TMP_Text title, TMP_Text description, Button retryButton, Button menuButton)
        {
            Panel = panel;
            Title = title;
            Description = description;
            RetryButton = retryButton;
            MenuButton = menuButton;
        }
    }
}
