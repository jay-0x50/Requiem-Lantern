using System;
using System.Collections.Generic;

namespace RequiemLantern.Core
{
    /// <summary>Provisional balance values. Names, elements and roles follow the supplied design.</summary>
    public static class PrototypeData
    {
        public const int PartyCapacity = 4;

        public static BattleState CreateBattle(ProgressData progress = null, int? seed = null)
        {
            var state = new BattleState { BattleId = Guid.NewGuid().ToString("N"), RandomSeed = seed ?? Guid.NewGuid().GetHashCode(), IsBoss = true };
            IEnumerable<string> party=new[] { "lian", "sera", "minato", "noa" };
            if(progress!=null){
                var collection=new ProgressService(progress);
                if(progress.ActiveParty.Count!=PartyCapacity||new HashSet<string>(progress.ActiveParty).Count!=PartyCapacity||progress.ActiveParty.Exists(id=>!collection.IsOwned(id)))
                    throw new InvalidOperationException("Battle party must contain four distinct owned allies.");
                party=progress.ActiveParty;
            }
            foreach (string id in party)
            {
                var growth = progress == null || progress.Characters == null ? null : progress.Characters.Find(x => x.Id == id);
                state.Allies.Add(CreateCharacter(id, growth == null ? 0 : growth.SyncLevel));
            }
            var anger = new UnitState { Id = "anger", Name = "분노", Portrait = "Anger", Role = "그림자 군체", IsEnemy = true, Element = Element.Shadow, Hp = 360, MaxHp = 360, Attack = 12, Defense = 2, Speed = 11, Rift = 125, MaxRift = 125 };
            anger.Parts.Add(new PartState { Id = "anger_arm", Name = "팔", Hp = 40, MaxHp = 40 });
            anger.Parts.Add(new PartState { Id = "anger_core", Name = "핵", Hp = 72, MaxHp = 72 });
            state.Enemies.Add(anger);
            BattleRules.ApplyActionProgression(state);
            return state;
        }

        public static UnitState CreateCharacter(string id, int syncLevel = 0)
        {
            UnitState unit;
            switch (id)
            {
                case "lian":
                    unit = Ally(id, "리안", "수호자", Element.Radiance, 150, 12);
                    unit.Skills.Add(Attack("lian_glimmer", "잔광", unit.Element, 1, 0, 1, 12, 6, 4, 2));
                    unit.Skills.Add(Attack("lian_breakthrough", "방벽 돌파", unit.Element, 2, 2, 1, 12, 9, 4, 3));
                    unit.Skills.Add(Attack("lian_crescendo", "고조", unit.Element, 3, 3, 1, 12, 15, 5, 4));
                    unit.Skills.Add(Defense("lian_guard", "빛의 호위", unit.Element, SkillKind.Guard, 18, 6, 4, 2));
                    break;
                case "sera":
                    unit = Ally(id, "세라", "돌격수", Element.Flame, 110, 14); unit.Attack = 12;
                    unit.Skills.Add(Attack("sera_glimmer", "잔광", unit.Element, 1, 0, 1, 14, 5, 4, 2));
                    var slash = Attack("sera_slash", "화염 베기", unit.Element, 2, 1, 1, 14, 8, 5, 2); slash.PartDamage = 22; unit.Skills.Add(slash);
                    unit.Skills.Add(Attack("sera_crescendo", "고조", unit.Element, 3, 3, 1, 14, 13, 6, 4));
                    unit.Skills.Add(Defense("sera_evade", "불꽃 걸음", unit.Element, SkillKind.Evade, 18, 7, 4, 2));
                    break;
                case "minato":
                    unit = Ally(id, "미나토", "치유사", Element.Tide, 100, 10);
                    unit.Skills.Add(Attack("minato_glimmer", "잔광", unit.Element, 1, 0, 1, 10, 5, 3, 2));
                    unit.Skills.Add(Healing("minato_heal", "치유 등불", unit.Element, 2, 1, 15, 12, 5, 3, 2));
                    unit.Skills.Add(Healing("minato_care", "생명의 파도", unit.Element, 3, 2, 15, 28, 8, 5, 3));
                    unit.Skills.Add(Defense("minato_guard", "물결 방벽", unit.Element, SkillKind.Guard, 18, 5, 3, 2));
                    break;
                case "noa":
                    unit = Ally(id, "노아", "기록자", Element.Shadow, 120, 13);
                    unit.Skills.Add(Attack("noa_glimmer", "그림자 자국", unit.Element, 1, 0, 1, 13, 6, 3, 2));
                    unit.Skills.Add(Attack("noa_script", "지워진 기록", unit.Element, 2, 1, 1, 13, 8, 4, 3));
                    unit.Skills.Add(Attack("noa_crescendo", "되찾은 이름", unit.Element, 3, 3, 1, 13, 14, 5, 4));
                    unit.Skills.Add(Defense("noa_evade", "그림자 접기", unit.Element, SkillKind.Evade, 18, 6, 5, 2));
                    break;
                case "heidel":
                    unit = Ally(id, "하이델", "관측자", Element.Frost, 120, 11);
                    unit.Skills.Add(Attack("heidel_glimmer", "서리 측정", unit.Element, 1, 0, 1, 11, 6, 3, 2));
                    unit.Skills.Add(Attack("heidel_calibrate", "균열 조율", unit.Element, 2, 1, 1, 11, 9, 4, 2));
                    unit.Skills.Add(Attack("heidel_crescendo", "관측의 끝", unit.Element, 3, 3, 1, 11, 15, 5, 4));
                    unit.Skills.Add(Defense("heidel_counter", "역상 보정", unit.Element, SkillKind.Counter, 18, 6, 4, 2));
                    break;
                case "kai":
                    unit = Ally(id, "카이", "등불지기", Element.Radiance, 115, 12);
                    unit.Skills.Add(Attack("kai_glimmer", "등불의 흔적", unit.Element, 1, 0, 1, 12, 5, 3, 2));
                    unit.Skills.Add(Attack("kai_signal", "귀항 신호", unit.Element, 2, 1, 1, 12, 8, 3, 2));
                    unit.Skills.Add(Attack("kai_crescendo", "길을 여는 빛", unit.Element, 3, 2, 1, 12, 13, 4, 3));
                    unit.Skills.Add(Defense("kai_guard", "등불 울타리", unit.Element, SkillKind.Guard, 18, 6, 4, 2));
                    break;
                case "mare":
                    unit = Ally(id, "마레", "인양꾼", Element.Flame, 125, 12);
                    unit.Skills.Add(Attack("mare_line", "당김줄", unit.Element, 1, 0, 1, 12, 5, 3, 2));
                    unit.Skills.Add(Attack("mare_hook", "닻걸이", unit.Element, 2, 1, 1, 12, 8, 4, 2));
                    unit.Skills.Add(Attack("mare_return", "돌아오는 불", unit.Element, 3, 2, 1, 12, 12, 4, 3));
                    unit.Skills.Add(Defense("mare_evade", "줄넘김", unit.Element, SkillKind.Evade, 18, 6, 4, 2));
                    break;
                default: throw new ArgumentException("Unknown character: " + id, "id");
            }
            unit.SyncLevel = Math.Max(0, syncLevel);CombatSkillFlavor.Configure(unit); return unit;
        }

