using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildScript
{
    /// <summary>
    /// GitHub 公开仓库不包含第三方美术源文件，因此 CI 只构建临时空场景来验证脚本和程序集。
    /// 完整可玩版本必须在拥有合法本机资源的环境调用 BuildPortfolioWindows。
    /// </summary>
    public static void Build()
    {
        // 创建临时空场景用于编译验证
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string tempScenePath = "Assets/__CI_BuildScene.unity";
        EditorSceneManager.SaveScene(scene, tempScenePath);

        var buildOptions = new BuildPlayerOptions
        {
            scenes = new[] { tempScenePath },
            locationPathName = "build/StandaloneWindows64/3DActGame.exe",
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

    public static void BuildPortfolioWindows()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new BuildFailedException("Build Settings 中没有启用场景。");

        var buildOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/PortfolioWindows/3DActGame.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"Portfolio Windows build failed: {report.summary.result}");

        Debug.Log($"[BuildScript] Portfolio Windows build succeeded: {report.summary.totalSize} bytes");
    }
}
