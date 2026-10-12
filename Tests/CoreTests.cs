using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RequiemLantern.Core;

internal static class CoreTests
{
    private static int passed;
    private static void Main()
    {
        Test("fresh battle deterministic intents", () => {
            var b = BattleEngine.CreatePrototype(); Equal(3, b.State.Ember); Equal(2, b.State.Intents.Count);
            Equal("난타", b.State.Intents[0].Name); Equal(2, b.State.Intents[0].HitPowers.Length); Equal(3, b.State.Intents[1].HitPowers.Length);
            Equal(2, b.State.Allies[0].SlotCount);
        });
        Test("slot occupancy, replacement and budget are atomic", () => {
            var b = BattleEngine.CreatePrototype(); string error;
            True(b.TryLink("lian", 0, "lian_crescendo", LinkKind.Strike, "anger", out error)); Equal(3, b.PlannedCost);
            False(b.TryLink("sera", 0, "sera_slash", LinkKind.Strike, "anger", out error)); Equal(1, b.State.Links.Count);
            True(b.TryLink("lian", 1, "lian_glimmer", LinkKind.Strike, "anger", out error)); Equal(0, b.PlannedCost); Equal(1, b.State.Links.Count);
            False(b.TryLink("lian", 1, "lian_crescendo", LinkKind.Strike, "anger", out error)); Equal(0, b.PlannedCost);
            False(b.TryLink("lian", 2, "lian_glimmer", LinkKind.Strike, "anger", out error));
            True(b.TryLink("lian", 0, "lian_glimmer", LinkKind.Clash, b.State.Intents[0].Id, out error));
            False(b.TryLink("sera", 0, "sera_glimmer", LinkKind.Clash, b.State.Intents[0].Id, out error));
        });
        Test("support slot and target restrictions", () => {
            var b = BattleEngine.CreatePrototype(); b.Unit("minato").IsSupport = true; string error;
            Equal(1, b.Unit("minato").SlotCount);
            False(b.TryLink("minato", 0, "minato_glimmer", LinkKind.Strike, "anger", out error));
            False(b.TryLink("minato", 1, "minato_heal", LinkKind.Support, "lian", out error));
            True(b.TryLink("minato", 0, "minato_heal", LinkKind.Support, "lian", out error));
            False(b.TryLink("lian", 0, "lian_glimmer", LinkKind.Support, "lian", out error));
        });
        Test("power, speed tie-break and complete tie", () => {
            Equal(1, BattleRules.CompareHit(10, 1, 9, 100)); Equal(1, BattleRules.CompareHit(9, 12, 9, 11));
            Equal(-1, BattleRules.CompareHit(9, 11, 9, 12)); Equal(0, BattleRules.CompareHit(9, 12, 9, 12));
            var b = Fixture(new[] { 9 }, new[] { 9 }, 12, 12); Link(b); b.ResolveRound();
            Equal(100, b.Unit("hero").Hp); Equal(300, b.Unit("enemy").Hp); True(b.State.Events.Any(x => x.Type == "Tie"));
        });
        Test("unpaired hits land after paired hits", () => {
            var b = Fixture(new[] { 8, 8, 8 }, new[] { 9, 9 }, 12, 11); Link(b); b.ResolveRound();
            Equal(82, b.Unit("hero").Hp); Equal(292, b.Unit("enemy").Hp);
        });
        Test("perfect ignition reverses clash", () => {
            var b = Fixture(new[] { 9, 9, 9 }, new[] { 10, 10 }, 12, 11); Link(b); b.ResolveRound(IgniteGrade.Perfect);
            Equal(100, b.Unit("hero").Hp); Equal(267, b.Unit("enemy").Hp); Equal(13, b.State.Lantern);
        });
        Test("clash binds earlier enemy attack", () => {
            var b = Fixture(new[] { 12, 12 }, new[] { 9, 9 }, 5, 20); Link(b); b.ResolveRound();
            Equal(100, b.Unit("hero").Hp); Equal(276, b.Unit("enemy").Hp);
        });
        Test("per-slot ignition grades are independent", () => {
            var b = Fixture(new[] { 8 }, new[] { 9 }, 12, 11); string error;
            True(b.TryLink("hero", 0, "hit", LinkKind.Strike, "enemy", out error));
            True(b.TryLink("hero", 1, "hit", LinkKind.Strike, "enemy", out error)); b.State.Intents.Clear();
            b.ResolveRound(new Dictionary<string, IgniteGrade> { { "hero:0", IgniteGrade.Perfect }, { "hero:1", IgniteGrade.Miss } });
            Equal(284, b.Unit("enemy").Hp); Equal(0, b.State.IgniteChain); Equal(3, b.State.Lantern);
        });
        Test("area forecast finishes after its clash opponent falls", () => {
            var b = Fixture(new[] { 1 }, new[] { 20, 20, 20 }, 12, 11); b.Unit("hero").Hp = 5;
            b.State.Intents[0].Kind = IntentKind.Area;
            b.State.Allies.Add(new UnitState { Id = "other", Name = "동료", Element = Element.Flame, Hp = 100, MaxHp = 100, Defense = 0 });
            Link(b); b.ResolveRound(); Equal(40, b.Unit("other").Hp);
        });
        Test("unopposed forecast deals damage", () => {
            var b = Fixture(new[] { 1 }, new[] { 9, 9 }, 12, 11); b.ResolveRound(); Equal(82, b.Unit("hero").Hp);
        });
        Test("part break cancels forecast and redirects clash", () => {
            var b = BattleEngine.CreatePrototype(); b.Unit("anger").Parts[0].Hp = 1; string error;
            True(b.TryLink("sera", 0, "sera_glimmer", LinkKind.Strike, "anger_arm", out error));
            True(b.TryLink("lian", 0, "lian_glimmer", LinkKind.Clash, b.State.Intents[0].Id, out error));
            b.ResolveRound(); True(b.State.Intents[0].Cancelled); True(b.State.Events.Any(x => x.Type == "Relink"));
            True(b.State.Events.Any(x => x.Type == "PartBreak")); b.BeginNextRound(); False(b.State.Intents.Any(x => x.Name == "난타"));
        });
        Test("rift cancels now and skips exactly next round", () => {
            var b = BattleEngine.CreatePrototype(); b.Unit("anger").Rift = 1; string error;
            True(b.TryLink("sera", 0, "sera_glimmer", LinkKind.Strike, "anger", out error));
            b.ResolveRound(); True(b.State.Intents.All(x => x.Cancelled)); Equal(150, b.Unit("lian").Hp);
            b.BeginNextRound(); Equal(2, b.State.Round); Equal(0, b.State.Intents.Count);
            b.ResolveRound(); b.BeginNextRound(); Equal(3, b.State.Round); Equal(125, b.Unit("anger").Rift); Equal(2, b.State.Intents.Count);
        });
        Test("guard redirects but piercing bypasses", () => {
            var b = BattleEngine.CreatePrototype(); string error; b.State.Intents.RemoveAt(1); b.State.Intents[0].TargetId = "minato";
            True(b.TryLink("lian", 0, "lian_guard", LinkKind.Guard, "minato", out error)); b.ResolveRound();
            Equal(100, b.Unit("minato").Hp); True(b.Unit("lian").Hp < 150);
            b = BattleEngine.CreatePrototype(); b.State.Intents.RemoveAt(1); b.State.Intents[0].TargetId = "minato"; b.State.Intents[0].Kind = IntentKind.Piercing;
            True(b.TryLink("lian", 0, "lian_guard", LinkKind.Guard, "minato", out error)); b.ResolveRound();
            True(b.Unit("minato").Hp < 100); Equal(150, b.Unit("lian").Hp);
        });
        Test("support heals and clamps at maximum", () => {
            var b = BattleEngine.CreatePrototype(); b.State.Intents.Clear(); b.Unit("lian").Hp = 140; string error;
            True(b.TryLink("minato", 0, "minato_heal", LinkKind.Support, "lian", out error)); b.ResolveRound(); Equal(150, b.Unit("lian").Hp);
        });
        Test("lantern is slot-free, spent first and reduces gloom", () => {
            var b = BattleEngine.CreatePrototype(); b.State.Lantern = 100; string error;
            True(b.TryLantern("lian", out error)); False(b.TryLantern("lian", out error)); Equal(0, b.State.Links.Count);
            b.ResolveRound(); Equal("Lantern", b.State.Events[0].Type); Equal(0, b.State.Gloom);
        });
        Test("gloom reduces ember recovery and timing windows", () => {
            var b = BattleEngine.CreatePrototype(); b.State.Intents.Clear(); b.State.Gloom = 3; b.State.Ember = 0; b.ResolveRound(); Equal(2, b.State.Ember);
            Equal(IgniteGrade.Perfect, BattleRules.GradeTiming(80, 0)); Equal(IgniteGrade.Good, BattleRules.GradeTiming(80, 3));
            Equal(IgniteGrade.Perfect, BattleRules.GradeTiming(80, 3, true)); Equal(IgniteGrade.Miss, BattleRules.GradeTiming(250, 0));
        });
        Test("different elements purify only after hits", () => {
            var b = BattleEngine.CreatePrototype(); b.State.Intents.Clear(); string error;
            True(b.TryLink("sera", 0, "sera_glimmer", LinkKind.Strike, "anger", out error));
            True(b.TryLink("lian", 0, "lian_glimmer", LinkKind.Strike, "anger", out error)); b.ResolveRound();
            Equal(0, b.State.Gloom); True(b.State.Events.Any(x => x.Type == "Resonance" && x.Message.Contains("정화")));
        });
        Test("auto clears within five rounds at Good", () => {
            var b = PlayAuto(); Console.WriteLine("  auto rounds=" + b.State.Round + " surviving=" + b.State.Allies.Count(x => x.Alive));
            Equal(BattleOutcome.Victory, b.State.Outcome); True(b.State.Round <= 5);
        });
        Test("recommendation budgets and determinism", () => {
            string first = TraceAuto(); for (int i = 0; i < 100; i++) Equal(first, TraceAuto());
        });
        Test("defeat and restart fully reset", () => {
            var b = BattleEngine.CreatePrototype(); string id = b.State.BattleId;
            for (int i = 0; i < 30 && b.State.Outcome == BattleOutcome.Ongoing; i++) { b.ResolveRound(); b.BeginNextRound(); }
            Equal(BattleOutcome.Defeat, b.State.Outcome); b.Restart(); Equal(BattleOutcome.Ongoing, b.State.Outcome); Equal(1, b.State.Round); Equal(3, b.State.Ember); True(b.State.BattleId != id);
            Equal(360, b.Unit("anger").Hp); Equal(150, b.Unit("lian").Hp); Equal(0, b.State.Links.Count);
        });
        Test("victory rewards claim once, repeats have no first-clear memory", () => {
            var p = new ProgressService(new ProgressData()); RewardData reward; var b = BattleEngine.CreatePrototype();
            False(p.ClaimVictory(b, out reward)); b = PlayAuto(); True(p.ClaimVictory(b, out reward)); Equal(120, p.Data.Coins); Equal(1, p.Data.MemoryFragments);
            False(p.ClaimVictory(b, out reward)); True(p.ClaimVictory(PlayAuto(), out reward)); Equal(240, p.Data.Coins); Equal(1, p.Data.MemoryFragments); Equal(12, p.Data.LanternFragments);
        });
        Test("repair cost and cap", () => {
            var p = new ProgressService(new ProgressData()); string error; False(p.RepairKeep(out error));
            p.Data.Coins = 300; True(p.RepairKeep(out error)); True(p.RepairKeep(out error)); Equal(3, p.Data.KeepLevel); Equal(0, p.Data.Coins); False(p.RepairKeep(out error));
        });
        Test("atomic save roundtrip and corrupt-primary recovery", () => {
            string directory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Tests", "test-output", "save-" + Guid.NewGuid().ToString("N"));
            try {
                var s = new SaveStore(System.IO.Path.Combine(directory, "progress.xml")); var d = s.Load(); d.Coins = 120; d.PlayerName = "카이 & 빛"; s.Save(d);
                Equal(120, s.Load().Coins); Equal("카이 & 빛", s.Load().PlayerName); d.Coins = 240; s.Save(d); Equal(240, s.Load().Coins);
                File.WriteAllText(s.Path, "corrupt"); Equal(120, s.Load().Coins); True(s.LastWarning != null);
                File.Delete(s.Path); Equal(120, s.Load().Coins);
            } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        });
        Test("story preserves names, quotations and prototype flags", () => {
            True(StoryCatalog.Prologue.Any(x => x.Speaker == "리안" && x.Text == "…움직이지 마. 그림자가 소리에 반응해." && !x.IsPrototype));
            True(StoryCatalog.Prologue.Any(x => x.IsPrototype)); True(StoryCatalog.FirstMeal.Any(x => x.Speaker == "미나토"));
            False(StoryCatalog.Prologue.Any(x => x.Text.Contains("실험체")));
        });
        Test("character stories retain scope, complete scene metadata and ordered music cues", () => {
            Equal("lian,sera,noa",string.Join(",",CharacterStoryCatalog.All.Select(s=>s.Id)));
            Equal(null,CharacterStoryCatalog.Get("minato"));Equal(null,CharacterStoryCatalog.Get("unknown"));
            foreach(var story in CharacterStoryCatalog.All){
                True(story.Lines.Count>=48);Equal(story.Id=="lian"?7:8,story.Lines.Select(l=>l.Caption).Distinct().Count());
                True(story.Lines.All(l=>l.IsPrototype&&!string.IsNullOrWhiteSpace(l.Text)&&!string.IsNullOrWhiteSpace(l.Location)));
                Equal(story.MusicId+"_phase1,"+story.MusicId+"_phase2,"+story.MusicId+"_afterglow",
                    string.Join(",",story.Lines.Where(l=>!string.IsNullOrEmpty(l.MusicCue)).Select(l=>l.MusicCue)));
                False(story.Lines.Any(l=>l.Text.Contains("실험 총책임자")));
            }
        });
        Test("human antagonists speak before their boss fight and remain distinct from Umbra", () => {
            Equal("umbra",CharacterStoryCatalog.Get("lian").BossKind);
            foreach(var pair in new[]{new[]{"sera","엘렌"},new[]{"noa","이브"}}){
                var story=CharacterStoryCatalog.Get(pair[0]);Equal("human",story.BossKind);
                True(!string.IsNullOrWhiteSpace(story.BossFaction));True(story.BossName.Contains(pair[1]));
                int encounter=story.Lines.FindIndex(l=>l.MusicCue==story.MusicId+"_phase1");
                True(encounter>0);True(story.Lines.Take(encounter).Any(l=>l.Speaker==pair[1]));
                True(story.Lines.Skip(encounter).Any(l=>l.Text.Contains("구금")));
            }
            True(CharacterStoryCatalog.Get("lian").Lines.Any(l=>l.Speaker=="로웬"));
        });
        Console.WriteLine("PASS: " + passed + " tests");
    }

