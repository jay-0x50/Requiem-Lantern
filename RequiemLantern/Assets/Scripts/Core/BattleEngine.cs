using System;
using System.Collections.Generic;
using System.Linq;

namespace RequiemLantern.Core
{
    /// <summary>Deterministic rules and presentation events; no Unity dependency.</summary>
    public sealed class BattleEngine
    {
        public BattleState State { get; private set; }
        private readonly Func<BattleState> factory;
        private readonly List<LinkCommand> previousLinks = new List<LinkCommand>();
        private sealed class DefenseAction
        { public UnitState Actor; public SkillData Skill; public RolledPower Roll; public bool Used; }
        private sealed class RolledPower
        { public int Raw; public float Power; public bool[] Flames; }
        private readonly Dictionary<string, DefenseAction> guards = new Dictionary<string, DefenseAction>();
        private IBattleRandom random;
        private readonly bool ownsRandom;
        // Card order has its own random stream, independent of all flame rolls.
        private readonly Dictionary<string, Random> deckRandom = new Dictionary<string, Random>();
        private readonly Dictionary<string, HashSet<Element>> elementsHit = new Dictionary<string, HashSet<Element>>();
        private readonly HashSet<string> reactions = new HashSet<string>();
        private readonly HashSet<string> shattered = new HashSet<string>();
        private int clashEmber, siphonEmber;

        public BattleEngine(BattleState state, Func<BattleState> restartFactory = null, IBattleRandom randomSource = null)
        {
            if (state == null) throw new ArgumentNullException("state");
            State = state; factory = restartFactory; ownsRandom = randomSource == null;
            random = randomSource ?? new SeededBattleRandom(state.RandomSeed);
            BattleRules.ApplyActionProgression(state);
            foreach (var unit in state.Allies.Concat(state.Enemies)) unit.Sanity = BattleRules.ClampSanity(unit.Sanity);
            foreach (var unit in state.Allies) InitializeDeck(unit);
        }

        public static BattleEngine CreatePrototype(ProgressData progress = null, int? seed = null)
        {
            Func<BattleState> create = () => PrototypeData.CreateBattle(progress, seed);
            var engine = new BattleEngine(create(), create);
            engine.Observe(); return engine;
        }
        public void BeginCampaign() { Observe(); }

        public int PlannedCost { get { return State.Links.Sum(x => Skill(x.ActorId, x.SkillId).Cost); } }
        public int AvailableEmber { get { return State.Ember - PlannedCost; } }
        public UnitState Unit(string id) { return State.Allies.Concat(State.Enemies).FirstOrDefault(x => x.Id == id); }
        public SkillData Skill(string actorId, string skillId)
        { var u = Unit(actorId); return u == null ? null : u.Skills.FirstOrDefault(x => x.Id == skillId); }
        public IntentState Intent(string id) { return State.Intents.FirstOrDefault(x => x.Id == id); }
        public UnitState TargetUnit(string targetId)
        { return Unit(targetId) ?? State.Enemies.FirstOrDefault(x => x.Parts.Any(p => p.Id == targetId)); }

        public bool TryLink(string actorId, int slot, string skillId, LinkKind kind, string targetId, out string error, string cardId = null)
        {
            error = null;
            if (State.Phase != BattlePhase.Link || State.Outcome != BattleOutcome.Ongoing) { error = "연결 단계에서만 변경할 수 있습니다."; return false; }
            UnitState actor = Unit(actorId); SkillData skill = Skill(actorId, skillId);
            if (actor == null || actor.IsEnemy || !actor.Alive || skill == null) { error = "행동 가능한 잔영과 스킬을 선택해 주세요."; return false; }
            if (!BattleRules.IsSkillUnlocked(actor, skill)) { error = "동기화하여 3스킬을 개방해야 합니다."; return false; }
            if (slot < 0 || slot >= actor.SlotCount)
            { error = "아직 개방되지 않은 행동 슬롯입니다."; return false; }
            if ((skill.Slots == 2 && slot != 0) || skill.Slots > actor.SlotCount)
            { error = "이 스킬은 빈 슬롯 두 칸이 필요합니다."; return false; }
            if (actor.IsSupport && kind != LinkKind.Guard && kind != LinkKind.Support) { error = "서포트는 호위·지원 연결만 가능합니다."; return false; }
            if ((skill.Kind == SkillKind.Attack && kind != LinkKind.Strike && kind != LinkKind.Clash) ||
                (skill.Kind == SkillKind.Heal && kind != LinkKind.Support) || (BattleRules.IsDefensive(skill) && kind != LinkKind.Guard))
            { error = "스킬과 연결 종류가 맞지 않습니다."; return false; }
            if (kind == LinkKind.Clash)
            {
                var intent = Intent(targetId);
                if (intent == null || intent.Resolved || !IntentActive(intent)) { error = "유효한 예고선을 선택해 주세요."; return false; }
                if (State.Links.Any(x => x.Kind == LinkKind.Clash && x.TargetId == targetId && !(x.ActorId == actorId && (skill.Slots == 2 || Occupies(x, slot)))))
                { error = "이 예고선에는 이미 맞불이 연결되어 있습니다."; return false; }
            }
            else if (kind == LinkKind.Strike)
            {
                var enemy = TargetUnit(targetId);
                if (enemy == null || !enemy.IsEnemy || !enemy.Alive || enemy.Parts.Any(x => x.Id == targetId && x.Destroyed))
                { error = "살아 있는 적이나 파괴되지 않은 부위를 선택해 주세요."; return false; }
            }
            else
            {
                var ally = Unit(targetId);
                if (ally == null || ally.IsEnemy || !ally.Alive) { error = "보호할 아군을 선택해 주세요."; return false; }
            }
            var replaced = State.Links.Where(x => x.ActorId == actorId && (skill.Slots == 2 || Occupies(x, slot))).ToList();
            SkillCard card = null;
            if (skill.Number > 0)
            {
                card = actor.SkillHand.FirstOrDefault(x => x.SkillId == skill.Id &&
                    (string.IsNullOrEmpty(cardId) || x.Id == cardId) && !State.Links.Any(link => link.CardId == x.Id && !replaced.Contains(link)));
                if (card == null) { error = "이번 손패의 사용 가능한 스킬 카드를 선택해 주세요."; return false; }
            }
            else if (!BattleRules.IsDefensive(skill) || !string.IsNullOrEmpty(cardId))
            { error = "방어 행동은 스킬 카드를 사용하지 않습니다."; return false; }
            int newCost = PlannedCost - replaced.Sum(x => Skill(x.ActorId, x.SkillId).Cost) + skill.Cost;
            if (newCost > State.Ember) { error = "불씨가 부족합니다. 연결을 바꾸거나 잔광을 선택해 주세요."; return false; }
            foreach (var link in replaced) State.Links.Remove(link);
            var linkedIntent = kind == LinkKind.Clash ? Intent(targetId) : null;
            State.Links.Add(new LinkCommand { ActorId = actorId, Slot = slot, SkillId = skillId, Kind = kind, TargetId = targetId, CardId = card == null ? null : card.Id,
                IntentActorId=linkedIntent == null ? null : linkedIntent.ActorId, IntentPartId=linkedIntent == null ? null : linkedIntent.PartId, Reason = "직접 연결" });
            return true;
        }

