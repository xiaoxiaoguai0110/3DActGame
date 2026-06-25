using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildScript
{
    public static void Build()
    {
        // 创建临时空场景用于编译验证
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string tempScenePath = "Assets/__CI_BuildScene.unity";
        EditorSceneManager.SaveScene(scene, tempScenePath);

        var buildOptions = new BuildPlayerOptions
        {
            scenes = new[] { tempScenePath },
            locationPathName = "build/3DActGame.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        BuildSummary summary = report.summary;

        // 清理临时场景
        AssetDatabase.DeleteAsset(tempScenePath);

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Build failed: {summary.result}");
            EditorApplication.Exit(1);
        }

        Debug.Log($"Build succeeded! {summary.totalSize} bytes");
    }
}
