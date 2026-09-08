using System.Collections.Generic;
using TunnelCrew.Presentation.Visual;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    /// <summary>
    /// 기능명세서 §6.4 의 Sorting Layer 12종을 프로젝트에 만든다.
    ///
    /// TagManager.asset 을 직접 편집하지 않고 SerializedObject 로 다룬다 — 에디터가 열려
    /// 있는 동안 파일을 덮어쓰면 다음 포커스에서 되돌아간다.
    ///
    /// 배치 모드:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod TunnelCrew.EditorTools.BuildVisualLayers.Run
    /// </summary>
    public static class BuildVisualLayers
    {
        [MenuItem("Tunnel Crew/비주얼 · 소팅 레이어 생성 (§6.4)")]
        public static void Run()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0)
            {
                Debug.LogError("[비주얼] TagManager.asset 을 열지 못했다.");
                return;
            }

            var so = new SerializedObject(asset[0]);
            var layers = so.FindProperty("m_SortingLayers");
            if (layers == null)
            {
                Debug.LogError("[비주얼] m_SortingLayers 프로퍼티를 찾지 못했다.");
                return;
            }

            // 이미 있는 이름 → 현재 인덱스
            var existing = new Dictionary<string, int>();
            var usedIds = new HashSet<int>();
            for (int i = 0; i < layers.arraySize; i++)
            {
                var e = layers.GetArrayElementAtIndex(i);
                existing[e.FindPropertyRelative("name").stringValue] = i;
                usedIds.Add(e.FindPropertyRelative("uniqueID").intValue);
            }

            // uniqueID 0 으로 저장돼 버린 레이어를 고친다. 0 은 Default 와 같은 값이라
            // 그 레이어를 쓰는 렌더러가 경고 없이 Default 로 떨어진다.
            int repaired = 0;
            for (int i = 0; i < layers.arraySize; i++)
            {
                var e = layers.GetArrayElementAtIndex(i);
                string n = e.FindPropertyRelative("name").stringValue;
                if (n == "Default") continue;
                var idProp = e.FindPropertyRelative("uniqueID");
                if (idProp.intValue != 0) continue;
                idProp.intValue = NewId(n, usedIds);
                repaired++;
            }

            int added = 0;
            foreach (var name in VisualLayers.InOrder)
            {
                if (name == "Default") continue;
                if (existing.ContainsKey(name)) continue;

                int idx = layers.arraySize;
                layers.InsertArrayElementAtIndex(idx);
                var e = layers.GetArrayElementAtIndex(idx);
                e.FindPropertyRelative("name").stringValue = name;
                e.FindPropertyRelative("uniqueID").intValue = NewId(name, usedIds);
                e.FindPropertyRelative("locked").boolValue = false;
                existing[name] = idx;
                added++;
            }

            // §6.4 표의 순서로 재배열한다. Default 는 배열의 어디에 있어도 되지만
            // 신규 레이어보다 뒤에 두면 UI 가 늘 Default 를 가장 앞에 그린다 → 맨 앞에 남긴다.
            var want = new List<string> { "Default" };
            foreach (var n in VisualLayers.InOrder) if (n != "Default") want.Add(n);

            for (int target = 0; target < want.Count; target++)
            {
                int at = IndexOf(layers, want[target]);
                if (at < 0 || at == target) continue;
                layers.MoveArrayElement(at, target);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            var order = new List<string>();
            for (int i = 0; i < layers.arraySize; i++)
                order.Add(layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
            Debug.Log($"[비주얼] 소팅 레이어 {added}개 추가 · ID 복구 {repaired}개. 최종 순서: {string.Join(" < ", order)}");
        }

        static int IndexOf(SerializedProperty layers, string name)
        {
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
                    return i;
            return -1;
        }

        /// <summary>
        /// 이름에서 만든 고정 ID. 같은 프로젝트를 다시 만들어도 같은 값이 나온다.
        ///
        /// 31비트로 자르는 것이 중요하다 — uniqueID 는 int 필드이고,
        /// SerializedProperty.intValue 에 음수를 넣으면 Unity 가 0 으로 되돌린다.
        /// 0 은 "레이어 없음"과 같은 값이라 그 레이어를 지정하는 렌더러가 조용히
        /// Default 로 떨어진다.
        /// </summary>
        static int NewId(string name, HashSet<int> used)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char ch in name) { h ^= ch; h *= 16777619u; }
                int id = (int)(h & 0x7FFFFFFF);
                while (id == 0 || used.Contains(id)) id = id == int.MaxValue ? 1 : id + 1;
                used.Add(id);
                return id;
            }
        }
    }
}