        public bool IsCardAvailable(UnitState actor, string cardId, int? replacingSlot = null)
        {
            if (actor == null || !actor.Alive || actor.IsEnemy) return false;
            var card = actor.SkillHand.FirstOrDefault(x => x.Id == cardId);
            var skill = card == null ? null : actor.Skills.FirstOrDefault(x => x.Id == card.SkillId);
            if (!BattleRules.IsSkillUnlocked(actor, skill) || skill.Slots > actor.SlotCount) return false;
            if (replacingSlot.HasValue && (replacingSlot.Value < 0 || replacingSlot.Value >= actor.SlotCount || skill.Slots > actor.SlotCount || skill.Slots == 2 && replacingSlot.Value != 0)) return false;
            return !State.Links.Any(link => link.CardId == cardId && !(replacingSlot.HasValue && link.ActorId == actor.Id && (skill.Slots == 2 || Occupies(link, replacingSlot.Value))));
        }
        public IEnumerable<SkillData> OfferedSkills(UnitState actor, int? replacingSlot = null)
        {
            if (actor == null || actor.IsEnemy || !actor.Alive) return Enumerable.Empty<SkillData>();
            return actor.Skills.Where(skill => BattleRules.IsSkillUnlocked(actor, skill) &&
                (BattleRules.IsDefensive(skill) && skill.Number == 0 || skill.Number > 0 && actor.SkillHand.Any(card => card.SkillId == skill.Id && IsCardAvailable(actor, card.Id, replacingSlot))));
        }

        private Random DeckRandom(UnitState actor)
        {
            Random value;
            if (!deckRandom.TryGetValue(actor.Id, out value))
            {
                // Stable FNV-1a name hash avoids runtime-dependent string.GetHashCode.
                uint hash = 2166136261u; foreach (char c in actor.Id) { hash ^= c; hash *= 16777619u; }
                value = new Random(unchecked((int)(hash ^ (uint)State.RandomSeed ^ 0x6d2b79f5u)));
                deckRandom.Add(actor.Id, value);
            }
            return value;
        }
        private void Shuffle(UnitState actor, List<string> cards)
        {
            var rng = DeckRandom(actor);
            for (int i = cards.Count - 1; i > 0; i--)
            { int j = rng.Next(i + 1); string value = cards[i]; cards[i] = cards[j]; cards[j] = value; }
        }
        private void InitializeDeck(UnitState actor)
        {
            if (actor.DeckInitialized) return;
            actor.SkillHand.Clear(); actor.DrawPile.Clear(); actor.DiscardPile.Clear(); actor.NextCardNumber = 0;
            foreach (var skill in actor.Skills.Where(x => x.Number >= 1 && x.Number <= 3 && BattleRules.IsSkillUnlocked(actor, x)).OrderBy(x => x.Number))
            {
                int count = skill.Number == 1 ? 3 : skill.Number == 2 ? 2 : 1;
                for (int i = 0; i < count; i++) actor.DrawPile.Add(skill.Id);
            }
            actor.DeckInitialized = true; Shuffle(actor, actor.DrawPile); FillHand(actor);
        }
        private void FillHand(UnitState actor)
        {
            while (actor.SkillHand.Count < 2)
            {
                if (actor.DrawPile.Count == 0)
                {
                    if (actor.DiscardPile.Count == 0) break;
                    actor.DrawPile.AddRange(actor.DiscardPile); actor.DiscardPile.Clear(); Shuffle(actor, actor.DrawPile);
                }
                string skillId = actor.DrawPile[0]; actor.DrawPile.RemoveAt(0);
                actor.SkillHand.Add(new SkillCard { Id = actor.Id + ":card:" + (++actor.NextCardNumber), SkillId = skillId });
            }
        }
        private void RefreshHand(UnitState actor)
        {
            // Unused offers expire at the round boundary. They join the discard pile
            // once, then the same weighted bag supplies two newly identified cards.
            actor.DiscardPile.AddRange(actor.SkillHand.Select(card => card.SkillId));
            actor.SkillHand.Clear();
            if (actor.Alive) FillHand(actor);
        }
        private void CommitHandCards()
        {
            // Resolve is the commitment point, including actions later cancelled by death.
            foreach (var link in State.Links.Where(x => !string.IsNullOrEmpty(x.CardId)))
            {
                var actor = Unit(link.ActorId); var card = actor.SkillHand.FirstOrDefault(x => x.Id == link.CardId);
                if (card == null) continue;
                actor.SkillHand.Remove(card); actor.DiscardPile.Add(card.SkillId);
            }
        }

        public void ClearLinks() { if (State.Phase == BattlePhase.Link) State.Links.Clear(); }
        public void RemoveLink(string actorId, int slot)
        { if (State.Phase == BattlePhase.Link) State.Links.RemoveAll(x => x.ActorId == actorId && Occupies(x, slot)); }
        private bool Occupies(LinkCommand link, int slot)
        { return link.Slot == slot || (Skill(link.ActorId, link.SkillId).Slots == 2 && slot < 2); }
        private bool SlotFree(UnitState unit, int slot) { return !State.Links.Any(x => x.ActorId == unit.Id && Occupies(x, slot)); }

        public void ReusePreviousLinks()
        {
            if (State.Phase != BattlePhase.Link) return;
            ClearLinks();
            foreach (var old in previousLinks)
            {
                string target = old.TargetId;
                if (old.Kind == LinkKind.Clash)
                {
                    string suffix = target.Substring(target.IndexOf(':') + 1);
                    var equivalent = State.Intents.FirstOrDefault(x => !x.Cancelled && !x.Resolved &&
                        (string.IsNullOrEmpty(old.IntentActorId) || x.ActorId == old.IntentActorId) &&
                        (string.IsNullOrEmpty(old.IntentPartId) || x.PartId == old.IntentPartId) &&
                        x.Id.EndsWith(":" + suffix, StringComparison.Ordinal));
                    if (equivalent == null) continue;
                    target = equivalent.Id;
                }
                string error; TryLink(old.ActorId, old.Slot, old.SkillId, old.Kind, target, out error);
            }
        }

