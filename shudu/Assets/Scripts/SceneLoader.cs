using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // 在 Inspector 中指定目标场景名称（需已添加到 Build Settings）
    public string targetSceneName;

    // 供 UI 按钮调用的方法
    public void LoadTargetScene()
    {
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogError("未设置目标场景名称！");
        }
    }
}