using System;
using System.Collections.Generic;

namespace RequiemLantern.Core
{
    public interface IBattleRandom { double NextDouble(); }
    public sealed class SeededBattleRandom : IBattleRandom
    {
        private readonly Random random;
        public SeededBattleRandom(int seed) { random = new Random(seed); }
        public double NextDouble() { return random.NextDouble(); }
    }
    public enum Element { Radiance, Flame, Tide, Frost, Shadow }
    public enum LinkKind { Strike, Clash, Guard, Support }
    public enum SkillKind { Attack, Guard, Heal, Evade, Counter }
    public enum IgniteGrade { Miss, Good, Perfect }
    public enum BattlePhase { Observe, Link, Resolve, WrapUp, Finished }
    public enum BattleOutcome { Ongoing, Victory, Defeat }
    public enum IntentKind { Normal, Area, Piercing, Illusion }
    public enum PreviewGrade { Advantage, Close, Disadvantage }

    [Serializable]
    public sealed class SkillData
    {
        public string Id, Name;
        public int Cost, Slots = 1, Speed, RiftDamage = 5, PartDamage = 7, Heal = 20;
        public int Number, RequiredSyncLevel, BasePower, FlamePower, FlameCount;
        public int[] HitPowers = new int[0];
        public Element Element;
        public SkillKind Kind;
        public bool IsPrototype = true;
    }

    [Serializable]
    public sealed class PartState
    {
        public string Id, Name;
        public int Hp, MaxHp;
        public bool Destroyed { get { return Hp <= 0; } }
    }

    [Serializable]
    public sealed class SkillCard
    {
        public string Id, SkillId;
    }

    [Serializable]
    public sealed class UnitState
    {
        public string Id, Name, Role, Portrait, IntentProfile;
        public int Hp, MaxHp, Speed, Attack = 10, Defense = 1;
        public int Sanity, SyncLevel;
        public int Rift, MaxRift, BrokenThroughRound, FrozenThroughRound;
        public Element Element;
        public bool IsEnemy, IsSupport, IsHuman;
        public List<SkillData> Skills = new List<SkillData>();
        public List<PartState> Parts = new List<PartState>();
        public List<SkillCard> SkillHand = new List<SkillCard>();
        public List<string> DrawPile = new List<string>(), DiscardPile = new List<string>();
        public int NextCardNumber;
        public bool DeckInitialized;
        public bool Alive { get { return Hp > 0; } }
        public int SlotCount { get { return IsSupport ? 1 : 2; } }
    }

    [Serializable]
    public sealed class IntentState
    {
        public string Id, ActorId, TargetId, PartId, Name;
        public int[] HitPowers = new int[0];
        public int Speed;
        public int BasePower, FlamePower, FlameCount;
        public Element Element;
        public IntentKind Kind;
        public bool Cancelled, Resolved;
    }

    [Serializable]
    public sealed class LinkCommand
    {
        public string ActorId, SkillId, TargetId, Reason, CardId;
        public int Slot;
        public LinkKind Kind;
        public LinkCommand Copy() { return (LinkCommand)MemberwiseClone(); }
    }

    public sealed class ClashPreview
    {
        public PreviewGrade Grade;
        public int AllyHits, EnemyHits, Ties;
        public float AllyPower, EnemyPower;
        public string Summary;
    }

    [Serializable]
    public sealed class BattleEvent
    {
        public string Type, ActorId, TargetId, Message;
        public int Amount, Round;
        public bool[] FlameStates = new bool[0], EnemyFlameStates = new bool[0];
        public float ActorPower, TargetPower;
        public int ActorSanity, TargetSanity, ClashIndex, WinnerStreak;
        public string SkillId, SkillName;
        public int SkillNumber, BasePower, FlamePower, EnemyBasePower, EnemyFlamePower;
        public BattleEvent(string type, string actor, string target, string message, int amount, int round)
        { Type = type; ActorId = actor; TargetId = target; Message = message; Amount = amount; Round = round; }
    }