        public bool TryLantern(string actorId, out string error)
        {
            error = null;
            var actor = Unit(actorId);
            if (State.Phase != BattlePhase.Link || State.Lantern < 100 || State.LanternQueued || actor == null || actor.IsEnemy || !actor.Alive)
            { error = "등불 게이지 100과 행동 가능한 잔영이 필요합니다."; return false; }
            State.LanternQueued = true; State.LanternActorId = actorId; return true;
        }

        public ClashPreview Preview(LinkCommand link, IgniteGrade grade = IgniteGrade.Good)
        {
            var preview = new ClashPreview(); var intent = Intent(link.TargetId);
            var actor = Unit(link.ActorId); var skill = Skill(link.ActorId, link.SkillId);
            if (!BattleRules.IsSkillUnlocked(actor, skill))
            { preview.Summary = "동기화 필요 · 3스킬 잠금"; preview.Grade = PreviewGrade.Disadvantage; return preview; }
            if (State.Phase == BattlePhase.Link && skill.Number > 0 && !actor.SkillHand.Any(card => card.SkillId == skill.Id && (string.IsNullOrEmpty(link.CardId) || card.Id == link.CardId) && IsCardAvailable(actor, card.Id, link.Slot)))
            { preview.Summary = "이번 손패에 없는 스킬 카드"; preview.Grade = PreviewGrade.Disadvantage; return preview; }
            if (intent == null || actor == null || skill == null)
            { preview.Summary = "공격·방어·지원 연결"; preview.Grade = PreviewGrade.Close; return preview; }
            var enemy = Unit(intent.ActorId);
            float multiplier = grade == IgniteGrade.Perfect ? 1.25f : grade == IgniteGrade.Miss ? .75f : 1f;
            preview.AllyPower = Power(Expected(BattleRules.Base(skill), BattleRules.Flame(skill), BattleRules.Flames(skill), actor.Sanity), skill.Element, enemy, multiplier, false);
            preview.EnemyPower = Power(Expected(BattleRules.Base(intent), BattleRules.Flame(intent), BattleRules.Flames(intent), enemy.Sanity), intent.Element, actor, 1, true);
            float lowestAlly = Power(BattleRules.Base(skill), skill.Element, enemy, multiplier, false);
            float highestEnemy = Power(BattleRules.Base(intent) + BattleRules.Flame(intent) * BattleRules.Flames(intent), intent.Element, actor, 1, true);
            int result = BattleRules.CompareHit(preview.AllyPower, 0, preview.EnemyPower, 0);
            preview.AllyHits = result > 0 ? 2 : 0; preview.EnemyHits = result < 0 ? 2 : 0; preview.Ties = result == 0 ? 1 : 0;
            preview.Grade = lowestAlly > highestEnemy || preview.AllyPower > preview.EnemyPower * 1.12f ? PreviewGrade.Advantage : preview.AllyPower >= preview.EnemyPower * .88f ? PreviewGrade.Close : PreviewGrade.Disadvantage;
            preview.Summary = string.Format("{0}  ·  기대 위력 {1:0.#} : {2:0.#}  ·  2연승 시 공격", preview.Grade == PreviewGrade.Advantage ? "우세" : preview.Grade == PreviewGrade.Close ? "접전" : "열세", preview.AllyPower, preview.EnemyPower);
            return preview;
        }
        private static float Expected(int basePower, int flamePower, int flames, int sanity)
        { return basePower + flamePower * flames * (float)BattleRules.FlameChance(sanity); }

        public void RecommendLinks()
        {
            if (State.Phase != BattlePhase.Link) return;
            ClearLinks();
            // Preserve low-health characters before spending the remaining budget on clashes.
            foreach (var healer in State.Allies.Where(x => x.Alive))
            {
                var heal = OfferedSkills(healer).FirstOrDefault(x => x.Kind == SkillKind.Heal && BattleRules.IsSkillUnlocked(healer, x) && x.Cost <= AvailableEmber);
                var wounded = State.Allies.Where(x => x.Alive && x.Hp < x.MaxHp * .4f).OrderBy(x => (float)x.Hp / x.MaxHp).FirstOrDefault();
                if (heal != null && wounded != null) AddRecommended(healer, 0, heal, LinkKind.Support, wounded.Id, "체력 40% 미만 아군을 우선 치유");
            }
            foreach (var intent in State.Intents.Where(x => !x.Cancelled && x.Kind != IntentKind.Illusion).OrderByDescending(x => x.HitPowers.Sum() * (x.Kind == IntentKind.Area ? State.Allies.Count(a => a.Alive) : 1)))
            {
                LinkCommand best = null; float bestScore = float.MinValue;
                foreach (var actor in State.Allies.Where(x => x.Alive && !x.IsSupport))
                for (int slot = 0; slot < actor.SlotCount; slot++)
                {
                    if (!SlotFree(actor, slot)) continue;
                    foreach (var skill in OfferedSkills(actor).Where(x => x.Kind == SkillKind.Attack && BattleRules.IsSkillUnlocked(actor, x) && x.Cost <= AvailableEmber))
                    {
                        if (skill.Slots == 2 && (slot != 0 || !SlotFree(actor, 1))) continue;
                        var candidate = new LinkCommand { ActorId = actor.Id, Slot = slot, SkillId = skill.Id, Kind = LinkKind.Clash, TargetId = intent.Id };
                        var preview = Preview(candidate);
                        if (preview.Grade == PreviewGrade.Disadvantage || preview.AllyHits == 0) continue;
                        float score = (preview.EnemyHits == 0 ? 100 : 0) + preview.AllyHits * 5 - preview.EnemyHits * 12 - skill.Cost * 4 - skill.Slots;
                        if (score > bestScore) { bestScore = score; best = candidate; }
                    }
                }
                if (best != null) AddRecommended(Unit(best.ActorId), best.Slot, Skill(best.ActorId, best.SkillId), LinkKind.Clash, best.TargetId, "위협이 큰 예고선부터 맞불로 상쇄");
                else if (intent.Kind == IntentKind.Normal)
                {
                    var guard = State.Allies.Where(x => x.Alive && x.Id != intent.TargetId).FirstOrDefault(x => Enumerable.Range(0, x.SlotCount).Any(s => SlotFree(x, s)) && x.Skills.Any(s => BattleRules.IsDefensive(s) && BattleRules.IsSkillUnlocked(x, s) && s.Cost <= AvailableEmber));
                    if (guard != null)
                    {
                        var skill = guard.Skills.First(x => BattleRules.IsDefensive(x) && BattleRules.IsSkillUnlocked(guard, x) && x.Cost <= AvailableEmber);
                        AddRecommended(guard, Enumerable.Range(0, guard.SlotCount).First(s => SlotFree(guard, s)), skill, LinkKind.Guard, intent.TargetId, "맞불 열세: 호위로 피해를 나누어 받기");
                    }
                }
            }
            foreach (var actor in State.Allies.Where(x => x.Alive))
            for (int slot = 0; slot < actor.SlotCount; slot++)
            {
                if (!SlotFree(actor, slot)) continue;
                var wounded = State.Allies.Where(x => x.Alive && x.Hp < x.MaxHp).OrderBy(x => (float)x.Hp / x.MaxHp).FirstOrDefault();
                var heal = OfferedSkills(actor).FirstOrDefault(x => x.Kind == SkillKind.Heal && BattleRules.IsSkillUnlocked(actor, x) && x.Cost <= AvailableEmber);
                if (heal != null && wounded != null) { AddRecommended(actor, slot, heal, LinkKind.Support, wounded.Id, "남은 불씨로 부상 회복"); continue; }
                if (actor.IsSupport)
                {
                    var g = OfferedSkills(actor).FirstOrDefault(x => BattleRules.IsDefensive(x) && BattleRules.IsSkillUnlocked(actor, x) && x.Cost <= AvailableEmber);
                    if (g != null) AddRecommended(actor, slot, g, LinkKind.Guard, State.Allies.First(x => x.Alive).Id, "서포트의 보호 연결");
                    continue;
                }
                var enemy = State.Enemies.Where(x => x.Alive).OrderBy(x => x.Hp).FirstOrDefault(); if (enemy == null) continue;
                var attack = OfferedSkills(actor).Where(x => x.Kind == SkillKind.Attack && BattleRules.IsSkillUnlocked(actor, x) && x.Slots == 1 && x.Cost <= AvailableEmber).OrderByDescending(x => Expected(BattleRules.Base(x), BattleRules.Flame(x), BattleRules.Flames(x), actor.Sanity)).FirstOrDefault();
                if (attack == null) continue;
                var part = enemy.Parts.Where(x => !x.Destroyed).OrderBy(x => x.Hp).FirstOrDefault();
                AddRecommended(actor, slot, attack, LinkKind.Strike, part == null ? enemy.Id : part.Id, part == null ? "남은 슬롯으로 균열 압박" : "부위를 파괴해 다음 예고선 차단");
            }
            if (State.Lantern >= 100 && !State.LanternQueued)
            { string error; TryLantern(State.Allies.First(x => x.Alive).Id, out error); }
        }

