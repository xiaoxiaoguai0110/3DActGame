using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// P1 Play Mode 冒烟测试：验证动画事件真正开启/关闭判定、输入窗口推进第二段，
/// 并验证敌人的过期 Animation Event 不会越权修改状态或造成伤害。
/// </summary>
public static class AnimationDrivenCombatSmokeTest
{
    private const string GameScenePath = "Assets/Scenes/1-GameScene.unity";
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    private const string RunningKey = "3DActGame.P1Smoke.Running";

    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int s_Stage;
    private static float s_Deadline;
    private static Player s_Player;
    private static WeaponDamage s_WeaponDamage;
    private static readonly float[] ExpectedDamages = { 10f, 15f, 20f, 25f, 30f };
    private static bool[] s_SawDamageWindows = new bool[ExpectedDamages.Length];
    private static int s_LastQueuedStage;

    [InitializeOnLoadMethod]
    private static void ResumeAfterDomainReload()
    {
        if (!SessionState.GetBool(RunningKey, false))
            return;

        EditorApplication.update -= UpdateSmokeTest;
        EditorApplication.update += UpdateSmokeTest;
    }

    [MenuItem("Tools/3DActGame/P1/Run Animation Driven Combat Smoke Test")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new UnityException("请先退出 Play Mode 再运行 P1 冒烟测试。");

        AnimationDrivenCombatEditor.Validate();
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        s_Stage = 0;
        s_Deadline = 0f;
        s_SawDamageWindows = new bool[ExpectedDamages.Length];
        s_LastQueuedStage = 0;
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
                        s_Deadline = now + 0.75f;
                        return;
                    }
                    if (now >= s_Deadline)
                        StartComboCheck(now);
                    break;
                case 1:
                    MonitorComboEvents(now);
                    break;
                case 2:
                    ValidateEnemyEventGuards();
                    Finish(true, "五段伤害窗口均由动画事件驱动，输入窗口完整推进连招，敌人旧事件状态门禁生效。");
                    break;
            }
        }
        catch (Exception exception)
        {
            Finish(false, exception.ToString());
        }
    }

    private static void StartComboCheck(float now)
    {
        s_Player = UnityEngine.Object.FindObjectOfType<Player>();
        s_WeaponDamage = s_Player != null ? s_Player.GetComponentInChildren<WeaponDamage>(true) : null;
        if (s_Player == null || s_WeaponDamage == null)
            throw new MissingComponentException("Play Mode 中缺少 Player 或 WeaponDamage。");

        foreach (Enemy enemy in UnityEngine.Object.FindObjectsOfType<Enemy>(true))
            enemy.enabled = false;

        // Editor 测试程序集不能直接访问 internal setter，通过反射只在测试中跳过开场等待。
        typeof(MainMenuUI).GetProperty("IsInputEnabled", BindingFlags.Public | BindingFlags.Static)
            ?.SetValue(null, true);
        InvokePrivate(s_Player, "HandleAttack");
        s_Deadline = now + 10f;
        s_Stage = 1;
    }

    private static void MonitorComboEvents(float now)
    {
        if (now >= s_Deadline)
            throw new TimeoutException("等待五段攻击动画事件超时。");
        if (s_Player.IsInvoking("DisableDamage"))
            throw new UnityException("仍存在固定 Invoke 伤害窗口。");

        bool damageActive = GetPrivateField<bool>(s_WeaponDamage, "m_IsDamageActive");
        float activeDamage = GetPrivateField<float>(s_WeaponDamage, "m_ActiveDamage");
        int comboStage = GetPrivateField<int>(s_Player, "m_ComboStage");
        bool inputWindowOpen = GetPrivateField<bool>(s_Player, "m_IsComboInputWindowOpen");

        if (comboStage >= 1 && comboStage <= ExpectedDamages.Length && damageActive)
        {
            float expectedDamage = ExpectedDamages[comboStage - 1];
            if (!Mathf.Approximately(activeDamage, expectedDamage))
                throw new UnityException($"第 {comboStage} 段有效帧伤害应为 {expectedDamage}，实际为 {activeDamage}。");
            s_SawDamageWindows[comboStage - 1] = true;
        }

        if (inputWindowOpen && comboStage < ExpectedDamages.Length && s_LastQueuedStage < comboStage)
        {
            InvokePrivate(s_Player, "HandleAttack");
            int preparedStage = GetPrivateField<int>(s_Player, "m_PreparedComboStage");
            if (preparedStage != comboStage + 1)
                throw new UnityException($"第 {comboStage} 段输入窗口内应缓存第 {comboStage + 1} 段，实际缓存 {preparedStage}。");
            s_LastQueuedStage = comboStage;
        }

        if (comboStage == ExpectedDamages.Length
            && s_SawDamageWindows[ExpectedDamages.Length - 1]
            && !damageActive)
        {
            for (int index = 0; index < s_SawDamageWindows.Length; index++)
            {
                if (!s_SawDamageWindows[index])
                    throw new UnityException($"未观察到第 {index + 1} 段伤害有效帧。");
            }

            s_Stage = 2;
        }
    }

    private static void ValidateEnemyEventGuards()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        Enemy enemy = UnityEngine.Object.Instantiate(prefab).GetComponent<Enemy>();
        SetPrivateField(enemy, "m_Player", s_Player.transform);
        enemy.transform.position = s_Player.transform.position + s_Player.transform.forward;
        enemy.transform.forward = -s_Player.transform.forward;

        Health playerHealth = s_Player.GetComponent<Health>();
        float hpBefore = playerHealth.GetCurrentHP();
        enemy.m_CurrentState = EnemyState.Idle;
        InvokePrivate(enemy, "OnAttackHit");
        if (!Mathf.Approximately(playerHealth.GetCurrentHP(), hpBefore))
            throw new UnityException("非 Attack 状态的旧 OnAttackHit 仍造成了伤害。");

        enemy.m_CurrentState = EnemyState.GetHit;
        InvokePrivate(enemy, "OnAttackEnd");
        if (enemy.m_CurrentState != EnemyState.GetHit)
            throw new UnityException("旧 OnAttackEnd 覆盖了 GetHit 状态。");

        enemy.m_CurrentState = EnemyState.Dead;
        InvokePrivate(enemy, "OnAttackEnd");
        InvokePrivate(enemy, "OnAttackHit");
        if (enemy.m_CurrentState != EnemyState.Dead || !Mathf.Approximately(playerHealth.GetCurrentHP(), hpBefore))
            throw new UnityException("Dead 状态未忽略旧攻击动画事件。");

        UnityEngine.Object.Destroy(enemy.gameObject);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, PrivateInstance);
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        method.Invoke(target, null);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void Finish(bool succeeded, string message)
    {
        EditorApplication.update -= UpdateSmokeTest;
        SessionState.SetBool(RunningKey, false);

        if (succeeded)
            Debug.Log($"[AnimationDrivenCombatSmokeTest] PASS：{message}");
        else
            Debug.LogError($"[AnimationDrivenCombatSmokeTest] FAIL：{message}");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(succeeded ? 0 : 1);
            return;
        }

        EditorApplication.ExitPlaymode();
    }
}