    [Serializable]
    public sealed class BattleState
    {
        public string BattleId, StageId = "chapter1_beacon_defense", StageName = "1-1  꺼진 등대";
        public int Round = 1, Ember = 3, Lantern, Gloom = 1, IgniteChain;
        public int RandomSeed;
        public int CoinReward = 120, FirstLanternReward = 10, RepeatLanternReward = 2, MemoryReward = 1;
        public bool LanternQueued;
        public string LanternActorId;
        public BattlePhase Phase = BattlePhase.Link;
        public BattleOutcome Outcome = BattleOutcome.Ongoing;
        public List<UnitState> Allies = new List<UnitState>();
        public List<UnitState> Enemies = new List<UnitState>();
        public List<IntentState> Intents = new List<IntentState>();
        public List<LinkCommand> Links = new List<LinkCommand>();
        public List<BattleEvent> Events = new List<BattleEvent>();
    }

    public static class BattleRules
    {
        public const int MaxEmber = 6;
        public const int MinSanity = -20, MaxSanity = 20, MaxClashContests = 64;
        public static int ClampSanity(int sanity) { return Math.Max(MinSanity, Math.Min(MaxSanity, sanity)); }
        public static double FlameChance(int sanity) { return (50 + 2 * ClampSanity(sanity)) / 100.0; }
        public static bool IsDefensive(SkillData skill)
        { return skill != null && (skill.Kind == SkillKind.Guard || skill.Kind == SkillKind.Evade || skill.Kind == SkillKind.Counter); }
        public static bool IsSkillUnlocked(UnitState actor, SkillData skill)
        { return actor != null && skill != null && actor.SyncLevel >= Math.Max(skill.RequiredSyncLevel, skill.Number == 3 ? 1 : 0); }
        // Legacy hit arrays remain readable by presentation and older authored data.
        // A skill now rolls a single total power; the array only supplies fallback flame count.
        public static int Flames(SkillData skill) { return Math.Max(1, skill.FlameCount > 0 ? skill.FlameCount : skill.HitPowers.Length); }
        public static int Flames(IntentState intent) { return Math.Max(1, intent.FlameCount > 0 ? intent.FlameCount : intent.HitPowers.Length); }
        public static int Base(SkillData skill) { return skill.BasePower > 0 ? skill.BasePower : LegacyBase(skill.HitPowers); }
        public static int Base(IntentState intent) { return intent.BasePower > 0 ? intent.BasePower : LegacyBase(intent.HitPowers); }
        public static int Flame(SkillData skill) { return Math.Max(0, skill.FlamePower > 0 ? skill.FlamePower : 2); }
        public static int Flame(IntentState intent) { return Math.Max(0, intent.FlamePower > 0 ? intent.FlamePower : 2); }
        private static int LegacyBase(int[] hits)
        { if (hits == null || hits.Length == 0) return 4; int total = 0; foreach (int hit in hits) total += hit; return Math.Max(1, total / hits.Length - 2); }
        public static float Affinity(Element attack, Element defense)
        {
            if ((attack == Element.Flame && defense == Element.Frost) ||
                (attack == Element.Frost && defense == Element.Tide) ||
                (attack == Element.Tide && defense == Element.Flame) ||
                (attack == Element.Radiance && defense == Element.Shadow) ||
                (attack == Element.Shadow && defense == Element.Radiance)) return 1.2f;
            if ((attack == Element.Frost && defense == Element.Flame) ||
                (attack == Element.Tide && defense == Element.Frost) ||
                (attack == Element.Flame && defense == Element.Tide)) return .85f;
            return 1;
        }
        public static IgniteGrade GradeTiming(float offsetMs, int gloom, bool wideWindow = false, bool lantern = false)
        {
            float window = (1f - Math.Max(0, Math.Min(3, gloom)) * .1f) * (wideWindow ? 1.5f : 1f) * (lantern ? 1.2f : 1f);
            float error = Math.Abs(offsetMs);
            return error <= 80 * window ? IgniteGrade.Perfect : error <= 200 * window ? IgniteGrade.Good : IgniteGrade.Miss;
        }
        // Speed orders actions only. Equal power cancels a clash contest and clears streaks.
        public static int CompareHit(float allyPower, int allySpeed, float enemyPower, int enemySpeed)
        {
            if (Math.Abs(allyPower - enemyPower) > .0001f) return allyPower > enemyPower ? 1 : -1;
            return 0;
        }
        public static string ElementName(Element element)
        {
            switch (element) { case Element.Flame: return "화염"; case Element.Frost: return "서리";
                case Element.Tide: return "물결"; case Element.Shadow: return "그림자"; default: return "광휘"; }
        }
    }
}