    private static BattleEngine Fixture(int[] allyHits, int[] enemyHits, int allySpeed, int enemySpeed)
    {
        var state = new BattleState(); state.Gloom = 0;
        var hero = new UnitState { Id = "hero", Name = "테스트 잔영", Element = Element.Flame, Hp = 100, MaxHp = 100, Attack = 10, Defense = 0, Speed = allySpeed };
        hero.Skills.Add(new SkillData { Id = "hit", Name = "시험", Element = Element.Flame, HitPowers = allyHits, Speed = allySpeed, RiftDamage = 0 });
        var enemy = new UnitState { Id = "enemy", Name = "테스트 군체", IsEnemy = true, Element = Element.Flame, Hp = 300, MaxHp = 300, Attack = 10, Defense = 0, Rift = 100, MaxRift = 100, Speed = enemySpeed };
        state.Allies.Add(hero); state.Enemies.Add(enemy);
        state.Intents.Add(new IntentState { Id = "forecast", ActorId = "enemy", TargetId = "hero", Name = "시험 예고선", HitPowers = enemyHits, Element = Element.Flame, Speed = enemySpeed });
        return new BattleEngine(state);
    }
    private static void Link(BattleEngine b) { string error; True(b.TryLink("hero", 0, "hit", LinkKind.Clash, "forecast", out error)); }
    private static BattleEngine PlayAuto()
    {
        var b = BattleEngine.CreatePrototype();
        for (int i = 0; i < 15 && b.State.Outcome == BattleOutcome.Ongoing; i++)
        { b.RecommendLinks(); True(b.PlannedCost <= b.State.Ember); b.ResolveRound(); b.BeginNextRound(); }
        return b;
    }
    private static string TraceAuto()
    {
        var b = BattleEngine.CreatePrototype(); var trace = new List<string>();
        for (int i = 0; i < 15 && b.State.Outcome == BattleOutcome.Ongoing; i++)
        { b.RecommendLinks(); True(b.PlannedCost <= b.State.Ember); b.ResolveRound(); trace.AddRange(b.State.Events.Select(x => x.Message)); b.BeginNextRound(); }
        return string.Join("|", trace);
    }
    private static void Test(string name, Action test)
    { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { Console.Error.WriteLine("FAIL " + name + "\n" + ex); Environment.Exit(1); } }
    private static void True(bool value) { if (!value) throw new Exception("Expected true"); }
    private static void False(bool value) { if (value) throw new Exception("Expected false"); }
    private static void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception("Expected " + expected + ", actual " + actual); }
}