        private void AddRecommended(UnitState actor, int slot, SkillData skill, LinkKind kind, string target, string reason)
        {
            string error;
            if (TryLink(actor.Id, slot, skill.Id, kind, target, out error)) State.Links.Last().Reason = reason;
        }

        private sealed class Scheduled
        { public int Speed, Order; public LinkCommand Link; public IntentState Intent; }

        public List<BattleEvent> ResolveRound(IgniteGrade grade = IgniteGrade.Good)
        { return ResolveRound(link => grade); }

        public List<BattleEvent> ResolveRound(Dictionary<string, IgniteGrade> grades)
        {
            return ResolveRound(link => {
                IgniteGrade grade;
                return grades != null && grades.TryGetValue(link.ActorId + ":" + link.Slot, out grade) ? grade : IgniteGrade.Good;
            });
        }

        public List<BattleEvent> ResolveRound(Func<LinkCommand, IgniteGrade> ignition)
        {
            if (State.Phase != BattlePhase.Link || State.Outcome != BattleOutcome.Ongoing) return State.Events;
            State.Events.Clear(); State.Phase = BattlePhase.Resolve;
            State.Ember -= PlannedCost;
            previousLinks.Clear(); previousLinks.AddRange(State.Links.Select(x => x.Copy()));
            CommitHandCards();
            guards.Clear(); elementsHit.Clear(); reactions.Clear(); shattered.Clear(); clashEmber = 0; siphonEmber = 0;
            if (State.LanternQueued)
            {
                State.Lantern -= 100; State.Gloom = Math.Max(0, State.Gloom - 1);
                Log("Lantern", State.LanternActorId, "", "등불 해방 — 어둠도 감소, 점화 판정 창 +20%", 100);
            }
            var queue = new List<Scheduled>(); int order = 0;
            foreach (var link in State.Links)
            {
                int speed = EffectiveSpeed(Unit(link.ActorId), Skill(link.ActorId, link.SkillId).Speed);
                var intent = link.Kind == LinkKind.Clash ? Intent(link.TargetId) : null;
                if (intent != null) speed = Math.Max(speed, EffectiveSpeed(Unit(intent.ActorId), intent.Speed));
                queue.Add(new Scheduled { Link = link, Speed = speed, Order = order++ });
            }
            foreach (var intent in State.Intents) queue.Add(new Scheduled { Intent = intent, Speed = EffectiveSpeed(Unit(intent.ActorId), intent.Speed), Order = order++ });
            while (queue.Count > 0)
            {
                // Re-evaluate after each action so a newly applied Freeze changes later actions.
                var action = queue.OrderByDescending(CurrentSpeed).ThenBy(x => x.Order).First();
                queue.Remove(action);
                if (action.Link != null)
                {
                    var actor = Unit(action.Link.ActorId);
                    if (!actor.Alive) { Log("Cancel", actor.Id, "", actor.Name + "의 행동 취소 — 전투불능", 0); continue; }
                    ResolveLink(action.Link, ignition == null ? IgniteGrade.Good : ignition(action.Link));
                }
                else ResolveIntent(action.Intent);
                CheckOutcome(); if (State.Outcome != BattleOutcome.Ongoing) break;
            }
            State.LanternQueued = false;
            if (State.Outcome == BattleOutcome.Ongoing)
            {
                State.Phase = BattlePhase.WrapUp;
                int recovery = State.Gloom >= 3 ? 2 : 3;
                State.Ember = Math.Min(BattleRules.MaxEmber, State.Ember + recovery);
                Log("WrapUp", "", "", "라운드 정리 — 불씨 +" + recovery, recovery);
            }
            return State.Events;
        }

        private int CurrentSpeed(Scheduled action)
        {
            if (action.Link == null) return EffectiveSpeed(Unit(action.Intent.ActorId), action.Intent.Speed);
            var link = action.Link;
            int speed = EffectiveSpeed(Unit(link.ActorId), Skill(link.ActorId, link.SkillId).Speed);
            var intent = link.Kind == LinkKind.Clash ? Intent(link.TargetId) : null;
            return intent != null && !intent.Resolved && !intent.Cancelled ? Math.Max(speed, EffectiveSpeed(Unit(intent.ActorId), intent.Speed)) : speed;
        }

