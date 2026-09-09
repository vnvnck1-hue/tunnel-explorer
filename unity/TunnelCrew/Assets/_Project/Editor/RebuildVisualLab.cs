using UnityEditor;
using UnityEngine;
using TunnelCrew.Presentation.Visual;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// Visual Lab 을 에디터에서 다시 조립한다.
    ///
    /// <b>왜 필요한가</b> — <see cref="VisualLabController"/> 는 평범한 MonoBehaviour 라서
    /// 조명·머티리얼·그림자를 만드는 <c>Rebuild()</c> 가 <c>Start()</c> 에서만 돈다. 씬을 열기만
    /// 해서는 저장된 옛 상태가 보이고, 프로파일·셰이더 값을 바꿔도 화면이 그대로다.
    /// 2026-09-09 에 "고쳤는데 씬에서 차이가 없다" 가 바로 이것이었다.
    ///
    /// 캡처 세트(<see cref="CaptureVisualLab"/>)도 같은 일을 하지만 32 장을 찍느라 오래 걸린다.
    /// 값만 만지며 눈으로 확인할 때는 이쪽이 맞다.
    /// </summary>
    static class RebuildVisualLab
    {
        [MenuItem("Tunnel Crew/비주얼 · Visual Lab 재조립 (현재 값 반영)", priority = 20)]
        static void Rebuild()
        {
            var lab = Object.FindFirstObjectByType<VisualLabController>();
            if (lab == null)
            {
                EditorUtility.DisplayDialog("Visual Lab",
                    "열려 있는 씬에 VisualLabController 가 없다.\n" +
                    "Assets/_Project/Scenes/VisualLab.unity 를 먼저 연다.", "확인");
                return;
            }

            lab.Rebuild();
            lab.EditorTick();
            SceneView.RepaintAll();
            Debug.Log("[비주얼] Visual Lab 재조립 완료 — 프로파일·셰이더의 현재 값이 화면에 반영됐다.");
        }
    }
}
