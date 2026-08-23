using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// P1 资源迁移工具：用攻击配置生成五段动画事件，并统一 Animator Attack Tag。
/// 设计师后续只需修改 PlayerAttackConfig，再执行 Apply 即可同步时间轴。
/// </summary>
public static class AnimationDrivenCombatEditor
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string PlayerControllerPath = "Assets/Anim/PlayerAnim/Player_Animator_Controller.controller";
    private const string ConfigFolderPath = "Assets/ScriptableObjects/Combat";
    private const string ConfigAssetPath = ConfigFolderPath + "/PlayerAttackConfig.asset";
    private const string AttackTag = "Attack";

    private static readonly string[] ClipPaths =
    {
        "Assets/Anim/PlayerAnim/combo_04_1.anim",
        "Assets/Anim/PlayerAnim/combo_04_2.anim",
        "Assets/Anim/PlayerAnim/combo_04_3.anim",
        "Assets/Anim/PlayerAnim/combo_04_4.anim",
        "Assets/Anim/PlayerAnim/combo_04_5.anim"
    };

    private static readonly HashSet<string> ManagedEventNames = new()
    {
        "BeginAttackStage",
        "EnableWeaponDamage",
        "DisableWeaponDamage",
        "OpenComboInputWindow",
        "CloseComboInputWindow"
    };

    [MenuItem("Tools/3DActGame/P1/Apply Animation Driven Combat")]
    public static void Apply()
    {
        PlayerAttackConfigSO config = GetOrCreateConfig();
        ConfigureAnimatorController();
        ConfigureAnimationEvents(config);
        BindPlayerPrefab(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("[AnimationDrivenCombatEditor] P1 应用完成：五段攻击已由 Animation Event 驱动。");
    }

    [MenuItem("Tools/3DActGame/P1/Validate Animation Driven Combat")]
    public static void Validate()
    {
        PlayerAttackConfigSO config = AssetDatabase.LoadAssetAtPath<PlayerAttackConfigSO>(ConfigAssetPath);
        if (config == null || config.MaxComboStage != ClipPaths.Length)
            throw new UnityException("PlayerAttackConfig 必须包含完整的五段连招配置。");

        ValidateConfig(config);
        ValidateAnimatorController();
        ValidateAnimationEvents(config);
        ValidatePlayerPrefab(config);
        Debug.Log("[AnimationDrivenCombatEditor] P1 验证通过：配置、Attack Tag、动画事件和 Prefab 引用均正确。");
    }

    private static PlayerAttackConfigSO GetOrCreateConfig()
    {
        PlayerAttackConfigSO config = AssetDatabase.LoadAssetAtPath<PlayerAttackConfigSO>(ConfigAssetPath);
        if (config != null)
            return config;

        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder(ConfigFolderPath);
        config = ScriptableObject.CreateInstance<PlayerAttackConfigSO>();
        AssetDatabase.CreateAsset(config, ConfigAssetPath);

        // 默认窗口按当前五段动画和 Animator 过渡点设置，之后可在 Inspector 中逐帧微调。
        float[,] timeline =
        {
            { 0.16f, 0.34f, 0.20f, 0.48f },
            { 0.14f, 0.30f, 0.20f, 0.38f },
            { 0.27f, 0.48f, 0.32f, 0.60f },
            { 0.44f, 0.67f, 0.50f, 0.80f },
            { 0.30f, 0.62f, 0.55f, 0.75f }
        };
        float[] damages = { 10f, 15f, 20f, 25f, 30f };

        SerializedObject serializedConfig = new(config);
        SerializedProperty attacks = serializedConfig.FindProperty("m_Attacks");
        attacks.arraySize = ClipPaths.Length;
        for (int index = 0; index < ClipPaths.Length; index++)
        {
            SerializedProperty attack = attacks.GetArrayElementAtIndex(index);
            attack.FindPropertyRelative("m_ComboStage").intValue = index + 1;
            attack.FindPropertyRelative("m_Damage").floatValue = damages[index];
            attack.FindPropertyRelative("m_CameraShakeIntensity").floatValue = 0.15f + index * 0.025f;
            attack.FindPropertyRelative("m_HitStopDuration").floatValue = 0.04f + index * 0.01f;
            attack.FindPropertyRelative("m_DamageStartTime").floatValue = timeline[index, 0];
            attack.FindPropertyRelative("m_DamageEndTime").floatValue = timeline[index, 1];
            attack.FindPropertyRelative("m_ComboInputStartTime").floatValue = timeline[index, 2];
            attack.FindPropertyRelative("m_ComboInputEndTime").floatValue = timeline[index, 3];
        }

        serializedConfig.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return config;
    }

    private static void ConfigureAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        if (controller == null)
            throw new MissingReferenceException("找不到 Player Animator Controller。");

        HashSet<AnimationClip> attackClips = ClipPaths
            .Select(path => AssetDatabase.LoadAssetAtPath<AnimationClip>(path))
            .ToHashSet();

        foreach (AnimatorControllerLayer layer in controller.layers)
            TagAttackStates(layer.stateMachine, attackClips);

        AnimatorControllerParameter[] parameters = controller.parameters;
        foreach (AnimatorControllerParameter parameter in parameters)
        {
            if (parameter.name == "ComboStage")
                parameter.defaultInt = 0;
        }
        controller.parameters = parameters;

        EditorUtility.SetDirty(controller);
    }

    private static void TagAttackStates(AnimatorStateMachine stateMachine, HashSet<AnimationClip> attackClips)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.motion is AnimationClip clip && attackClips.Contains(clip))
                childState.state.tag = AttackTag;
        }

        foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
            TagAttackStates(child.stateMachine, attackClips);
    }

    private static void ConfigureAnimationEvents(PlayerAttackConfigSO config)
    {
        for (int stage = 1; stage <= ClipPaths.Length; stage++)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPaths[stage - 1]);
            if (clip == null || !config.TryGetAttack(stage, out PlayerAttackConfigSO.AttackDefinition attack))
                throw new MissingReferenceException($"缺少第 {stage} 段动画或攻击配置。");

            List<AnimationEvent> events = AnimationUtility.GetAnimationEvents(clip)
                .Where(animationEvent => !ManagedEventNames.Contains(animationEvent.functionName))
                .ToList();

            float firstFrame = Mathf.Min(clip.length * 0.02f, 1f / clip.frameRate);
            events.Add(CreateEvent("BeginAttackStage", firstFrame, stage));
            events.Add(CreateEvent("EnableWeaponDamage", attack.DamageStartTime * clip.length));
            events.Add(CreateEvent("DisableWeaponDamage", attack.DamageEndTime * clip.length));
            events.Add(CreateEvent("OpenComboInputWindow", attack.ComboInputStartTime * clip.length));
            events.Add(CreateEvent("CloseComboInputWindow", attack.ComboInputEndTime * clip.length));

            AnimationUtility.SetAnimationEvents(clip, events
                .OrderBy(animationEvent => animationEvent.time)
                .ThenBy(animationEvent => GetEventOrder(animationEvent.functionName))
                .ToArray());
            EditorUtility.SetDirty(clip);
        }
    }

    private static AnimationEvent CreateEvent(string functionName, float time, int intParameter = 0)
    {
        return new AnimationEvent
        {
            functionName = functionName,
            time = time,
            intParameter = intParameter
        };
    }

    private static int GetEventOrder(string functionName)
    {
        return functionName switch
        {
            "BeginAttackStage" => 0,
            "EnableWeaponDamage" => 1,
            "OpenComboInputWindow" => 2,
            "DisableWeaponDamage" => 3,
            "CloseComboInputWindow" => 4,
            _ => 5
        };
    }

    private static void BindPlayerPrefab(PlayerAttackConfigSO config)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Player player = root.GetComponent<Player>();
            if (player == null)
                throw new MissingComponentException("Player.prefab 根节点缺少 Player。");

            SerializedObject serializedPlayer = new(player);
            serializedPlayer.FindProperty("m_AttackConfig").objectReferenceValue = config;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateConfig(PlayerAttackConfigSO config)
    {
        for (int stage = 1; stage <= ClipPaths.Length; stage++)
        {
            if (!config.TryGetAttack(stage, out PlayerAttackConfigSO.AttackDefinition attack))
                throw new UnityException($"PlayerAttackConfig 缺少第 {stage} 段。");
            if (attack.Damage < 0f)
                throw new UnityException($"第 {stage} 段伤害不能为负数。");
            if (!(attack.DamageStartTime < attack.DamageEndTime))
                throw new UnityException($"第 {stage} 段伤害开始时间必须早于结束时间。");
            if (!(attack.ComboInputStartTime < attack.ComboInputEndTime))
                throw new UnityException($"第 {stage} 段输入窗口开始时间必须早于结束时间。");
        }

        if (!config.TryGetAttack(1, out PlayerAttackConfigSO.AttackDefinition firstAttack)
            || !Mathf.Approximately(firstAttack.Damage, 10f))
        {
            throw new UnityException("第一段攻击伤害必须保持为 10。");
        }
    }

    private static void ValidateAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        HashSet<AnimationClip> expectedClips = ClipPaths
            .Select(path => AssetDatabase.LoadAssetAtPath<AnimationClip>(path))
            .ToHashSet();
        HashSet<AnimationClip> taggedClips = new();

        foreach (AnimatorControllerLayer layer in controller.layers)
            CollectTaggedAttackClips(layer.stateMachine, taggedClips);

        if (!expectedClips.SetEquals(taggedClips))
            throw new UnityException("五段连招状态必须且只能使用 Attack Tag。");

        AnimatorControllerParameter comboStage = controller.parameters
            .Single(parameter => parameter.name == "ComboStage");
        if (comboStage.defaultInt != 0)
            throw new UnityException("Animator 的 ComboStage 默认值必须为 0。");
    }

    private static void CollectTaggedAttackClips(AnimatorStateMachine stateMachine, HashSet<AnimationClip> clips)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.tag == AttackTag && childState.state.motion is AnimationClip clip)
                clips.Add(clip);
        }

        foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
            CollectTaggedAttackClips(child.stateMachine, clips);
    }

    private static void ValidateAnimationEvents(PlayerAttackConfigSO config)
    {
        for (int stage = 1; stage <= ClipPaths.Length; stage++)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPaths[stage - 1]);
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            foreach (string eventName in ManagedEventNames)
            {
                if (events.Count(animationEvent => animationEvent.functionName == eventName) != 1)
                    throw new UnityException($"{clip.name} 必须包含且仅包含一个 {eventName} 事件。");
            }

            AnimationEvent beginEvent = events.Single(animationEvent => animationEvent.functionName == "BeginAttackStage");
            if (beginEvent.intParameter != stage)
                throw new UnityException($"{clip.name} 的 BeginAttackStage 参数应为 {stage}。");

            config.TryGetAttack(stage, out PlayerAttackConfigSO.AttackDefinition attack);
            ValidateEventTime(events, "EnableWeaponDamage", attack.DamageStartTime * clip.length, clip);
            ValidateEventTime(events, "DisableWeaponDamage", attack.DamageEndTime * clip.length, clip);
            ValidateEventTime(events, "OpenComboInputWindow", attack.ComboInputStartTime * clip.length, clip);
            ValidateEventTime(events, "CloseComboInputWindow", attack.ComboInputEndTime * clip.length, clip);
        }
    }

    private static void ValidateEventTime(AnimationEvent[] events, string name, float expectedTime, AnimationClip clip)
    {
        float actualTime = events.Single(animationEvent => animationEvent.functionName == name).time;
        if (!Mathf.Approximately(actualTime, expectedTime))
            throw new UnityException($"{clip.name} 的 {name} 时间与攻击配置不一致。");
    }

    private static void ValidatePlayerPrefab(PlayerAttackConfigSO expectedConfig)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Player player = root.GetComponent<Player>();
            SerializedObject serializedPlayer = new(player);
            if (serializedPlayer.FindProperty("m_AttackConfig").objectReferenceValue != expectedConfig)
                throw new MissingReferenceException("Player.prefab 未绑定 PlayerAttackConfig。");
            if (serializedPlayer.FindProperty("m_WeaponDamage").objectReferenceValue == null)
                throw new MissingReferenceException("Player.prefab 未绑定 WeaponDamage。");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