        public void BeginNextRound()
        {
            if (State.Phase != BattlePhase.WrapUp || State.Outcome != BattleOutcome.Ongoing) return;
            State.Round++; State.Links.Clear(); State.Events.Clear();
            foreach (var actor in State.Allies) RefreshHand(actor);
            foreach (var enemy in State.Enemies.Where(x => x.Alive && x.Rift == 0 && x.BrokenThroughRound < State.Round)) enemy.Rift = enemy.MaxRift;
            Observe();
        }

        public void Restart()
        {
            if (factory == null) throw new InvalidOperationException("Restart requires a battle factory.");
            State = factory(); if (ownsRandom) random = new SeededBattleRandom(State.RandomSeed); deckRandom.Clear(); foreach (var actor in State.Allies) InitializeDeck(actor); previousLinks.Clear(); guards.Clear(); elementsHit.Clear(); reactions.Clear(); shattered.Clear(); Observe();
        }

        private void Observe()
        {
            BattleRules.ApplyActionProgression(State);
            State.Intents.Clear(); State.Phase = BattlePhase.Observe;
            // Rotate over living allies so every enemy forecast names its actual recipient.
            // Health ordering keeps a wounded ally first while stable roster order breaks ties.
            var targets = State.Allies.Where(x => x.Alive).OrderBy(x => (float)x.Hp / x.MaxHp).ToList();
            foreach (var enemy in State.Enemies.Where(x => x.Alive))
            {
                if (enemy.BrokenThroughRound >= State.Round) { Log("Skip", enemy.Id, "", enemy.Name + " — 균열로 이번 라운드 행동 불가", 0); continue; }
                if (targets.Count == 0) continue;
                var target = targets[(State.Round - 1 + State.Enemies.IndexOf(enemy)) % targets.Count];
                string intentPrefix = State.Round + ":" + enemy.Id + "_";
                var arm = enemy.Parts.FirstOrDefault(x => x.Id.EndsWith("arm", StringComparison.Ordinal));
                var core = enemy.Parts.FirstOrDefault(x => x.Id.EndsWith("core", StringComparison.Ordinal));
                int anger = Math.Min(5, State.Round - 1);
                if(!string.IsNullOrEmpty(enemy.IntentProfile)){
                    string profile=enemy.IntentProfile;bool solo=State.Allies.Count==1;
                    if(arm==null||!arm.Destroyed)State.Intents.Add(new IntentState { Id=intentPrefix+"arm",ActorId=enemy.Id,TargetId=target.Id,PartId=arm==null?null:arm.Id,
                        Name=enemy.IsHuman?"진입 저지":profile=="tutorial"?"첫 습격":"연속 공격",HitPowers=profile=="tutorial"?new[]{5}:new[]{7+anger,7+anger},Speed=10,Element=enemy.Element,
                        Kind=profile=="piercing"?IntentKind.Piercing:IntentKind.Normal });
                    if(!solo&&profile!="tutorial"&&profile!="skirmish"&&(core==null||!core.Destroyed))State.Intents.Add(new IntentState { Id=intentPrefix+"core",ActorId=enemy.Id,
                        TargetId=profile=="command"?target.Id:"all",PartId=core==null?null:core.Id,Name=profile=="illusion"?"기억의 잔향 · 허상":enemy.IsHuman?"강제 집행":"번지는 어둠",
                        HitPowers=profile=="command"?new[]{6+anger,6+anger}:new[]{4+anger,5+anger,4+anger},Speed=8,Element=enemy.Element,
                        Kind=profile=="illusion"?IntentKind.Illusion:profile=="command"?IntentKind.Piercing:IntentKind.Area });
                    if(State.Intents.All(x=>x.ActorId!=enemy.Id))State.Intents.Add(new IntentState { Id=intentPrefix+"body",ActorId=enemy.Id,TargetId=target.Id,
                        Name=enemy.IsHuman?"남은 저항":"남은 어둠",HitPowers=new[]{6+anger},Speed=8,Element=enemy.Element });
                    continue;
                }
                if (arm == null || !arm.Destroyed) State.Intents.Add(new IntentState { Id = intentPrefix + "arm", ActorId = enemy.Id, TargetId = target.Id, PartId = arm == null ? null : arm.Id, Name = "난타", HitPowers = new[] { 10 + anger, 10 + anger }, Speed = 11, Element = Element.Shadow, Kind = IntentKind.Normal });
                if (core == null || !core.Destroyed) State.Intents.Add(new IntentState { Id = intentPrefix + "core", ActorId = enemy.Id, TargetId = "all", PartId = core == null ? null : core.Id, Name = "절규", HitPowers = new[] { 6 + anger, 6 + anger, 6 + anger }, Speed = 9, Element = Element.Shadow, Kind = IntentKind.Area });
                if (State.Intents.All(x => x.ActorId != enemy.Id)) State.Intents.Add(new IntentState { Id = intentPrefix + "body", ActorId = enemy.Id, TargetId = target.Id, Name = "남은 분노", HitPowers = new[] { 8 + anger }, Speed = 8, Element = Element.Shadow });
            }
            foreach (var intent in State.Intents)
            {
                intent.BasePower = BattleRules.Base(intent); intent.FlamePower = BattleRules.Flame(intent); intent.FlameCount = BattleRules.Flames(intent);
            }
            State.Phase = BattlePhase.Link;
            Log("Observe", "", "", "관측 — 예고선을 읽고 빛실을 연결하세요.", State.Intents.Count);
        }

