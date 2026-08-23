using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 作品集 P0 Play Mode 测试：验证玩家受击硬直、动态目标和胜利结算闭环。
/// </summary>
public static class PortfolioP0SmokeTest
{
    private const string GameScenePath = "Assets/Scenes/1-GameScene.unity";
    private const string RunningKey = "3DActGame.PortfolioP0Smoke.Running";
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static int s_Stage;
    private static float s_Deadline;
    private static Player s_Player;
    private static EnemySpawner s_Spawner;
    private static GameFlowController s_Flow;
    private static Transform s_HitRoot;
    private static Vector3 s_BasePosition;
    private static Quaternion s_BaseRotation;
    private static string s_UnexpectedAnimatorError;

    [InitializeOnLoadMethod]
    private static void ResumeAfterDomainReload()
    {
        if (!SessionState.GetBool(RunningKey, false))
            return;

        EditorApplication.update -= UpdateSmokeTest;
        EditorApplication.update += UpdateSmokeTest;
    }

    [MenuItem("Tools/3DActGame/Portfolio P0/Run Smoke Test")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new UnityException("请先退出 Play Mode 再运行作品集 P0 冒烟测试。");

        PortfolioP0Editor.Validate();
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        s_Stage = 0;
        s_Deadline = 0f;
        s_UnexpectedAnimatorError = null;
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
                        BeginHitReactionCheck(now);
                    break;
                case 1:
                    ValidateHitReactionStarted(now);
                    break;
                case 2:
                    ValidateHitReactionFinishedAndTriggerVictory(now);
                    break;
                case 3:
                    DriveFiniteEncounter(now);
                    break;
                case 4:
                    ValidateVictoryResult(now);
                    break;
            }
        }
        catch (Exception exception)
        {
            Finish(false, exception.ToString());
        }
    }

    private static void BeginHitReactionCheck(float now)
    {
        s_Player = UnityEngine.Object.FindObjectOfType<Player>();
        s_Spawner = UnityEngine.Object.FindObjectOfType<EnemySpawner>();
        s_Flow = UnityEngine.Object.FindObjectOfType<GameFlowController>();
        if (s_Player == null || s_Spawner == null || s_Flow == null)
            throw new MissingComponentException("Play Mode 缺少 Player、EnemySpawner 或 GameFlowController。");

        foreach (Enemy enemy in UnityEngine.Object.FindObjectsOfType<Enemy>(true))
            enemy.enabled = false;
        s_Spawner.enabled = false;

        TMP_Text objective = GetPrivateField<TMP_Text>(s_Flow, "m_ObjectiveText");
        if (objective == null || !objective.text.Contains($"0 / {s_Spawner.TotalEnemyCount}"))
            throw new UnityException("动态目标没有显示初始击败进度。");

        s_HitRoot = GetPrivateField<Transform>(s_Player, "m_HitReactionRoot");
        if (s_HitRoot == null)
            throw new MissingReferenceException("Player 没有受击硬直模型根节点。");
        s_BasePosition = s_HitRoot.localPosition;
        s_BaseRotation = s_HitRoot.localRotation;

        Application.logMessageReceived += HandleLog;
        Health health = s_Player.GetComponent<Health>();
        health.TakeDamage(1f);
        s_Deadline = now + 0.1f;
        s_Stage = 1;
    }

    private static void ValidateHitReactionStarted(float now)
    {
        if (now < s_Deadline)
            return;

        if (!GetPrivateField<bool>(s_Player, "m_IsHitReacting"))
            throw new UnityException("玩家受伤后没有进入受击硬直。");
        if (Vector3.Distance(s_HitRoot.localPosition, s_BasePosition) <= 0.001f
            && Quaternion.Angle(s_HitRoot.localRotation, s_BaseRotation) <= 0.1f)
        {
            throw new UnityException("受击硬直状态已进入，但模型没有产生可见位移或倾斜。");
        }
        if (!string.IsNullOrEmpty(s_UnexpectedAnimatorError))
            throw new UnityException(s_UnexpectedAnimatorError);

        s_Deadline = now + 0.4f;
        s_Stage = 2;
    }

    private static void ValidateHitReactionFinishedAndTriggerVictory(float now)
    {
        if (now < s_Deadline)
            return;

        if (GetPrivateField<bool>(s_Player, "m_IsHitReacting"))
            throw new UnityException("玩家受击硬直没有按时结束。");
        if (Vector3.Distance(s_HitRoot.localPosition, s_BasePosition) > 0.001f
            || Quaternion.Angle(s_HitRoot.localRotation, s_BaseRotation) > 0.1f)
        {
            throw new UnityException("受击结束后模型根节点没有恢复原姿态。");
        }

        s_Deadline = now + 10f;
        s_Stage = 3;
    }

    private static void DriveFiniteEncounter(float now)
    {
        if (s_Flow.CurrentState == GameFlowController.FlowState.Victory)
        {
            TMP_Text objective = GetPrivateField<TMP_Text>(s_Flow, "m_ObjectiveText");
            if (objective == null || !objective.text.Contains($"{s_Spawner.TotalEnemyCount} / {s_Spawner.TotalEnemyCount}"))
                throw new UnityException("最后一只敌人死亡后目标进度没有更新为完成。");

            s_Deadline = now + 2f;
            s_Stage = 4;
            return;
        }

        if (now >= s_Deadline)
            throw new TimeoutException($"有限遭遇超时：已击败 {s_Spawner.TotalDefeated} / {s_Spawner.TotalEnemyCount}。");

        Enemy aliveEnemy = null;
        foreach (Enemy enemy in UnityEngine.Object.FindObjectsOfType<Enemy>(true))
        {
            Health health = enemy.GetComponent<Health>();
            Health initializedHealth = GetPrivateField<Health>(enemy, "m_Health");
            if (health != null && initializedHealth != null && health.GetCurrentHP() > 0f)
            {
                aliveEnemy = enemy;
                break;
            }
        }

        if (aliveEnemy != null)
        {
            aliveEnemy.enabled = false;
            Health health = aliveEnemy.GetComponent<Health>();
            health.TakeDamage(health.GetCurrentHP());
            return;
        }

        InvokePrivate(s_Spawner, "TrySpawnEnemy");
    }

    private static void ValidateVictoryResult(float now)
    {
        GameObject resultPanel = GetPrivateField<GameObject>(s_Flow, "m_ResultPanel");
        if (resultPanel != null && resultPanel.activeSelf && Mathf.Approximately(Time.timeScale, 0f))
        {
            Finish(true, "玩家受击硬直、8 只有限遭遇、动态目标、胜利事件和结算暂停均通过。");
            return;
        }

        if (now >= s_Deadline)
            throw new TimeoutException("等待胜利结算界面超时。");
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Warning)
            && condition.Contains("OnGetHit", StringComparison.OrdinalIgnoreCase)
            && condition.Contains("parameter", StringComparison.OrdinalIgnoreCase))
        {
            s_UnexpectedAnimatorError = "玩家受击仍访问不存在的 Animator OnGetHit 参数。";
        }
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

    private static void Finish(bool succeeded, string message)
    {
        Application.logMessageReceived -= HandleLog;
        EditorApplication.update -= UpdateSmokeTest;
        SessionState.SetBool(RunningKey, false);
        Time.timeScale = 1f;

        if (succeeded)
            Debug.Log($"[PortfolioP0SmokeTest] PASS：{message}");
        else
            Debug.LogError($"[PortfolioP0SmokeTest] FAIL：{message}");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(succeeded ? 0 : 1);
            return;
        }

        EditorApplication.ExitPlaymode();
    }
}
