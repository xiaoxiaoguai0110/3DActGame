using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 一次性把旧的场景单体敌人迁移为 Prefab + EnemySpawner 结构。
/// </summary>
public static class EnemySpawnerEditor
{
    private const string ScenePath = "Assets/Scenes/1-GameScene.unity";
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    private const string BloodPrefabPath = "Assets/Prefabs/BloodEffect1.prefab";

    [InitializeOnLoadMethod]
    private static void ScheduleMigration()
    {
        EditorApplication.delayCall += TryAutoApply;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += TryAutoApply;
    }

    private static void TryAutoApply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || scene.isDirty || FindSceneComponent<EnemySpawner>() != null)
            return;

        Apply();
    }

    [MenuItem("Tools/3DActGame/Setup Random Enemy Spawner")]
    public static void Apply()
    {
        ConfigureEnemyPrefab();

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        RemoveFixedSceneEnemies();
        EnemySpawner spawner = CreateOrReuseSpawner();
        ConfigureEnemyHealthUI();

        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[EnemySpawnerEditor] 随机敌人生成器已配置：初始 3 只、最大存活 5 只、死亡后继续补充。");
    }

    [MenuItem("Tools/3DActGame/Validate Random Enemy Spawner")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (prefab == null || prefab.GetComponent<Enemy>() == null || prefab.GetComponent<Health>() == null)
            throw new MissingComponentException("Enemy.prefab 必须同时包含 Enemy 和 Health。");

        EnemySpawner spawner = FindSceneComponent<EnemySpawner>();
        if (spawner == null)
            throw new MissingComponentException("场景中没有 EnemySpawner。");

        SerializedObject serializedSpawner = new(spawner);
        if (serializedSpawner.FindProperty("m_EnemyPrefab").objectReferenceValue == null)
            throw new MissingReferenceException("EnemySpawner.m_EnemyPrefab 未绑定。");
        if (serializedSpawner.FindProperty("m_MaxAliveEnemies").intValue > 5)
            throw new UnityException("EnemySpawner 最大数量超过五只。");

        if (FindSceneComponents<Enemy>().Count != 0)
            throw new UnityException("场景中仍存在固定敌人，应只由 EnemySpawner 在运行时生成。");

        EnemyHealthUI healthUI = FindSceneComponent<EnemyHealthUI>();
        if (healthUI == null)
            throw new MissingComponentException("原版 HUD 缺少支持多敌人的 EnemyHealthUI。");

        SerializedObject serializedHealthUI = new(healthUI);
        if (serializedHealthUI.FindProperty("m_Slider").objectReferenceValue == null)
            throw new MissingReferenceException("EnemyHealthUI.m_Slider 未绑定。");

        Debug.Log("[EnemySpawnerEditor] 验证通过：Prefab 依赖完整，场景最大敌人数为 5，敌人血条已支持多目标。");
    }

    private static void ConfigureEnemyPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            prefabRoot.tag = "Enemy";

            Health health = prefabRoot.GetComponent<Health>();
            if (health == null)
                health = prefabRoot.AddComponent<Health>();

            SerializedObject serializedHealth = new(health);
            serializedHealth.FindProperty("m_MaxHP").floatValue = 200f;
            serializedHealth.FindProperty("m_CurrentHP").floatValue = 200f;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();

            Enemy enemy = prefabRoot.GetComponent<Enemy>();
            if (enemy == null)
                throw new MissingComponentException("Enemy.prefab 缺少 Enemy 组件。");

            SerializedObject serializedEnemy = new(enemy);
            serializedEnemy.FindProperty("m_CurrentState").enumValueIndex = (int)EnemyState.Idle;
            serializedEnemy.FindProperty("playerObj").objectReferenceValue = null;
            serializedEnemy.FindProperty("bloodPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BloodPrefabPath);
            serializedEnemy.FindProperty("bloodSpawn").objectReferenceValue = FindChild(prefabRoot.transform, "CHIMERA_ Spine");
            serializedEnemy.ApplyModifiedPropertiesWithoutUndo();

            // 血条由场景 HUD 统一选择最近敌人，Prefab 上不应再各自控制同一条 Slider。
            foreach (EnemyHealthUI oldHealthUI in prefabRoot.GetComponents<EnemyHealthUI>())
                Object.DestroyImmediate(oldHealthUI);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, EnemyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void RemoveFixedSceneEnemies()
    {
        foreach (Enemy enemy in FindSceneComponents<Enemy>())
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(enemy.gameObject);
            Object.DestroyImmediate(root != null ? root : enemy.gameObject);
        }
    }

    private static EnemySpawner CreateOrReuseSpawner()
    {
        EnemySpawner spawner = FindSceneComponent<EnemySpawner>();
        if (spawner == null)
            spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        SerializedObject serialized = new(spawner);
        serialized.FindProperty("m_EnemyPrefab").objectReferenceValue = prefab.GetComponent<Enemy>();
        serialized.FindProperty("m_MaxAliveEnemies").intValue = 5;
        serialized.FindProperty("m_InitialEnemyCount").intValue = 3;
        serialized.FindProperty("m_SpawnInterval").floatValue = 6f;
        serialized.FindProperty("m_MinDistanceFromPlayer").floatValue = 15f;
        serialized.FindProperty("m_MinEnemySpacing").floatValue = 6f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return spawner;
    }

    private static void ConfigureEnemyHealthUI()
    {
        Slider enemySlider = null;
        foreach (Slider slider in FindSceneComponents<Slider>())
        {
            if (slider.name == "EnemyHealthSlider")
            {
                enemySlider = slider;
                break;
            }
        }

        if (enemySlider == null)
            throw new MissingReferenceException("没有找到原版 EnemyHealthSlider。");

        Canvas canvas = enemySlider.GetComponentInParent<Canvas>(true);
        if (canvas == null)
            throw new MissingComponentException("EnemyHealthSlider 不在 Canvas 下。");

        EnemyHealthUI healthUI = canvas.GetComponent<EnemyHealthUI>();
        if (healthUI == null)
            healthUI = canvas.gameObject.AddComponent<EnemyHealthUI>();

        SerializedObject serialized = new(healthUI);
        serialized.FindProperty("m_Slider").objectReferenceValue = enemySlider;
        serialized.FindProperty("m_DisplayRange").floatValue = 20f;
        serialized.FindProperty("m_TargetRefreshInterval").floatValue = 0.2f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(healthUI);
    }

    private static Transform FindChild(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        List<T> components = FindSceneComponents<T>();
        return components.Count > 0 ? components[0] : null;
    }

    private static List<T> FindSceneComponents<T>() where T : Component
    {
        List<T> result = new();
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component.gameObject.scene.IsValid() && component.gameObject.scene.path == ScenePath)
                result.Add(component);
        }

        return result;
    }
}