        private void ResolveLink(LinkCommand link, IgniteGrade grade)
        {
            var actor = Unit(link.ActorId); var skill = Skill(link.ActorId, link.SkillId);
            if (!BattleRules.IsSkillUnlocked(actor, skill)) { Log("Cancel", actor.Id, "", "동기화되지 않은 스킬 — 행동 취소", 0); return; }
            float multiplier = 1;
            if (grade == IgniteGrade.Perfect)
            { multiplier = 1.25f + Math.Min(.25f, State.IgniteChain * .05f); State.IgniteChain++; AddLantern(3); }
            else if (grade == IgniteGrade.Miss) { multiplier = .75f; State.IgniteChain = 0; }
            Log("Ignite", actor.Id, link.TargetId, actor.Name + " · " + skill.Name + " — " + (grade == IgniteGrade.Perfect ? "점화" : grade == IgniteGrade.Good ? "양호" : "빗나감"), (int)Math.Round(multiplier * 100));
            if (link.Kind == LinkKind.Support)
            {
                var ally = Unit(link.TargetId); if (ally == null || !ally.Alive) return;
                var roll = RollSkill(actor, skill, null, multiplier); LogRoll(actor, ally, roll, null, 0, skill);
                int heal = Math.Min(ally.MaxHp - ally.Hp, (int)Math.Round((skill.Heal + roll.Raw - BattleRules.Base(skill)) * multiplier)); ally.Hp += heal;
                Log("Heal", actor.Id, ally.Id, actor.Name + " → " + ally.Name + " 치유 +" + heal, heal); return;
            }
            if (link.Kind == LinkKind.Guard)
            {
                var roll = RollSkill(actor, skill, null, multiplier); LogRoll(actor, Unit(link.TargetId), roll, null, 0, skill);
                guards[link.TargetId] = new DefenseAction { Actor = actor, Skill = skill, Roll = roll };
                Log("Guard", actor.Id, link.TargetId, actor.Name + " · " + skill.Name + " — 방어 위력 " + roll.Power.ToString("0.#"), (int)Math.Round(roll.Power)); return;
            }
            if (link.Kind == LinkKind.Clash)
            {
                var intent = Intent(link.TargetId);
                if (intent != null && !intent.Resolved && IntentActive(intent)) { ResolveClash(actor, skill, intent, multiplier); return; }
                var enemy = intent == null ? null : Unit(intent.ActorId);
                Log("Relink", actor.Id, enemy == null ? "" : enemy.Id, "사라진 예고선 → 적 본체 공격으로 전환", 0);
                if (enemy != null && enemy.Alive) Strike(actor, skill, enemy, null, multiplier); return;
            }
            var target = TargetUnit(link.TargetId); if (target == null || !target.Alive) return;
            Strike(actor, skill, target, target.Parts.FirstOrDefault(x => x.Id == link.TargetId && !x.Destroyed), multiplier);
        }

        private RolledPower Roll(int basePower, int flamePower, int flames, int sanity)
        {
            var roll = new RolledPower { Raw = basePower, Flames = new bool[flames] };
            for (int i = 0; i < flames; i++)
            { roll.Flames[i] = random.NextDouble() < BattleRules.FlameChance(sanity); if (roll.Flames[i]) roll.Raw += flamePower; }
            roll.Power = roll.Raw; return roll;
        }
        private RolledPower RollSkill(UnitState actor, SkillData skill, UnitState target, float multiplier)
        {
            var roll = Roll(BattleRules.Base(skill), BattleRules.Flame(skill), BattleRules.Flames(skill), actor.Sanity);
            roll.Power = target == null ? roll.Raw * multiplier : Power(roll.Raw, skill.Element, target, multiplier, false); return roll;
        }
        private RolledPower RollIntent(UnitState enemy, IntentState intent, UnitState target)
        {
            var roll = Roll(BattleRules.Base(intent), BattleRules.Flame(intent), BattleRules.Flames(intent), enemy.Sanity);
            roll.Power = target == null ? roll.Raw : Power(roll.Raw, intent.Element, target, 1, true); return roll;
        }
        private BattleEvent LogRoll(UnitState actor, UnitState target, RolledPower own, RolledPower other, int index, SkillData skill = null, IntentState intent = null, IntentState enemyIntent = null)
        {
            var e = new BattleEvent("FlameRoll", actor.Id, target == null ? "" : target.Id,
                actor.Name + (actor.IsEnemy ? " · 어둠" : " · 불꽃") + " " + own.Flames.Count(x => x) + "/" + own.Flames.Length + " · 위력 " + own.Power.ToString("0.#"), (int)Math.Round(own.Power), State.Round);
            e.FlameStates = own.Flames; e.EnemyFlameStates = other == null ? new bool[0] : other.Flames;
            e.ActorPower = own.Power; e.TargetPower = other == null ? 0 : other.Power;
            e.ActorSanity = actor.Sanity; e.TargetSanity = target == null ? 0 : target.Sanity; e.ClashIndex = index;
            if (skill != null) SetSkillMetadata(e, skill);
            else if (intent != null) SetIntentMetadata(e, intent);
            if (enemyIntent != null) { e.EnemyBasePower = BattleRules.Base(enemyIntent); e.EnemyFlamePower = BattleRules.Flame(enemyIntent); }
            if (enemyIntent != null) SetIntentContext(e, enemyIntent);
            State.Events.Add(e); return e;
        }
        private static void SetSkillMetadata(BattleEvent e, SkillData skill)
        { e.SkillId = skill.Id; e.SkillName = skill.Name; e.SkillNumber = skill.Number; e.BasePower = BattleRules.Base(skill); e.FlamePower = BattleRules.Flame(skill); }
        private static void SetIntentMetadata(BattleEvent e, IntentState intent)
        { e.SkillId = intent.Id; e.SkillName = intent.Name; e.SkillNumber = 0; e.BasePower = BattleRules.Base(intent); e.FlamePower = BattleRules.Flame(intent); SetIntentContext(e,intent); }
        private static void SetIntentContext(BattleEvent e, IntentState intent)
        { e.IntentId = intent.Id; e.OriginalTargetId = intent.TargetId; }
        private void Strike(UnitState actor, SkillData skill, UnitState enemy, PartState part, float multiplier)
        {
            var roll = RollSkill(actor, skill, enemy, multiplier); LogRoll(actor, enemy, roll, null, 0, skill);
            ApplyStrike(actor, skill, enemy, part, roll);
        }
        private void ApplyStrike(UnitState actor, SkillData skill, UnitState enemy, PartState part, RolledPower roll, IntentState clashIntent = null)
        {
            if (!actor.Alive || !enemy.Alive) return;
            // The whole skill deals its final rolled total once; lit flames never multiply hit count.
            Hurt(actor, enemy, Damage(actor, enemy, roll.Power, 1), skill, roll, clashIntent); RiftHit(enemy, skill.RiftDamage, part, skill.PartDamage); React(skill.Element, enemy);
        }

