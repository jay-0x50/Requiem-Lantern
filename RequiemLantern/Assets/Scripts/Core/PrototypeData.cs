using System;
using System.Collections.Generic;

namespace RequiemLantern.Core
{
    /// <summary>Provisional balance values. Names, elements and roles follow the supplied design.</summary>
    public static class PrototypeData
    {
        public static BattleState CreateBattle()
        {
            var state = new BattleState { BattleId = Guid.NewGuid().ToString("N") };
            var lian = Ally("lian", "리안", "수호자", Element.Radiance, 150, 12);
            lian.Skills.Add(Attack("lian_glimmer", "잔광", Element.Radiance, 0, 1, 12, 6, 6));
            lian.Skills.Add(Attack("lian_breakthrough", "방벽 돌파", Element.Radiance, 2, 1, 12, 9, 9, 9));
            lian.Skills.Add(new SkillData { Id = "lian_guard", Name = "빛의 호위", Element = Element.Radiance, Kind = SkillKind.Guard, Cost = 1, Speed = 18 });
            lian.Skills.Add(Attack("lian_crescendo", "고조", Element.Radiance, 3, 2, 12, 10, 10, 10, 12, 14));
            var sera = Ally("sera", "세라", "돌격수", Element.Flame, 110, 14);
            sera.Attack = 12;
            sera.Skills.Add(Attack("sera_glimmer", "잔광", Element.Flame, 0, 1, 14, 6, 7));
            var slash = Attack("sera_slash", "화염 베기", Element.Flame, 1, 1, 14, 8, 10); slash.PartDamage = 10;
            sera.Skills.Add(slash);
            sera.Skills.Add(Attack("sera_crescendo", "고조", Element.Flame, 3, 2, 14, 7, 9, 11, 14));
            var minato = Ally("minato", "미나토", "치유사", Element.Tide, 100, 10);
            minato.Skills.Add(Attack("minato_glimmer", "잔광", Element.Tide, 0, 1, 10, 5, 6));
            minato.Skills.Add(new SkillData { Id = "minato_heal", Name = "치유 등불", Element = Element.Tide, Kind = SkillKind.Heal, Cost = 1, Speed = 15, Heal = 30 });
            minato.Skills.Add(new SkillData { Id = "minato_care", Name = "응급 처치", Element = Element.Tide, Kind = SkillKind.Heal, Cost = 0, Speed = 10, Heal = 12 });
            state.Allies.AddRange(new[] { lian, sera, minato });
            var anger = new UnitState { Id = "anger", Name = "분노", Role = "그림자 군체", IsEnemy = true, Element = Element.Shadow, Hp = 360, MaxHp = 360, Attack = 12, Defense = 2, Speed = 11, Rift = 125, MaxRift = 125 };
            anger.Parts.Add(new PartState { Id = "anger_arm", Name = "팔", Hp = 40, MaxHp = 40 });
            anger.Parts.Add(new PartState { Id = "anger_core", Name = "핵", Hp = 72, MaxHp = 72 });
            state.Enemies.Add(anger);
            return state;
        }

        private static UnitState Ally(string id, string name, string role, Element element, int hp, int speed)
        { return new UnitState { Id = id, Name = name, Role = role, Portrait = id, Element = element, Hp = hp, MaxHp = hp, Speed = speed }; }
        private static SkillData Attack(string id, string name, Element element, int cost, int slots, int speed, params int[] hits)
        { return new SkillData { Id = id, Name = name, Element = element, Cost = cost, Slots = slots, Speed = speed, HitPowers = hits, Kind = SkillKind.Attack }; }
    }

    [Serializable]
    public sealed class StoryLine
    {
        public string Speaker, Text, Background, Portrait;
        public string Location, Caption, MusicCue, ListenerPortrait;
        public bool IsPrototype;
        public StoryLine(string speaker, string text, string background = "ruins", string portrait = "", bool prototype = false)
        { Speaker = speaker; Text = text; Background = background; Portrait = portrait; IsPrototype = prototype; }
    }

    public static class StoryCatalog
    {
        // Source dialogue is verbatim. Added bridging narration is marked for scenario review.
        public static readonly List<StoryLine> Prologue = new List<StoryLine>
        {
            new StoryLine("", "[암전. 바람 소리]", "black"),
            new StoryLine("", "태양이 사라진 지 13년. 소등의 날 이후, 세계는 영야에 잠겨 있다.", "ruins", "", true),
            new StoryLine("카이", "(얼어붙은 폐허에서 눈을 뜬다. 손에는 꺼져가는 낡은 등불 하나.)", "ruins", "", true),
            new StoryLine("", "그림자 군체 ‘부정’이 등불을 향해 다가온다. 카이는 쓰러지고, 빛은 한 점으로 줄어든다.", "ruins", "", true),
            new StoryLine("리안", "…움직이지 마. 그림자가 소리에 반응해.", "ruins", "lian"),
            new StoryLine("카이", "(숨을 삼킨다)", "ruins", "lian"),
            new StoryLine("리안", "살아 있는 사람을 본 건 오랜만이네.\n…그 불, 아직 꺼지지 않았구나.", "ruins", "lian"),
            new StoryLine("", "[등불이 희미하게 번쩍인다]", "ruins", "lian"),
            new StoryLine("", "등불이 리안의 파편과 공명한다. 첫 등불 해방. 두 사람을 덮던 그림자가 물러난다.", "ruins", "lian", true),
            new StoryLine("리안", "(작게) …따뜻해.", "ruins", "lian"),
            new StoryLine("", "리안이 13년 동안 지킨 등대 요새. 마지막 불씨가 남아 있는 곳으로, 두 사람은 걸음을 옮긴다.", "keep", "lian", true)
        };

        public static readonly List<StoryLine> FirstMeal = new List<StoryLine>
        {
            new StoryLine("", "요새의 첫 등불이 켜진 뒤, 세라와 미나토가 합류한다. 생존자들은 식탁에 모인다.", "keep", "", true),
            new StoryLine("세라", "자자, 다 같이 먹자! 오늘은 특별히 통조림 두 개야!", "keep", "sera"),
            new StoryLine("미나토", "…그거 유통기한이 삼 년 지났는데요.", "keep", "minato"),
            new StoryLine("세라", "영양은 변함없어! 아마도!", "keep", "sera"),
            new StoryLine("리안", "(말없이 한 입 먹고) …나쁘지 않아.", "keep", "lian"),
            new StoryLine("세라", "오! 리안이 웃었다!", "keep", "sera"),
            new StoryLine("리안", "…안 웃었어.", "keep", "lian"),
            new StoryLine("", "해안의 안개 너머, ‘분노’ 개체가 등대의 빛을 향해 움직인다. 등불지기는 세 잔영의 빛을 잇는다.", "keep", "", true)
        };

        public static readonly List<StoryLine> Epilogue = new List<StoryLine>
        {
            new StoryLine("", "분노가 물러난 자리에 작은 빛이 남는다. 카이는 리안의 기억 한 조각을 대신 짊어진다.", "keep", "lian", true),
            new StoryLine("기억 속 목소리", "미안해", "black"),
            new StoryLine("", "꺼지던 태양의 잔상이 스친다. 누구의 목소리였을까. 등대의 불은 아직 켜져 있다.", "keep", "", true)
        };
    }
}
