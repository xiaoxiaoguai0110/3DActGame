using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// P0 战斗稳定性资源迁移工具：把场景实例上的关键战斗组件固化到 Prefab，
/// 并校验 CharacterController、伤害判定和 Root Motion 的唯一所有权。
/// </summary>
public static class CombatStabilityEditor
{
    private const string GameScenePath = "Assets/Scenes/1-GameScene.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";

    [MenuItem("Tools/3DActGame/P0/Apply Combat Stability Prefabs")]
    public static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        Player scenePlayer = UnityEngine.Object.FindObjectOfType<Player>();
        if (scenePlayer == null)
            throw new MissingComponentException("1-GameScene 中没有 Player，无法迁移 Prefab 配置。");

        ControllerSnapshot controller = ControllerSnapshot.Capture(scenePlayer.GetComponent<CharacterController>());
        WeaponSnapshot weapon = WeaponSnapshot.Capture(scenePlayer);

        ConfigurePlayerPrefab(controller, weapon);
        ConfigureEnemyPrefab();
        AssetDatabase.SaveAssets();

        // 重新打开场景，让新增的 Prefab 组件成为实例的来源组件，再清理旧的 Added Override。
        scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        CleanupPlayerSceneOverrides(scene);
        AssetDatabase.SaveAssets();

