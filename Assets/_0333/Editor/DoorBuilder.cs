using System;
using UnityEditor;
using UnityEngine;

namespace Night0333.EditorTools
{
    /// <summary>정면 자동문(문짝 두 장)에 열림·닫힘과 문 차임 소리를 붙인다. 손님 넣기와 전체 씬 생성 때 함께 실행된다.</summary>
    public static class DoorBuilder
    {
        public const string ChimePath = "Assets/_0333/Audio/door_chime.mp3";

        public static DoorController AddToScene()
        {
            var old = UnityEngine.Object.FindFirstObjectByType<DoorController>(FindObjectsInactive.Include);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            Transform left = FindByName("door_leaf_l"), right = FindByName("door_leaf_r");
            if (left == null || right == null) throw new Exception("문짝 그림(door_leaf_l / door_leaf_r)을 씬에서 찾지 못함");

            var go = new GameObject("Front Door");
            var door = go.AddComponent<DoorController>();
            door.leftLeaf = left;
            door.rightLeaf = right;
            // 한쪽 문짝 폭만큼 밀어야 벽 속으로 다 들어간다
            var lsr = left.GetComponent<SpriteRenderer>();
            if (lsr != null) door.slideDistance = Mathf.Round(lsr.bounds.size.x);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            door.source = src;
            door.chime = AssetDatabase.LoadAssetAtPath<AudioClip>(ChimePath);
            if (door.chime == null) Debug.LogWarning("[0333] 문 차임 소리를 찾지 못함: " + ChimePath);
            EditorUtility.SetDirty(door);
            return door;
        }

        static Transform FindByName(string name)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }
    }
}