        private void ResolveClash(UnitState actor, SkillData skill, IntentState intent, float multiplier)
        {
            var enemy = Unit(intent.ActorId);
            if (intent.Kind == IntentKind.Illusion)
            { intent.Resolved = true; Log("Illusion", actor.Id, enemy.Id, "허상선 — 빛실이 허공을 가릅니다.", 0); return; }
            if (intent.TargetId != actor.Id)
            {
                var protectedAlly = Unit(intent.TargetId);
                var intercept = new BattleEvent("Intercept", actor.Id, enemy.Id,
                    intent.Kind == IntentKind.Area ? actor.Name + " — 전체 공격에 맞불" : actor.Name + " → " + (protectedAlly == null ? "동료" : protectedAlly.Name) + " 대신 맞불", 0, State.Round);
                SetSkillMetadata(intercept, skill); SetIntentContext(intercept, intent); State.Events.Add(intercept);
            }
            int streakOwner = 0, streak = 0;
            for (int i = 1; i <= BattleRules.MaxClashContests; i++)
            {
                if (!actor.Alive || !IntentActive(intent)) break;
                var own = RollSkill(actor, skill, enemy, multiplier); var other = RollIntent(enemy, intent, actor);
                LogRoll(actor, enemy, own, other, i, skill, null, intent);
                int result = BattleRules.CompareHit(own.Power, 0, other.Power, 0);
                streak = result == 0 ? 0 : result == streakOwner ? streak + 1 : 1; streakOwner = result;
                var clash = new BattleEvent(result == 0 ? "Tie" : "Clash", actor.Id, enemy.Id,
                    result == 0 ? "챙! 위력 동률 — 연승 초기화" : "챙! " + (result > 0 ? actor.Name : enemy.Name) + " " + streak + "/2 연승", result, State.Round)
                { ActorPower = own.Power, TargetPower = other.Power, FlameStates = own.Flames, EnemyFlameStates = other.Flames,
                    ActorSanity = actor.Sanity, TargetSanity = enemy.Sanity, ClashIndex = i, WinnerStreak = streak };
                SetSkillMetadata(clash, skill); SetIntentContext(clash,intent); clash.EnemyBasePower = BattleRules.Base(intent); clash.EnemyFlamePower = BattleRules.Flame(intent);
                State.Events.Add(clash);
                if (streak < 2) continue;
                intent.Resolved = true;
                var winner = result > 0 ? actor : enemy; var loser = result > 0 ? enemy : actor;
                var win = new BattleEvent("ClashWin", winner.Id, loser.Id, winner.Name + " 맞불 승리 — 2연승 후 공격", 2, State.Round)
                { ActorPower = result > 0 ? own.Power : other.Power, TargetPower = result > 0 ? other.Power : own.Power,
                    FlameStates = result > 0 ? own.Flames : other.Flames, EnemyFlameStates = result > 0 ? other.Flames : own.Flames,
                    ActorSanity = winner.Sanity, TargetSanity = loser.Sanity, ClashIndex = i, WinnerStreak = 2 };
                if (result > 0) { SetSkillMetadata(win, skill); win.EnemyBasePower = BattleRules.Base(intent); win.EnemyFlamePower = BattleRules.Flame(intent); }
                else { SetIntentMetadata(win, intent); win.EnemyBasePower = BattleRules.Base(skill); win.EnemyFlamePower = BattleRules.Flame(skill); }
                SetIntentContext(win,intent);
                State.Events.Add(win); ChangeSanity(winner, 2); ChangeSanity(loser, -2);
                if (result > 0)
                {
                    AddLantern(10); if (clashEmber < 2) { clashEmber++; State.Ember = Math.Min(6, State.Ember + 1); }
                    ApplyStrike(actor, skill, enemy, null, own, intent);
                }
                else EnemyHit(enemy, intent, other, actor);
                return;
            }
            intent.Resolved = true; Log("Stalemate", actor.Id, enemy.Id, "맞불 교착 — 이번 행동을 마치고 피해 없이 물러납니다.", 0);
        }

        private void ResolveIntent(IntentState intent)
        {
            if (intent.Resolved || !IntentActive(intent)) return;
            var enemy = Unit(intent.ActorId);
            if (intent.Kind != IntentKind.Illusion)
            {
                var target = intent.TargetId == "all" ? State.Allies.FirstOrDefault(x => x.Alive) : Unit(intent.TargetId);
                if (target == null || !target.Alive)
                {
                    intent.Resolved = true;
                    var cancelled = new BattleEvent("Cancel", enemy.Id, intent.Id, intent.Name + " 취소 — 예고한 대상이 전투불능", 0, State.Round);
                    SetIntentContext(cancelled,intent); State.Events.Add(cancelled); return;
                }
                var roll = RollIntent(enemy, intent, target); LogRoll(enemy, target, roll, null, 0, null, intent); EnemyHit(enemy, intent, roll, null);
            }
            intent.Resolved = true;
        }

        private void EnemyHit(UnitState enemy, IntentState intent, RolledPower roll, UnitState clashActor)
        {
            var targets = intent.Kind == IntentKind.Area ? State.Allies.Where(x => x.Alive).ToList() : new List<UnitState> { clashActor ?? Unit(intent.TargetId) };
            foreach (var original in targets)
            {
                if (original == null || !original.Alive) continue;
                var target = original; float reduction = 1; DefenseAction defense = null;
                if (clashActor == null && intent.Kind == IntentKind.Normal && guards.TryGetValue(original.Id, out defense) && defense.Actor.Alive)
                {
                    target = defense.Actor;
                    float incoming = Power(roll.Raw, intent.Element, target, 1, true);
                    if (defense.Skill.Kind == SkillKind.Evade && !defense.Used)
                    {
                        defense.Used = true;
                        if (defense.Roll.Power >= incoming) { Log("Evade", target.Id, enemy.Id, target.Name + " 회피 — 피해 없음", 0); continue; }
                        Log("EvadeFail", target.Id, enemy.Id, target.Name + " 회피 실패", 0);
                    }
                    else if (defense.Skill.Kind == SkillKind.Guard) reduction = Math.Max(.15f, 1 - defense.Roll.Power / 35f);
                    else if (defense.Skill.Kind == SkillKind.Counter) reduction = .85f;
                }
                else defense = null;
                float attackPower = clashActor != null && original == clashActor ? roll.Power : Power(roll.Raw, intent.Element, target, 1, true);
                Hurt(enemy, target, Damage(enemy, target, attackPower, reduction), null, new RolledPower { Raw = roll.Raw, Power = attackPower, Flames = roll.Flames }, intent);
                if (defense != null && defense.Skill.Kind == SkillKind.Counter && !defense.Used && target.Alive && enemy.Alive)
                {
                    defense.Used = true; Log("Counter", target.Id, enemy.Id, target.Name + " 반격", 0);
                    float counterPower = Power(defense.Roll.Raw, defense.Skill.Element, enemy, 1, false);
                    Hurt(target, enemy, Damage(target, enemy, counterPower, 1), defense.Skill, new RolledPower { Raw = defense.Roll.Raw, Power = counterPower, Flames = defense.Roll.Flames });
                    RiftHit(enemy, defense.Skill.RiftDamage, null, 0); React(defense.Skill.Element, enemy);
                }
            }
        }
        private void ChangeSanity(UnitState unit, int change)
        {
            int previous = unit.Sanity; unit.Sanity = BattleRules.ClampSanity(previous + change);
            var e = new BattleEvent("Sanity", unit.Id, "", unit.Name + " 정신력 " + (unit.Sanity >= 0 ? "+" : "") + unit.Sanity, unit.Sanity - previous, State.Round)
            { ActorSanity = unit.Sanity }; State.Events.Add(e);
        }

