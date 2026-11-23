using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Night0333
{
    /// <summary>하루 근무 기록(결과 화면 내용).</summary>
    [Serializable]
    public class DayRecord
    {
        public int day;
        public int seed;
        public int correct;
        public List<ChecklistEntry> entries = new List<ChecklistEntry>();
    }

    /// <summary>세이브 파일 내용.</summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>다음에 할 날(1~7).</summary>
        public int nextDay = 1;
        public List<DayRecord> days = new List<DayRecord>();
        /// <summary>흔적: 이상한 손님을 "예"로 통과시킨 수(7일 합계). 3이면 죽음.</summary>
        public int traces;
        /// <summary>실종: 평범한 손님을 "아니오"로 돌려보낸 수(7일 합계). 3이면 체포.</summary>
        public int missing;
        /// <summary>경찰이 와서 알린 실종 수(전단이 붙은 수).</summary>
        public int policeSeen;
        public List<string> missingIds = new List<string>();
        /// <summary>죽은 횟수(흔적 3으로 괴물에게 죽고 그날을 다시 한 수). 메인 화면에 괴물이 비친다.</summary>
        public int deaths;
        /// <summary>이미 나온 3:33 이벤트 번호(7일 동안 같은 것이 다시 나오지 않게).</summary>
        public List<int> events333 = new List<int>();
        /// <summary>저장된 날의 시작 상태(다음 근무 시작에 체포되면 그날을 다시 하려고 남긴다).</summary>
        public string prevStartJson = "";

        public SaveData Clone() => JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(this));
    }

    /// <summary>
    /// 로컬 세이브(JSON 파일 하나). 하루 근무가 끝나 결과 화면을 본 뒤 자동으로 저장한다.
    /// 파일: Application.persistentDataPath/save0333.json
    /// </summary>
    public static class SaveSystem
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "save0333.json");

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath)) return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[0333] 세이브를 읽지 못함: " + e.Message);
            }
            return new SaveData();
        }

        public static void Save(SaveData data)
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[0333] 세이브를 쓰지 못함: " + e.Message);
            }
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
