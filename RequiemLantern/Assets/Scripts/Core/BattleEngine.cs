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
        private readonly Dictionary<string, string> guards = new Dictionary<string, string>();
        private readonly Dictionary<string, HashSet<Element>> elementsHit = new Dictionary<string, HashSet<Element>>();
        private readonly HashSet<string> reactions = new HashSet<string>();
        private readonly HashSet<string> shattered = new HashSet<string>();
        private int clashEmber, siphonEmber;

        public BattleEngine(BattleState state, Func<BattleState> restartFactory = null)
        { State = state; factory = restartFactory; }

        public static BattleEngine CreatePrototype()
        {
            var engine = new BattleEngine(PrototypeData.CreateBattle(), PrototypeData.CreateBattle);
            engine.Observe();
            return engine;
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

        public bool TryLink(string actorId, int slot, string skillId, LinkKind kind, string targetId, out string error)
        {
            error = null;
            if (State.Phase != BattlePhase.Link || State.Outcome != BattleOutcome.Ongoing) { error = "연결 단계에서만 변경할 수 있습니다."; return false; }
            UnitState actor = Unit(actorId); SkillData skill = Skill(actorId, skillId);
            if (actor == null || actor.IsEnemy || !actor.Alive || skill == null) { error = "행동 가능한 잔영과 스킬을 선택해 주세요."; return false; }
            if (slot < 0 || slot >= actor.SlotCount || (skill.Slots == 2 && slot != 0) || skill.Slots > actor.SlotCount)
            { error = "이 스킬은 빈 슬롯 두 칸이 필요합니다."; return false; }
            if (actor.IsSupport && kind != LinkKind.Guard && kind != LinkKind.Support) { error = "서포트는 호위·지원 연결만 가능합니다."; return false; }
            if ((skill.Kind == SkillKind.Attack && kind != LinkKind.Strike && kind != LinkKind.Clash) ||
                (skill.Kind == SkillKind.Heal && kind != LinkKind.Support) || (skill.Kind == SkillKind.Guard && kind != LinkKind.Guard))
            { error = "스킬과 연결 종류가 맞지 않습니다."; return false; }
            if (kind == LinkKind.Clash)
            {
                var intent = Intent(targetId);
                if (intent == null || intent.Cancelled || intent.Resolved) { error = "유효한 예고선을 선택해 주세요."; return false; }
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
            int newCost = PlannedCost - replaced.Sum(x => Skill(x.ActorId, x.SkillId).Cost) + skill.Cost;
            if (newCost > State.Ember) { error = "불씨가 부족합니다. 연결을 바꾸거나 잔광을 선택해 주세요."; return false; }
            foreach (var link in replaced) State.Links.Remove(link);
            State.Links.Add(new LinkCommand { ActorId = actorId, Slot = slot, SkillId = skillId, Kind = kind, TargetId = targetId, Reason = "직접 연결" });
            return true;
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
                    var equivalent = State.Intents.FirstOrDefault(x => x.Id.EndsWith(":" + suffix, StringComparison.Ordinal));
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
            var p = new ClashPreview(); var intent = Intent(link.TargetId);
            var actor = Unit(link.ActorId); var skill = Skill(link.ActorId, link.SkillId);
            if (intent == null || actor == null || skill == null)
            { p.Summary = "공격·호위·지원 연결"; p.Grade = PreviewGrade.Close; return p; }
            var enemy = Unit(intent.ActorId);
            float multiplier = grade == IgniteGrade.Perfect ? 1.25f : grade == IgniteGrade.Miss ? .75f : 1f;
            for (int i = 0; i < Math.Max(skill.HitPowers.Length, intent.HitPowers.Length); i++)
            {
                bool a = i < skill.HitPowers.Length, b = i < intent.HitPowers.Length;
                float ap = a ? Power(skill.HitPowers[i], skill.Element, enemy, multiplier, false) : 0;
                float ep = b ? Power(intent.HitPowers[i], intent.Element, actor, 1, true) : 0;
                p.AllyPower = Math.Max(p.AllyPower, ap); p.EnemyPower = Math.Max(p.EnemyPower, ep);
                int result = !a ? -1 : !b ? 1 : BattleRules.CompareHit(ap, EffectiveSpeed(actor, skill.Speed), ep, EffectiveSpeed(enemy, intent.Speed));
                if (result > 0) p.AllyHits++; else if (result < 0) p.EnemyHits++; else p.Ties++;
            }
            p.Grade = p.EnemyHits == 0 && p.AllyHits > 0 ? PreviewGrade.Advantage : p.AllyHits >= p.EnemyHits ? PreviewGrade.Close : PreviewGrade.Disadvantage;
            p.Summary = string.Format("{0}  ·  아군 {1}타 / 적 {2}타  ({3:0.#} : {4:0.#})", p.Grade == PreviewGrade.Advantage ? "우세" : p.Grade == PreviewGrade.Close ? "접전" : "열세", p.AllyHits, p.EnemyHits, p.AllyPower, p.EnemyPower);
            return p;
        }

        public void RecommendLinks()
        {
            if (State.Phase != BattlePhase.Link) return;
            ClearLinks();
            // Preserve low-health characters before spending the remaining budget on clashes.
            foreach (var healer in State.Allies.Where(x => x.Alive))
            {
                var heal = healer.Skills.FirstOrDefault(x => x.Kind == SkillKind.Heal && x.Cost <= AvailableEmber);
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
                    foreach (var skill in actor.Skills.Where(x => x.Kind == SkillKind.Attack && x.Cost <= AvailableEmber))
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
                    var guard = State.Allies.Where(x => x.Alive && x.Id != intent.TargetId).FirstOrDefault(x => Enumerable.Range(0, x.SlotCount).Any(s => SlotFree(x, s)) && x.Skills.Any(s => s.Kind == SkillKind.Guard && s.Cost <= AvailableEmber));
                    if (guard != null)
                    {
                        var skill = guard.Skills.First(x => x.Kind == SkillKind.Guard && x.Cost <= AvailableEmber);
                        AddRecommended(guard, Enumerable.Range(0, guard.SlotCount).First(s => SlotFree(guard, s)), skill, LinkKind.Guard, intent.TargetId, "맞불 열세: 호위로 피해를 나누어 받기");
                    }
                }
            }
            foreach (var actor in State.Allies.Where(x => x.Alive))
            for (int slot = 0; slot < actor.SlotCount; slot++)
            {
                if (!SlotFree(actor, slot)) continue;
                var wounded = State.Allies.Where(x => x.Alive && x.Hp < x.MaxHp).OrderBy(x => (float)x.Hp / x.MaxHp).FirstOrDefault();
                var heal = actor.Skills.FirstOrDefault(x => x.Kind == SkillKind.Heal && x.Cost <= AvailableEmber);
                if (heal != null && wounded != null) { AddRecommended(actor, slot, heal, LinkKind.Support, wounded.Id, "남은 불씨로 부상 회복"); continue; }
                if (actor.IsSupport)
                {
                    var g = actor.Skills.FirstOrDefault(x => x.Kind == SkillKind.Guard && x.Cost <= AvailableEmber);
                    if (g != null) AddRecommended(actor, slot, g, LinkKind.Guard, State.Allies.First(x => x.Alive).Id, "서포트의 보호 연결");
                    continue;
                }
                var enemy = State.Enemies.Where(x => x.Alive).OrderBy(x => x.Hp).FirstOrDefault(); if (enemy == null) continue;
                var attack = actor.Skills.Where(x => x.Kind == SkillKind.Attack && x.Slots == 1 && x.Cost <= AvailableEmber).OrderByDescending(x => x.HitPowers.Sum()).FirstOrDefault();
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
            foreach (var enemy in State.Enemies.Where(x => x.Alive && x.Rift == 0 && x.BrokenThroughRound < State.Round)) enemy.Rift = enemy.MaxRift;
            Observe();
        }

        public void Restart()
        {
            if (factory == null) throw new InvalidOperationException("Restart requires a battle factory.");
            State = factory(); previousLinks.Clear(); guards.Clear(); elementsHit.Clear(); reactions.Clear(); shattered.Clear(); Observe();
        }

        private void Observe()
        {
            State.Intents.Clear(); State.Phase = BattlePhase.Observe;
            foreach (var enemy in State.Enemies.Where(x => x.Alive))
            {
                if (enemy.BrokenThroughRound >= State.Round) { Log("Skip", enemy.Id, "", enemy.Name + " — 균열로 이번 라운드 행동 불가", 0); continue; }
                var target = State.Allies.Where(x => x.Alive).OrderBy(x => (float)x.Hp / x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
                if (target == null) continue;
                var arm = enemy.Parts.FirstOrDefault(x => x.Id.EndsWith("arm", StringComparison.Ordinal));
                var core = enemy.Parts.FirstOrDefault(x => x.Id.EndsWith("core", StringComparison.Ordinal));
                int anger = Math.Min(5, State.Round - 1);
                if(!string.IsNullOrEmpty(enemy.IntentProfile)){
                    string profile=enemy.IntentProfile;bool solo=State.Allies.Count==1;
                    if(arm==null||!arm.Destroyed)State.Intents.Add(new IntentState { Id=State.Round+":anger_arm",ActorId=enemy.Id,TargetId=target.Id,PartId=arm==null?null:arm.Id,
                        Name=enemy.IsHuman?"진입 저지":profile=="tutorial"?"첫 습격":"연속 공격",HitPowers=profile=="tutorial"?new[]{5}:new[]{7+anger,7+anger},Speed=10,Element=enemy.Element,
                        Kind=profile=="piercing"?IntentKind.Piercing:IntentKind.Normal });
                    if(!solo&&profile!="tutorial"&&profile!="skirmish"&&(core==null||!core.Destroyed))State.Intents.Add(new IntentState { Id=State.Round+":anger_core",ActorId=enemy.Id,
                        TargetId=profile=="command"?target.Id:"all",PartId=core==null?null:core.Id,Name=profile=="illusion"?"기억의 잔향 · 허상":enemy.IsHuman?"강제 집행":"번지는 어둠",
                        HitPowers=profile=="command"?new[]{6+anger,6+anger}:new[]{4+anger,5+anger,4+anger},Speed=8,Element=enemy.Element,
                        Kind=profile=="illusion"?IntentKind.Illusion:profile=="command"?IntentKind.Piercing:IntentKind.Area });
                    if(State.Intents.All(x=>x.ActorId!=enemy.Id))State.Intents.Add(new IntentState { Id=State.Round+":anger_body",ActorId=enemy.Id,TargetId=target.Id,
                        Name=enemy.IsHuman?"남은 저항":"남은 어둠",HitPowers=new[]{6+anger},Speed=8,Element=enemy.Element });
                    continue;
                }
                if (arm == null || !arm.Destroyed) State.Intents.Add(new IntentState { Id = State.Round + ":anger_arm", ActorId = enemy.Id, TargetId = target.Id, PartId = arm == null ? null : arm.Id, Name = "난타", HitPowers = new[] { 10 + anger, 10 + anger }, Speed = 11, Element = Element.Shadow, Kind = IntentKind.Normal });
                if (core == null || !core.Destroyed) State.Intents.Add(new IntentState { Id = State.Round + ":anger_core", ActorId = enemy.Id, TargetId = "all", PartId = core == null ? null : core.Id, Name = "절규", HitPowers = new[] { 6 + anger, 6 + anger, 6 + anger }, Speed = 9, Element = Element.Shadow, Kind = IntentKind.Area });
                if (State.Intents.All(x => x.ActorId != enemy.Id)) State.Intents.Add(new IntentState { Id = State.Round + ":anger_body", ActorId = enemy.Id, TargetId = target.Id, Name = "남은 분노", HitPowers = new[] { 8 + anger }, Speed = 8, Element = Element.Shadow });
            }
            State.Phase = BattlePhase.Link;
            Log("Observe", "", "", "관측 — 예고선을 읽고 빛실을 연결하세요.", State.Intents.Count);
        }

        private void ResolveLink(LinkCommand link, IgniteGrade grade)
        {
            var actor = Unit(link.ActorId); var skill = Skill(link.ActorId, link.SkillId);
            float multiplier = 1;
            if (grade == IgniteGrade.Perfect)
            { multiplier = 1.25f + Math.Min(.25f, State.IgniteChain * .05f); State.IgniteChain++; AddLantern(3); }
            else if (grade == IgniteGrade.Miss) { multiplier = .75f; State.IgniteChain = 0; }
            Log("Ignite", actor.Id, link.TargetId, actor.Name + " · " + skill.Name + " — " + (grade == IgniteGrade.Perfect ? "점화" : grade == IgniteGrade.Good ? "양호" : "빗나감"), (int)Math.Round(multiplier * 100));
            if (link.Kind == LinkKind.Support)
            {
                var ally = Unit(link.TargetId); if (ally == null || !ally.Alive) return;
                int heal = Math.Min(ally.MaxHp - ally.Hp, (int)Math.Round(skill.Heal * multiplier)); ally.Hp += heal;
                Log("Heal", actor.Id, ally.Id, actor.Name + " → " + ally.Name + " 치유 +" + heal, heal); return;
            }
            if (link.Kind == LinkKind.Guard)
            { guards[link.TargetId] = actor.Id; Log("Guard", actor.Id, link.TargetId, actor.Name + "의 호위 — 받는 피해 35% 감소", 0); return; }
            if (link.Kind == LinkKind.Clash)
            {
                var intent = Intent(link.TargetId);
                if (intent != null && !intent.Resolved && IntentActive(intent)) { ResolveClash(actor, skill, intent, multiplier); return; }
                var enemy = intent == null ? null : Unit(intent.ActorId);
                Log("Relink", actor.Id, enemy == null ? "" : enemy.Id, "사라진 예고선 → 적 본체 공격으로 전환", 0);
                if (enemy != null && enemy.Alive) Strike(actor, skill, enemy, null, multiplier);
                return;
            }
            var target = TargetUnit(link.TargetId); if (target == null || !target.Alive) return;
            var part = target.Parts.FirstOrDefault(x => x.Id == link.TargetId && !x.Destroyed);
            Strike(actor, skill, target, part, multiplier);
        }

        private void Strike(UnitState actor, SkillData skill, UnitState enemy, PartState part, float multiplier)
        {
            bool hit = false;
            foreach (int raw in skill.HitPowers)
            {
                if (!enemy.Alive) break;
                int damage = Damage(actor, enemy, Power(raw, skill.Element, enemy, multiplier, false), 1);
                Hurt(actor, enemy, damage); RiftHit(enemy, skill.RiftDamage, part, skill.PartDamage); hit = true;
            }
            if (hit) React(skill.Element, enemy);
        }

        private void ResolveClash(UnitState actor, SkillData skill, IntentState intent, float multiplier)
        {
            var enemy = Unit(intent.ActorId); int wins = 0, losses = 0; bool hit = false;
            if (intent.Kind == IntentKind.Illusion)
            { intent.Resolved = true; Log("Illusion", actor.Id, enemy.Id, "허상선 — 빛실이 허공을 가릅니다.", 0); return; }
            for (int i = 0; i < Math.Max(skill.HitPowers.Length, intent.HitPowers.Length); i++)
            {
                if (!enemy.Alive) break;
                bool a = actor.Alive && i < skill.HitPowers.Length, b = i < intent.HitPowers.Length && IntentActive(intent);
                if (!a && !b) continue;
                float ap = a ? Power(skill.HitPowers[i], skill.Element, enemy, multiplier, false) : 0;
                float ep = b ? Power(intent.HitPowers[i], intent.Element, actor, 1, true) : 0;
                int result = !a ? -1 : !b ? 1 : BattleRules.CompareHit(ap, EffectiveSpeed(actor, skill.Speed), ep, EffectiveSpeed(enemy, intent.Speed));
                if (result > 0)
                { wins++; Hurt(actor, enemy, Damage(actor, enemy, ap, 1)); RiftHit(enemy, skill.RiftDamage, null, 0); hit = true; }
                else if (result < 0)
                { losses++; EnemyHit(enemy, intent, intent.HitPowers[i], actor); }
                else Log("Tie", actor.Id, enemy.Id, "파워·속도 동률 — 양쪽 타점 상쇄", 0);
            }
            intent.Resolved = true;
            if (hit) React(skill.Element, enemy);
            if (wins > losses)
            {
                AddLantern(10); if (clashEmber < 2) { clashEmber++; State.Ember = Math.Min(6, State.Ember + 1); }
                Log("ClashWin", actor.Id, enemy.Id, "맞불 우세 — " + wins + ":" + losses + " / 등불 +10", wins);
            }
            else Log("Clash", actor.Id, enemy.Id, "맞불 결과 — " + wins + ":" + losses, wins);
        }

        private void ResolveIntent(IntentState intent)
        {
            if (intent.Resolved || !IntentActive(intent)) return;
            var enemy = Unit(intent.ActorId);
            if (intent.Kind != IntentKind.Illusion)
            foreach (int raw in intent.HitPowers)
            { if (!IntentActive(intent)) break; EnemyHit(enemy, intent, raw, null); }
            intent.Resolved = true;
        }

        private void EnemyHit(UnitState enemy, IntentState intent, int raw, UnitState clashActor)
        {
            var targets = intent.Kind == IntentKind.Area ? State.Allies.Where(x => x.Alive).ToList() : new List<UnitState> { clashActor ?? Unit(intent.TargetId) };
            foreach (var original in targets)
            {
                if (original == null || !original.Alive) continue;
                var target = original; float reduction = 1;
                string guardId;
                if (clashActor == null && intent.Kind == IntentKind.Normal && guards.TryGetValue(original.Id, out guardId))
                { var guard = Unit(guardId); if (guard.Alive) { target = guard; reduction = .65f; } }
                int damage = Damage(enemy, target, Power(raw, intent.Element, target, 1, true), reduction);
                Hurt(enemy, target, damage);
            }
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
        private float Power(int raw, Element element, UnitState target, float ignite, bool enemy)
        { return raw * ignite * BattleRules.Affinity(element, target.Element) * (enemy && element == Element.Shadow && State.Gloom >= 2 ? 1 + (State.Gloom - 1) * .1f : 1); }
        private int Damage(UnitState actor, UnitState target, float power, float reduction)
        { return Math.Max(1, (int)Math.Round(Math.Max(1, actor.Attack * power / 10 - target.Defense) * reduction * (target.IsEnemy && target.BrokenThroughRound >= State.Round ? 1.3f : 1))); }
        private void Hurt(UnitState actor, UnitState target, int damage)
        {
            target.Hp = Math.Max(0, target.Hp - damage);
            if (!target.IsEnemy) AddLantern(2);
            Log("Damage", actor.Id, target.Id, actor.Name + " → " + target.Name + "  " + damage, damage);
            if (target.Hp == 0)
            { Log("DefeatUnit", actor.Id, target.Id, target.Name + " 전투불능", 0); if (!target.IsEnemy) State.Gloom = Math.Min(3, State.Gloom + 1); CancelInvalidIntents(); }
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
                    if (line != null) line.HitPowers = line.HitPowers.Take(line.HitPowers.Length - 1).ToArray();
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
