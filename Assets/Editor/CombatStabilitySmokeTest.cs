using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// P0 战斗稳定性 Play Mode 冒烟测试。
/// 主动运行时验证重力、Root Motion、伤害快照、死亡重载和跨场景单例。
/// </summary>
public static class CombatStabilitySmokeTest
{
    private const string GameScenePath = "Assets/Scenes/1-GameScene.unity";
    private const string RunningKey = "3DActGame.P0Smoke.Running";

    private static int s_Stage;
    private static float s_Deadline;
    private static float s_StartHeight;
    private static Vector3 s_StartPosition;
    private static float s_AttackStartHeight;
    private static int s_SceneLoadCount;
    private static float s_ReloadObservedAt;
    private static InputReader s_OriginalInputReader;
    private static AudioManager s_OriginalAudioManager;

    [InitializeOnLoadMethod]
    private static void ResumeAfterDomainReload()
    {
        if (!SessionState.GetBool(RunningKey, false))
            return;

        EditorApplication.update -= UpdateSmokeTest;
        EditorApplication.update += UpdateSmokeTest;
    }

    [MenuItem("Tools/3DActGame/P0/Run Combat Stability Smoke Test")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new UnityException("请先退出 Play Mode 再运行 P0 冒烟测试。");

        CombatStabilityEditor.Validate();
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        s_Stage = 0;
        s_Deadline = 0f;
        EditorApplication.update -= UpdateSmokeTest;
        EditorApplication.update += UpdateSmokeTest;
        EditorApplication.EnterPlaymode();
    }

    private static void UpdateSmokeTest()
    {
        if (!EditorApplication.isPlaying)
            return;

        try
        {
            float now = Time.realtimeSinceStartup;
            switch (s_Stage)
            {
                case 0:
                    if (s_Deadline <= 0f)
                    {
                        // 等待场景中所有 Start 执行，确保计时器与事件订阅已经建立。
                        s_Deadline = now + 0.5f;
                        return;
                    }
                    if (now < s_Deadline)
                        return;
                    BeginGravityCheck(now);
                    break;
                case 1:
                    if (now < s_Deadline)
                        return;
                    ValidateGravityAndDamageSnapshot(now);
                    break;
                case 2:
                    if (now < s_Deadline)
                        return;
                    ValidateDuplicateManagersAndKillPlayer(now);
                    break;
                case 3:
                    ValidateSceneReload(now);
                    break;
            }
        }
        catch (Exception exception)
        {
            Finish(false, exception.ToString());
        }
    }

    private static void BeginGravityCheck(float now)
    {
        Player player = UnityEngine.Object.FindObjectOfType<Player>();
        CharacterController controller = player != null ? player.GetComponent<CharacterController>() : null;
        if (player == null || controller == null)
            throw new MissingComponentException("Play Mode 中缺少 Player 或 CharacterController。");

        controller.enabled = false;
        player.transform.position += Vector3.up * 3f;
        controller.enabled = true;
        s_StartHeight = player.transform.position.y;
        s_StartPosition = player.transform.position;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        s_OriginalInputReader = InputReader.Instance;
        s_OriginalAudioManager = AudioManager.Instance;
        s_SceneLoadCount = 0;
        s_Deadline = now + 1f;
        s_Stage = 1;
    }

    private static void ValidateGravityAndDamageSnapshot(float now)
    {
        Player player = UnityEngine.Object.FindObjectOfType<Player>();
        if (player == null || player.transform.position.y >= s_StartHeight - 0.05f)
            throw new UnityException("无输入状态下玩家没有下落，垂直移动未持续执行。");

        Vector2 horizontalDrift = new(
            player.transform.position.x - s_StartPosition.x,
            player.transform.position.z - s_StartPosition.z);
        if (horizontalDrift.magnitude > 0.05f)
            throw new UnityException($"无输入时玩家产生了 {horizontalDrift.magnitude:F3} 米水平位移，请检查 Root Motion。");

        System.Reflection.MethodInfo startCombo = typeof(Player).GetMethod(
            "StartCombo",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        startCombo?.Invoke(player, new object[] { 1 });

        WeaponDamage weaponDamage = player.GetComponentInChildren<WeaponDamage>(true);
        System.Reflection.FieldInfo activeDamage = typeof(WeaponDamage).GetField(
            "m_ActiveDamage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        float damage = activeDamage != null ? (float)activeDamage.GetValue(weaponDamage) : -1f;
        if (!Mathf.Approximately(damage, 10f))
            throw new UnityException($"第一段攻击伤害快照应为 10，实际为 {damage}。");

        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position += Vector3.up;
        controller.enabled = true;
        s_AttackStartHeight = player.transform.position.y;

        new GameObject("P0 Duplicate InputReader").AddComponent<InputReader>();
        new GameObject("P0 Duplicate AudioManager").AddComponent<AudioManager>();

        s_Deadline = now + 0.25f;
        s_Stage = 2;
    }

    private static void ValidateDuplicateManagersAndKillPlayer(float now)
    {
        Player player = UnityEngine.Object.FindObjectOfType<Player>();
        if (player == null || player.transform.position.y >= s_AttackStartHeight - 0.01f)
            throw new UnityException("攻击期间玩家没有下落，重力仍被连招状态阻断。");

        if (InputReader.Instance == null || InputReader.Instance != s_OriginalInputReader)
            throw new UnityException("创建重复 InputReader 后持久化主实例发生变化。");
        if (AudioManager.Instance == null || AudioManager.Instance != s_OriginalAudioManager)
            throw new UnityException("创建重复 AudioManager 后持久化主实例发生变化。");

        Health health = player != null ? player.GetComponent<Health>() : null;
        if (health == null)
            throw new MissingComponentException("Play Mode 中 Player 缺少 Health。");

        health.TakeDamage(health.GetCurrentHP());
        s_Deadline = now + 5f;
        s_Stage = 3;
    }

    private static void ValidateSceneReload(float now)
    {
        if (s_SceneLoadCount > 1)
            throw new UnityException($"玩家死亡后场景被重复加载：{s_SceneLoadCount} 次。");

        if (s_SceneLoadCount == 1 && now >= s_ReloadObservedAt + 0.5f)
        {
            if (InputReader.Instance == null || AudioManager.Instance == null)
                throw new UnityException("场景重载后 InputReader 或 AudioManager 主实例为空。");
            if (UnityEngine.Object.FindObjectsOfType<InputReader>(true).Length != 1)
                throw new UnityException("场景重载后存在多个 InputReader 实例。");
            if (UnityEngine.Object.FindObjectsOfType<AudioManager>(true).Length != 1)
                throw new UnityException("场景重载后存在多个 AudioManager 实例。");

            Finish(true, "重力、Root Motion、10 点伤害快照、单次死亡重载和跨场景单例均通过。");
            return;
        }

        if (now >= s_Deadline)
            throw new TimeoutException("等待玩家死亡后的场景重载超时。");
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        s_SceneLoadCount++;
        s_ReloadObservedAt = Time.realtimeSinceStartup;
    }

    private static void Finish(bool succeeded, string message)
    {
        EditorApplication.update -= UpdateSmokeTest;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SessionState.SetBool(RunningKey, false);

        if (succeeded)
            Debug.Log($"[CombatStabilitySmokeTest] PASS：{message}");
        else
            Debug.LogError($"[CombatStabilitySmokeTest] FAIL：{message}");

        // 批处理验证无需恢复编辑器界面，直接退出可避免场景恢复时触发耗时的自动光照烘焙。
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(succeeded ? 0 : 1);
            return;
        }

        EditorApplication.ExitPlaymode();
    }
}
