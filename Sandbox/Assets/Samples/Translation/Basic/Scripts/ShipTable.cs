using System.Collections.Generic;
using Translation;
using Translation.Unity;
using UnityEngine;

namespace TranslationSample
{
    /// <summary> ShipData.json 의 한 행. 실제 프로젝트의 데이터 클래스에 해당한다 (class 여야 한다. struct 는 안 된다) </summary>
    public class Ship
    {
        public int Id;
        public string Name;
        public string Description;
        public ShipGrade Grade;
        public int Power;
    }

    public enum ShipGrade
    {
        Rare,
        Epic,
    }

    /// <summary>
    /// 게임 표를 읽고, 번역 대상 컬럼(Name · Description)을 지금 언어로 바꿔 넣는다.
    /// 실제 프로젝트에서는 "DB 를 다 읽은 직후" 한 자리에서 표마다 Inject 를 한 번씩 부르면 된다.
    /// JSON 읽는 부분은 프로젝트 것을 쓰면 되고, 여기서는 SDK 에 든 MiniJson 으로 대신했다
    /// </summary>
    public static class ShipTable
    {
        private const string ResourcePath = "TranslationSample/GameData/ShipData";

        // 어느 표의 어느 컬럼을 바꿀지. Translation Settings 의 Targets 와 같은 내용이다
        private static readonly TableTarget Target =
            new TableTarget("ShipData", "Name", "Description") { IdColumn = "Id" };

        public static List<Ship> Load()
        {
            var ships = Read();

            if (TranslationRuntime.IsLoaded)
            {
                // 행 객체의 Name · Description 을 지금 언어 값으로 덮어쓴다. 번역이 없는 행은 원문이 남는다
                var report = new FieldInjector(TranslationRuntime.Catalog).Inject(Target, ships);
                if (report.Warnings.Count > 0)
                    Debug.LogWarning("[Sample] ShipData 번역 넣기 경고: " + string.Join(" / ", report.Warnings));
            }

            return ships;
        }

        private static List<Ship> Read()
        {
            var ships = new List<Ship>();
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError("[Sample] Resources 에 ShipData.json 이 없다: " + ResourcePath);
                return ships;
            }

            foreach (var item in (List<object>)MiniJson.Parse(asset.text))
            {
                var map = (Dictionary<string, object>)item;
                ships.Add(new Ship
                {
                    Id = MiniJson.GetInt(map, "Id"),
                    Name = MiniJson.GetString(map, "Name"),
                    Description = MiniJson.GetString(map, "Description"),
                    Grade = MiniJson.GetString(map, "Grade") == "Epic" ? ShipGrade.Epic : ShipGrade.Rare,
                    Power = MiniJson.GetInt(map, "Power"),
                });
            }

            return ships;
        }
    }
}
