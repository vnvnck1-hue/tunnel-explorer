using UnityEngine;
using UnityEngine.Rendering;

namespace TunnelCrew.Presentation.Prototype
{
    /// <summary>발 위치가 화면 아래에 있을수록 앞에 그려지는 결정론적 탑다운 정렬.</summary>
    [DefaultExecutionOrder(900)]
    public sealed class ModularGunnerDepthSorter : MonoBehaviour
    {
        const int BaseOrder = 100;
        const float OrdersPerUnit = 4f;

        SortingGroup _group;

        public static int OrderFor(float worldY) => BaseOrder - Mathf.RoundToInt(worldY * OrdersPerUnit);

        void Awake()
        {
            _group = GetComponent<SortingGroup>();
            Apply();
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            if (_group != null) _group.sortingOrder = OrderFor(transform.position.y);
        }
    }
}