        private bool IntentActive(IntentState intent)
        {
            var enemy = Unit(intent.ActorId);
            if (intent.Cancelled || enemy == null || !enemy.Alive || enemy.BrokenThroughRound >= State.Round || enemy.Parts.Any(x => x.Id == intent.PartId && x.Destroyed))
            { intent.Cancelled = true; return false; }
            return true;
        }
        private int EffectiveSpeed(UnitState unit, int speed)
        { return unit.FrozenThroughRound >= State.Round ? (int)Math.Floor(speed * .7f) : speed; }
        private float Power(float raw, Element element, UnitState target, float ignite, bool enemy)
        { return raw * ignite * BattleRules.Affinity(element, target.Element) * (enemy && element == Element.Shadow && State.Gloom >= 2 ? 1 + (State.Gloom - 1) * .1f : 1); }
        private int Damage(UnitState actor, UnitState target, float power, float reduction)
        { return Math.Max(1, (int)Math.Round(Math.Max(1, actor.Attack * power / 10 - target.Defense) * reduction * (target.IsEnemy && target.BrokenThroughRound >= State.Round ? 1.3f : 1))); }
        private void Hurt(UnitState actor, UnitState target, int damage, SkillData skill = null, RolledPower roll = null, IntentState intent = null)
        {
            target.Hp = Math.Max(0, target.Hp - damage);
            if (!target.IsEnemy) AddLantern(2);
            var e = new BattleEvent("Damage", actor.Id, target.Id, actor.Name + " → " + target.Name + "  " + damage, damage, State.Round)
            { ActorSanity = actor.Sanity, TargetSanity = target.Sanity };
            if (skill != null) SetSkillMetadata(e, skill); else if (intent != null) SetIntentMetadata(e, intent);
            if (intent != null) SetIntentContext(e,intent);
            if (roll != null) { e.FlameStates = roll.Flames; e.ActorPower = roll.Power; }
            State.Events.Add(e);
            if (target.Hp == 0)
            { Log("DefeatUnit", actor.Id, target.Id, target.Name + " 전투불능", 0); if (!target.IsEnemy)
                {
                    State.Gloom = Math.Min(3, State.Gloom + 1);
                    foreach (var teammate in State.Allies.Where(x => x.Alive && x.Id != target.Id)) ChangeSanity(teammate, -3);
                }
                CancelInvalidIntents(); }
        }
        private void RiftHit(UnitState enemy, int riftDamage, PartState part, int partDamage)
        {
            if (!enemy.Alive) return;
            if (part != null && !part.Destroyed)
            {
                part.Hp = Math.Max(0, part.Hp - partDamage);
                if (part.Destroyed) { Log("PartBreak", "", part.Id, part.Name + " 파괴 — 연결된 예고선 소멸", 0); CancelInvalidIntents(); }
            }
            if (enemy.Rift <= 0) return;
            int amount = (int)Math.Round(riftDamage * (shattered.Contains(enemy.Id) ? 1.5f : 1)) + (part == null ? 0 : 2);
            enemy.Rift = Math.Max(0, enemy.Rift - amount);
            if (enemy.Rift == 0)
            {
                enemy.BrokenThroughRound = State.Round + 1; AddLantern(15);
                Log("Rift", "", enemy.Id, "균열 — 남은 행동 취소 / 다음 라운드 행동 불가 / 받는 피해 +30%", 15); CancelInvalidIntents();
            }
        }
        private void CancelInvalidIntents()
        {
            foreach (var intent in State.Intents.Where(x => !x.Cancelled && !x.Resolved))
            { if (!IntentActive(intent)) Log("Cancel", intent.ActorId, intent.Id, intent.Name + " 예고선 소멸", 0); }
        }
        private void React(Element element, UnitState enemy)
        {
            HashSet<Element> seen;
            if (!elementsHit.TryGetValue(enemy.Id, out seen)) { seen = new HashSet<Element>(); elementsHit[enemy.Id] = seen; }
            foreach (Element old in seen.Where(x => x != element).OrderBy(x => (int)x))
            {
                int low = Math.Min((int)old, (int)element), high = Math.Max((int)old, (int)element);
                if (!reactions.Add(enemy.Id + ":" + low + ":" + high)) continue;
                string name = "";
                if (old == Element.Radiance || element == Element.Radiance) { name = "정화"; State.Gloom = Math.Max(0, State.Gloom - 1); }
                else if (old == Element.Shadow || element == Element.Shadow) { name = "잠식"; if (siphonEmber++ == 0) State.Ember = Math.Min(6, State.Ember + 1); }
                else if ((old == Element.Flame && element == Element.Frost) || (old == Element.Frost && element == Element.Flame)) { name = "파쇄"; shattered.Add(enemy.Id); }
                else if ((old == Element.Flame && element == Element.Tide) || (old == Element.Tide && element == Element.Flame))
                {
                    name = "증기"; var line = State.Intents.FirstOrDefault(x => x.ActorId == enemy.Id && !x.Cancelled && !x.Resolved && x.HitPowers.Length > 0);
                    if (line != null)
                    {
                        // Every usable action keeps at least one flame.
                        line.FlameCount = Math.Max(1, BattleRules.Flames(line) - 1);
                        line.HitPowers = line.HitPowers.Take(Math.Max(1, line.HitPowers.Length - 1)).ToArray();
                    }
                }
                else { name = "결빙"; enemy.FrozenThroughRound = State.Round + 1; }
                Log("Resonance", "", enemy.Id, "공명 · " + name + " — 이후 행동부터 적용", 0);
            }
            seen.Add(element);
        }
        private void AddLantern(int amount) { State.Lantern = Math.Min(100, State.Lantern + amount); }
        private void CheckOutcome()
        {
            if (State.Enemies.All(x => !x.Alive)) State.Outcome = BattleOutcome.Victory;
            else if (State.Allies.Where(x => !x.IsSupport).All(x => !x.Alive)) State.Outcome = BattleOutcome.Defeat;
            if (State.Outcome != BattleOutcome.Ongoing)
            { State.Phase = BattlePhase.Finished; Log("BattleEnd", "", "", State.Outcome == BattleOutcome.Victory ? "승리 — 마지막 빛을 지켜냈습니다." : "패배 — 빛실을 다시 이을 수 있습니다.", 0); }
        }
        private void Log(string type, string actor, string target, string message, int amount)
        { State.Events.Add(new BattleEvent(type, actor, target, message, amount, State.Round)); }
    }
}