        public static bool IsCharacter(string id) { return id == "lian" || id == "sera" || id == "minato" || id == "noa" || id == "heidel" || id == "kai" || id == "mare"; }
        private static UnitState Ally(string id, string name, string role, Element element, int hp, int speed)
        { return new UnitState { Id = id, Name = name, Role = role, Portrait = id, Element = element, Hp = hp, MaxHp = hp, Speed = speed, Sanity = 0 }; }
        private static SkillData Attack(string id, string name, Element element, int number, int cost, int slots, int speed, int basePower, int flamePower, int flames)
        {
            return new SkillData { Id = id, Name = name, Number = number, RequiredSyncLevel = number == 3 ? 1 : 0, Element = element, Cost = cost, Slots = slots, Speed = speed,
                BasePower = basePower, FlamePower = flamePower, FlameCount = flames, HitPowers = DisplayHits(basePower, flamePower, flames), Kind = SkillKind.Attack,
                RiftDamage = number == 3 ? 30 : number == 2 ? 14 : 9, PartDamage = number == 3 ? 32 : number == 2 ? 22 : 14 };
        }
        private static SkillData Defense(string id, string name, Element element, SkillKind kind, int speed, int basePower, int flamePower, int flames)
        { return new SkillData { Id = id, Name = name, Element = element, Kind = kind, Cost = 0, Speed = speed, BasePower = basePower, FlamePower = flamePower, FlameCount = flames, HitPowers = DisplayHits(basePower, flamePower, flames) }; }
        private static SkillData Healing(string id, string name, Element element, int number, int cost, int speed, int heal, int basePower, int flamePower, int flames)
        { return new SkillData { Id = id, Name = name, Number = number, RequiredSyncLevel = number == 3 ? 1 : 0, Element = element, Kind = SkillKind.Heal, Cost = cost, Speed = speed, Heal = heal, BasePower = basePower, FlamePower = flamePower, FlameCount = flames, HitPowers = DisplayHits(basePower, flamePower, flames) }; }
        private static int[] DisplayHits(int basePower, int flamePower, int flames)
        { var values = new int[flames]; for (int i = 0; i < flames; i++) values[i] = basePower + flamePower * (i + 1); return values; }
    }

    [Serializable]
    public sealed class StoryLine
    {
        public string Speaker, Text, Background, Portrait;
        public string Location, Caption, MusicCue, ListenerPortrait;
        public bool IsPrototype;
        public float AutoDelay = 2.6f;
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