        Validate();
        Debug.Log("[CombatStabilityEditor] P0 Prefab 迁移完成：Player 可直接实例化战斗，Player/Enemy Root Motion 已关闭。");
    }

    [MenuItem("Tools/3DActGame/P0/Validate Combat Stability")]
    public static void Validate()
    {
        ValidateCountdownTimerIsOneShot();
        ValidatePlayerPrefab();
        ValidateEnemyPrefab();
        ValidateGameScene();
        Debug.Log("[CombatStabilityEditor] P0 验证通过：Prefab 依赖、场景实例和位移所有权配置正确。");
    }

    private static void ConfigurePlayerPrefab(ControllerSnapshot controller, WeaponSnapshot weaponSnapshot)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Player player = root.GetComponent<Player>();
            if (player == null)
                throw new MissingComponentException("Player.prefab 根节点缺少 Player 组件。");

            root.tag = "Player";

            // CharacterController 是玩家唯一的位移/碰撞所有者，移除旧 Rigidbody 与 CapsuleCollider。
            foreach (Rigidbody rigidbody in root.GetComponentsInChildren<Rigidbody>(true))
                UnityEngine.Object.DestroyImmediate(rigidbody);

            CapsuleCollider oldCapsule = root.GetComponent<CapsuleCollider>();
            if (oldCapsule != null)
                UnityEngine.Object.DestroyImmediate(oldCapsule);

            CharacterController characterController = root.GetComponent<CharacterController>();
            if (characterController == null)
                characterController = root.AddComponent<CharacterController>();
            controller.ApplyTo(characterController);

            if (root.GetComponent<Health>() == null)
                root.AddComponent<Health>();

            Animator animator = root.GetComponent<Animator>();
            if (animator == null)
                throw new MissingComponentException("Player.prefab 根节点缺少 Animator。");
            animator.applyRootMotion = false;

            WeaponDamage weaponDamage = FindOrCreateWeaponDamage(root, weaponSnapshot);
            SerializedObject serializedPlayer = new(player);
            serializedPlayer.FindProperty("m_WeaponDamage").objectReferenceValue = weaponDamage;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static WeaponDamage FindOrCreateWeaponDamage(GameObject root, WeaponSnapshot snapshot)
    {
        WeaponDamage[] existingDamages = root.GetComponentsInChildren<WeaponDamage>(true);
        WeaponDamage weaponDamage = existingDamages.FirstOrDefault();
        GameObject weaponObject = weaponDamage != null ? weaponDamage.gameObject : null;

        if (weaponObject == null && snapshot != null && !string.IsNullOrEmpty(snapshot.Path))
            weaponObject = root.transform.Find(snapshot.Path)?.gameObject;

        if (weaponObject == null)
        {
            Transform parent = FindWeaponParent(root.transform, snapshot);
            weaponObject = new GameObject(snapshot?.Name ?? "WeaponHitbox");
            weaponObject.transform.SetParent(parent, false);

            if (snapshot != null)
            {
                weaponObject.transform.localPosition = snapshot.LocalPosition;
                weaponObject.transform.localRotation = snapshot.LocalRotation;
                weaponObject.transform.localScale = snapshot.LocalScale;
            }
        }

        Collider collider = EnsureWeaponCollider(weaponObject, snapshot);
        collider.isTrigger = true;
        collider.enabled = true;

        if (weaponDamage == null || weaponDamage.gameObject != weaponObject)
            weaponDamage = weaponObject.GetComponent<WeaponDamage>() ?? weaponObject.AddComponent<WeaponDamage>();

        foreach (WeaponDamage extra in root.GetComponentsInChildren<WeaponDamage>(true))
        {
            if (extra != weaponDamage)
                UnityEngine.Object.DestroyImmediate(extra);
        }

        return weaponDamage;
    }

    private static Transform FindWeaponParent(Transform root, WeaponSnapshot snapshot)
    {
        if (snapshot != null && !string.IsNullOrEmpty(snapshot.Path))
        {
            int separator = snapshot.Path.LastIndexOf('/');
            if (separator > 0)
            {
                Transform snapshotParent = root.Find(snapshot.Path.Substring(0, separator));
                if (snapshotParent != null)
                    return snapshotParent;
            }
        }

        Transform rightHand = root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(child => child.name.Equals("hand_r", StringComparison.OrdinalIgnoreCase));
        return rightHand != null ? rightHand : root;
    }

    private static Collider EnsureWeaponCollider(GameObject weaponObject, WeaponSnapshot snapshot)
    {
        Collider existing = weaponObject.GetComponent<Collider>();
        if (existing != null && !(existing is CharacterController))
            return existing;

        BoxCollider box = weaponObject.AddComponent<BoxCollider>();
        if (snapshot?.Collider is BoxCollider sourceBox)
        {
            box.center = sourceBox.center;
            box.size = sourceBox.size;
        }
        else
        {
            box.center = new Vector3(0f, 0.55f, 0f);
            box.size = new Vector3(0.15f, 1.1f, 0.15f);
        }

        return box;
    }

    private static void ConfigureEnemyPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            Animator animator = root.GetComponent<Animator>();
            if (animator == null)
                throw new MissingComponentException("Enemy.prefab 根节点缺少 Animator。");

            // NavMeshAgent 负责敌人位移，Animator 只输出姿态，避免两个系统同时修改 Transform。
            animator.applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CleanupPlayerSceneOverrides(Scene scene)
    {
        Player player = UnityEngine.Object.FindObjectOfType<Player>();
        if (player == null)
            throw new MissingComponentException("Prefab 更新后场景中的 Player 丢失。");

        Component[] components = player.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null || !PrefabUtility.IsAddedComponentOverride(component))
                continue;

            if (component is CharacterController || component is Health || component is WeaponDamage || component is Rigidbody)
                PrefabUtility.RevertAddedComponent(component, InteractionMode.AutomatedAction);
        }

        Player refreshedPlayer = UnityEngine.Object.FindObjectOfType<Player>();
        SerializedObject serializedPlayer = new(refreshedPlayer);
        SerializedProperty weaponProperty = serializedPlayer.FindProperty("m_WeaponDamage");
        if (weaponProperty.prefabOverride)
            PrefabUtility.RevertPropertyOverride(weaponProperty, InteractionMode.AutomatedAction);

        serializedPlayer.Update();
        if (weaponProperty.objectReferenceValue == null)
        {
            WeaponDamage weaponDamage = refreshedPlayer.GetComponentInChildren<WeaponDamage>(true);
            weaponProperty.objectReferenceValue = weaponDamage;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(refreshedPlayer);
        }

        Animator animator = refreshedPlayer.GetComponent<Animator>();
        if (animator != null && animator.applyRootMotion)
        {
            animator.applyRootMotion = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ValidatePlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Player player = root.GetComponent<Player>();
            if (player == null || root.GetComponent<CharacterController>() == null || root.GetComponent<Health>() == null)
                throw new MissingComponentException("Player.prefab 必须包含 Player、CharacterController 和 Health。");

            if (root.GetComponents<CharacterController>().Length != 1)
                throw new UnityException("Player.prefab 的 CharacterController 数量必须为 1。");
            if (root.GetComponent<CapsuleCollider>() != null || root.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new UnityException("Player.prefab 仍包含旧 Rigidbody 或 CapsuleCollider。");

            Animator animator = root.GetComponent<Animator>();
            if (animator == null || animator.applyRootMotion)
                throw new UnityException("Player.prefab 必须关闭 Apply Root Motion。");

            SerializedObject serializedPlayer = new(player);
            SerializedProperty comboDamages = serializedPlayer.FindProperty("m_ComboDamages");
            if (comboDamages.arraySize == 0 || !Mathf.Approximately(comboDamages.GetArrayElementAtIndex(0).floatValue, 10f))
                throw new UnityException("Player.prefab 第一段连招伤害必须为 10。");

            WeaponDamage weaponDamage = serializedPlayer.FindProperty("m_WeaponDamage").objectReferenceValue as WeaponDamage;
            if (weaponDamage == null || weaponDamage.GetComponent<Collider>() == null)
                throw new MissingReferenceException("Player.prefab 的 m_WeaponDamage 必须绑定到带 Collider 的 WeaponDamage。");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateEnemyPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            Animator animator = root.GetComponent<Animator>();
            if (animator == null || animator.applyRootMotion)
                throw new UnityException("Enemy.prefab 必须关闭 Apply Root Motion，由 NavMeshAgent 负责位移。");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateGameScene()
    {
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        Player player = UnityEngine.Object.FindObjectOfType<Player>();
        if (player == null)
            throw new MissingComponentException("1-GameScene 缺少 Player。");

        if (player.GetComponents<CharacterController>().Length != 1 || player.GetComponents<Health>().Length != 1)
            throw new UnityException("场景 Player 的 CharacterController 或 Health 存在重复实例。");
        if (player.GetComponentsInChildren<WeaponDamage>(true).Length != 1)
            throw new UnityException("场景 Player 的 WeaponDamage 数量必须为 1。");
        if (player.GetComponent<CapsuleCollider>() != null || player.GetComponentsInChildren<Rigidbody>(true).Length != 0)
            throw new UnityException("场景 Player 仍包含旧 Rigidbody 或 CapsuleCollider。");
        if (player.GetComponent<Animator>().applyRootMotion)
            throw new UnityException("场景 Player 仍启用了 Apply Root Motion。");

        SerializedObject serializedPlayer = new(player);
        if (serializedPlayer.FindProperty("m_WeaponDamage").objectReferenceValue == null)
            throw new MissingReferenceException("场景 Player 的 m_WeaponDamage 未绑定。");

        if (UnityEngine.Object.FindObjectsOfType<InputReader>(true).Length != 1)
            throw new UnityException("1-GameScene 中 InputReader 数量必须为 1；跨场景重复由运行时单例清理。");
        if (UnityEngine.Object.FindObjectsOfType<AudioManager>(true).Length != 1)
            throw new UnityException("1-GameScene 中 AudioManager 数量必须为 1；跨场景重复由运行时单例清理。");
    }

    private static void ValidateCountdownTimerIsOneShot()
    {
        CountdownTimer timer = new();
        int callbackCount = 0;
        timer.OnTimerEnd += () => callbackCount++;
        timer.Start(0.1f);
        timer.Tick(0.2f);
        timer.Tick(0.2f);

        if (callbackCount != 1 || timer.IsRunning)
            throw new UnityException("CountdownTimer 必须只触发一次结束回调，避免重复加载场景。");
    }

    private sealed class ControllerSnapshot
    {
        private float Height { get; set; } = 2f;
        private float Radius { get; set; } = 0.26f;
        private float SlopeLimit { get; set; } = 51.93f;
        private float StepOffset { get; set; } = 0.3f;
        private float SkinWidth { get; set; } = 0.08f;
        private float MinMoveDistance { get; set; } = 0.001f;
        private Vector3 Center { get; set; } = new(0f, 1f, 0f);

        public static ControllerSnapshot Capture(CharacterController source)
        {
            ControllerSnapshot snapshot = new();
            if (source == null)
                return snapshot;

            snapshot.Height = source.height;
            snapshot.Radius = source.radius;
            snapshot.SlopeLimit = source.slopeLimit;
            snapshot.StepOffset = source.stepOffset;
            snapshot.SkinWidth = source.skinWidth;
            snapshot.MinMoveDistance = source.minMoveDistance;
            snapshot.Center = source.center;
            return snapshot;
        }

        public void ApplyTo(CharacterController target)
        {
            target.height = Height;
            target.radius = Radius;
            target.slopeLimit = SlopeLimit;
            target.stepOffset = StepOffset;
            target.skinWidth = SkinWidth;
            target.minMoveDistance = MinMoveDistance;
            target.center = Center;
        }
    }

    private sealed class WeaponSnapshot
    {
        public string Path { get; private set; }
        public string Name { get; private set; }
        public Vector3 LocalPosition { get; private set; }
        public Quaternion LocalRotation { get; private set; }
        public Vector3 LocalScale { get; private set; }
        public Collider Collider { get; private set; }

        public static WeaponSnapshot Capture(Player player)
        {
            WeaponDamage weaponDamage = player.GetComponentInChildren<WeaponDamage>(true);
            if (weaponDamage == null)
                return null;

            Transform weapon = weaponDamage.transform;
            return new WeaponSnapshot
            {
                Path = AnimationUtility.CalculateTransformPath(weapon, player.transform),
                Name = weapon.name,
                LocalPosition = weapon.localPosition,
                LocalRotation = weapon.localRotation,
                LocalScale = weapon.localScale,
                Collider = weapon.GetComponent<Collider>()
            };
        }
    }
}
