using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using RequiemLantern.Core;

internal static class RecurringContentTests
{
    static int passed;
    static readonly List<string> balanceRuns=new List<string>();
    sealed class Clock { public DateTime Now=Utc("2026-10-16T00:00:00Z"); }
    static void Main()
    {
        Test("legacy saves initialize activities without retroactive grants or changing collection",()=>{
            var clock=new Clock();var original=Player(clock);original.Data.Coins=321;original.Data.RecruitPity=39;original.Data.PickupGuaranteed=true;
            original.Data.ClearedStages.Add("c1s02");var directory=Temp();var path=Path.Combine(directory,"legacy.xml");var store=new SaveStore(path);store.Save(original.Data);
            var xml=XDocument.Load(path);xml.Root.Element("Activities").Remove();xml.Save(path);
            var loaded=new ProgressService(store.Load(),null,()=>clock.Now);Equal(null,store.LastWarning);Equal(321,loaded.Data.Coins);Equal(39,loaded.Data.RecruitPity);True(loaded.Data.PickupGuaranteed);
            Equal("lian,minato,heidel,mare",string.Join(",",loaded.Data.ActiveParty));var view=loaded.GetActivities();True(view.DungeonUnlocked);Equal(0,view.DailyQuests.Single(x=>x.Id=="daily_victory").Current);
            Equal(0,loaded.Data.Stardust);Equal(0,view.WeeklyDungeonRewardsClaimed);
        });
        Test("KST five o'clock daily and Monday weekly reset boundaries are exact",()=>{
            var clock=new Clock { Now=Utc("2026-10-18T19:59:59Z") };var p=Player(clock);var before=p.GetActivities();Equal("2026-10-18",before.DailyKey);Equal("2026-10-12",before.WeeklyKey);
            Equal(Utc("2026-10-18T20:00:00Z"),before.NextDailyResetUtc);Equal(Utc("2026-10-18T20:00:00Z"),before.NextWeeklyResetUtc);Equal(DateTimeKind.Utc,before.NextDailyResetUtc.Kind);
            True(p.CheckIn());True(p.ClaimQuest("daily_checkin",before.DailyKey,out var reward,out var error));
            p.Data.Activities.WeeklyVictories=9;clock.Now=clock.Now.AddSeconds(1);var after=p.GetActivities();Equal("2026-10-19",after.DailyKey);Equal("2026-10-19",after.WeeklyKey);
            Equal(0,after.WeeklyQuests[0].Current);False(after.DailyQuests[0].Claimed);Equal(0,after.DailyQuests[0].Current);Equal(20,p.Data.Stardust);
        });
        Test("ordinary snapshots and rejected claims leave the entire save unchanged",()=>{
            var clock=new Clock();var p=Player(clock);var view=p.GetActivities();string before=Snapshot(p.Data);
            for(int i=0;i<10;i++){clock.Now=clock.Now.AddMinutes(2);p.GetActivities();}Equal(before,Snapshot(p.Data));
            False(p.ClaimQuest("missing",view.DailyKey,out var reward,out var error));Equal(before,Snapshot(p.Data));
            False(p.ClaimQuest("daily_victory",view.DailyKey,out reward,out error));Equal(before,Snapshot(p.Data));
            False(p.ClaimQuest("daily_checkin","2026-10-15",out reward,out error));Equal(before,Snapshot(p.Data));
            clock.Now=clock.Now.AddDays(1);False(p.ClaimQuest("daily_checkin",view.DailyKey,out reward,out error));Equal(before,Snapshot(p.Data));
        });
        Test("check in is once per day and must be explicitly claimed",()=>{
            var clock=new Clock();var p=Player(clock);True(p.CheckIn());False(p.CheckIn());Equal(0,p.Data.Stardust);
            var quest=p.GetActivities().DailyQuests[0];True(quest.CanClaim);True(p.ClaimQuest(quest.Id,quest.PeriodKey,out var reward,out var error));Equal(20,reward.Stardust);Equal(100,p.Data.Coins);Equal(1,p.Data.LanternFragments);
            string before=Snapshot(p.Data);False(p.ClaimQuest(quest.Id,quest.PeriodKey,out reward,out error));Equal(before,Snapshot(p.Data));
        });
        Test("clock rollback cannot reopen any previously paid day or week, including after reload",()=>{
            var clock=new Clock();var p=Player(clock);p.CheckIn();var first=p.GetActivities();p.ClaimQuest("daily_checkin",first.DailyKey,out var reward,out var error);
            clock.Now=clock.Now.AddDays(7);p.CheckIn();var future=p.GetActivities();p.ClaimQuest("daily_checkin",future.DailyKey,out reward,out error);p.Data.Activities.WeeklyDungeonRewardsClaimed=3;
            var store=new SaveStore(Path.Combine(Temp(),"progress.xml"));store.Save(p.Data);clock.Now=clock.Now.AddDays(-7);p=new ProgressService(store.Load(),null,()=>clock.Now);
            var rolled=p.GetActivities();Equal(future.DailyKey,rolled.DailyKey);Equal(future.WeeklyKey,rolled.WeeklyKey);False(p.CheckIn());Equal(3,rolled.WeeklyDungeonRewardsClaimed);
            False(p.ClaimQuest("daily_checkin",first.DailyKey,out reward,out error));Equal(40,p.Data.Stardust);
        });
        Test("story-only stages and standalone training never increase activity progress",()=>{
            var p=Player(new Clock());p.GetActivities();var story=CampaignCatalog.Chapters[0].Stages.First(s=>!s.HasBattle);CampaignRules.Complete(p.Data,story);
            var training=BattleEngine.CreatePrototype(p.Data,0);Win(training);True(p.ClaimVictory(training,out var reward));
            Equal(0,p.GetActivities().WeeklyQuests[0].Current);Equal(0,p.Data.Activities.WeeklyClashes);Equal(0,p.Data.Activities.WeeklyDefenses);
        });
        Test("real campaign victory records actual resolved clashes and defenses only when received",()=>{
            var p=Player(new Clock());p.GetActivities();var battle=WonMain(p,true);True(battle.ResolvedClashes>=3);True(battle.ResolvedDefenses>=2);
            Equal(0,p.Data.Activities.DailyVictories);True(p.ClaimVictory(battle,out var reward));Equal(1,p.Data.Activities.DailyVictories);Equal(3,p.Data.Activities.DailyClashes);Equal(2,p.Data.Activities.DailyDefenses);
            string before=Snapshot(p.Data);False(p.ClaimVictory(battle,out reward));Equal(before,Snapshot(p.Data));
        });
        Test("defeat, exit, unexecuted plans, and restarting cannot bank progress",()=>{
            var p=Player(new Clock());p.GetActivities();var battle=CampaignBattleFactory.Create(Boss(),p.Data,0);battle.RecommendLinks();Equal(0,battle.ResolvedClashes);
            foreach(var ally in battle.State.Allies)ally.Hp=0;battle.ResolveRound();Equal(BattleOutcome.Defeat,battle.State.Outcome);False(p.ClaimVictory(battle,out var reward));Equal(0,p.Data.Activities.WeeklyVictories);
            battle=WonMain(p,true);True(battle.ResolvedClashes>0);True(battle.ResolvedDefenses>0);battle.Restart();Equal(0,battle.ResolvedClashes);Equal(0,battle.ResolvedDefenses);False(p.ClaimVictory(battle,out reward));
        });
        Test("dungeon unlock waits for 1-2 story completion and does not consume entries on failure",()=>{
            var p=Player(new Clock());var session=new WeeklyDungeonSession();False(session.Enter("coast",p,false,out var error));var stage=CampaignCatalog.Get("c1s02");p.Data.RewardedStages.Add(stage.Id);
            False(session.Enter("coast",p,false,out error));CampaignRules.Complete(p.Data,stage);True(session.Enter("coast",p,false,out error));
            string before=Snapshot(p.Data);False(session.Enter("reef",p,false,out error));Equal(before,Snapshot(p.Data));
            foreach(var ally in session.Battle.State.Allies)ally.Hp=0;session.Battle.ResolveRound();False(session.ClaimVictory(p,out var reward,out error));Equal(0,p.GetActivities().WeeklyDungeonRewardsClaimed);
            session.Restart(p);Equal(BattleOutcome.Ongoing,session.Battle.State.Outcome);session.Exit();Equal(null,session.Active);Equal(0,p.GetActivities().WeeklyDungeonRewardsClaimed);
        });
        Test("all three dungeon difficulties preserve starter party, flame rules and action ceilings and really win",()=>{
            var p=Unlocked(new Clock());foreach(var dungeon in DungeonCatalog.Definitions)foreach(int seed in new[]{0,1,42}){
                var battle=DungeonCatalog.Create(dungeon,p.Data,seed);Equal("lian,minato,heidel,mare",string.Join(",",battle.State.Allies.Select(a=>a.Id)));Equal(dungeon.EnemyCount,battle.State.Enemies.Count);
                True(battle.State.Allies.All(a=>a.SyncLevel==0&&a.SkillHand.Count==2));Equal(0,battle.State.CoinReward);Equal(0,battle.State.MemoryReward);
                Equal(0,battle.State.Allies.Sum(a=>a.Skills.Count(s=>s.Number==3&&BattleRules.IsSkillUnlocked(a,s))));Win(battle);
                balanceRuns.Add(dungeon.Id+" seed="+seed+" result="+battle.State.Outcome+" rounds="+battle.State.Round+" clashes="+battle.ResolvedClashes);
                if(dungeon.IsBoss)True(battle.State.Allies[2].SlotCount==1&&battle.State.Allies[3].SlotCount==1);
            }
        });
        Test("the three paid wins share one weekly quota, and dungeon rewards never clear main stages",()=>{
            var p=Unlocked(new Clock());var clears=string.Join(",",p.Data.ClearedStages);var stages=string.Join(",",p.Data.RewardedStages);var session=new WeeklyDungeonSession();
            foreach(var dungeon in DungeonCatalog.Definitions){True(session.Enter(dungeon.Id,p,false,out var error));Win(session.Battle);False(p.ClaimVictory(session.Battle,out var normal));
                True(session.ClaimVictory(p,out var reward,out error));Equal(dungeon.Reward.Stardust,reward.Stardust);False(session.ClaimVictory(p,out reward,out error));session.Exit();}
            Equal(3,p.GetActivities().WeeklyDungeonRewardsClaimed);Equal(300,p.Data.Stardust);Equal(clears,string.Join(",",p.Data.ClearedStages));Equal(stages,string.Join(",",p.Data.RewardedStages));
            False(session.Enter("coast",p,false,out var reason));True(reason.Contains("연습"));Equal(3,p.Data.ClaimedBattles.Count);
        });
        Test("practice remains fixed across a weekly reset and gives no currency or quest progress",()=>{
            var clock=new Clock { Now=Utc("2026-10-18T19:59:00Z") };var p=Unlocked(clock);p.GetActivities();var session=new WeeklyDungeonSession();True(session.Enter("coast",p,true,out var error));
            Win(session.Battle);clock.Now=clock.Now.AddMinutes(2);var before=p.GetActivities();True(session.ClaimVictory(p,out var reward,out error));Equal("보상 없음",reward.Summary);Equal(0,p.Data.Stardust);Equal(0,p.Data.Coins);
            Equal(0,p.Data.Activities.WeeklyVictories);Equal(0,p.Data.Activities.WeeklyClashes);Equal(0,p.Data.Activities.WeeklyDungeonRewardsClaimed);False(session.ClaimVictory(p,out reward,out error));
        });
        Test("paid dungeon rewards use the week of receipt",()=>{
            var clock=new Clock { Now=Utc("2026-10-18T19:59:00Z") };var p=Unlocked(clock);p.GetActivities();p.Data.Activities.WeeklyDungeonRewardsClaimed=2;
            var session=new WeeklyDungeonSession();True(session.Enter("coast",p,false,out var error));Win(session.Battle);clock.Now=clock.Now.AddMinutes(2);
            True(session.ClaimVictory(p,out var reward,out error));Equal("2026-10-19",p.Data.Activities.WeeklyKey);Equal(1,p.Data.Activities.WeeklyDungeonRewardsClaimed);Equal(80,reward.Stardust);
        });
        Test("every quest can be earned through actual play and pays its exact reward once",()=>{
            var p=Unlocked(new Clock());p.CheckIn();var session=new WeeklyDungeonSession();
            for(int i=0;i<3;i++){True(session.Enter("coast",p,false,out var error));Win(session.Battle);True(session.ClaimVictory(p,out var reward,out error));session.Exit();}
            for(int i=0;i<15&&(p.Data.Activities.WeeklyVictories<10||p.Data.Activities.WeeklyClashes<20||p.Data.Activities.WeeklyDefenses<8);i++)True(p.ClaimVictory(WonMain(p,i==0),out var reward));
            var view=p.GetActivities();True(view.DailyQuests.Concat(view.WeeklyQuests).All(q=>q.CanClaim));int star=p.Data.Stardust,coins=p.Data.Coins,lantern=p.Data.LanternFragments,memory=p.Data.MemoryFragments,shards=p.Data.CharacterShards;
            foreach(var quest in view.DailyQuests.Concat(view.WeeklyQuests)){True(p.ClaimQuest(quest.Id,quest.PeriodKey,out var reward,out var error));False(p.ClaimQuest(quest.Id,quest.PeriodKey,out reward,out error));}
            Equal(800,p.Data.Stardust-star);Equal(4700,p.Data.Coins-coins);Equal(27,p.Data.LanternFragments-lantern);Equal(3,p.Data.MemoryFragments-memory);Equal(30,p.Data.CharacterShards-shards);
        });
        Test("quest overflow checks all balances before changing claims or any balance",()=>{
            foreach(string field in new[]{"Stardust","Coins","LanternFragments","MemoryFragments","CharacterShards"}){
                var p=Player(new Clock());p.GetActivities();p.Data.Activities.WeeklyClashes=20;p.Data.Activities.WeeklyDefenses=8;p.CheckIn();
                typeof(ProgressData).GetField(field).SetValue(p.Data,int.MaxValue);string id=field=="MemoryFragments"?"weekly_clash":field=="CharacterShards"?"weekly_defense":"daily_checkin";
                string key=id.StartsWith("weekly")?p.Data.Activities.WeeklyKey:p.Data.Activities.DailyKey;string before=Snapshot(p.Data);
                False(p.ClaimQuest(id,key,out var reward,out var error));True(error.Contains("한도"));Equal(before,Snapshot(p.Data));
            }
        });
        Test("dungeon overflow is atomic across all five currencies and remains retryable",()=>{
            var p=Unlocked(new Clock());p.GetActivities();var session=new WeeklyDungeonSession();session.Enter("watch",p,false,out var error);Win(session.Battle);var checkpoint=p.Capture();
            foreach(string field in new[]{"Stardust","Coins","LanternFragments","MemoryFragments","CharacterShards"}){
                p.Restore(checkpoint);typeof(ProgressData).GetField(field).SetValue(p.Data,int.MaxValue);string before=Snapshot(p.Data);
                False(session.ClaimVictory(p,out var reward,out error));True(error.Contains("한도"));Equal(before,Snapshot(p.Data));
            }
            p.Restore(checkpoint);True(session.ClaimVictory(p,out var success,out error));Equal(120,success.Stardust);
        });
        Test("failed persistence can restore and retry a quest and a dungeon without losing the session",()=>{
            var p=Unlocked(new Clock());p.CheckIn();var key=p.GetActivities().DailyKey;var checkpoint=p.Capture();string before=Snapshot(p.Data);
            True(p.ClaimQuest("daily_checkin",key,out var reward,out var error));FailSave(p,checkpoint);Equal(before,Snapshot(p.Data));True(p.ClaimQuest("daily_checkin",key,out reward,out error));
            var session=new WeeklyDungeonSession();session.Enter("coast",p,false,out error);Win(session.Battle);checkpoint=p.Capture();before=Snapshot(p.Data);
            True(session.ClaimVictory(p,out reward,out error));FailSave(p,checkpoint);Equal(before,Snapshot(p.Data));True(session.Active!=null);Equal(BattleOutcome.Victory,session.Battle.State.Outcome);
            True(session.ClaimVictory(p,out reward,out error));Equal(1,p.Data.Activities.WeeklyDungeonRewardsClaimed);False(session.ClaimVictory(p,out reward,out error));
        });
        Test("campaign checkpoint restores before and after-story phase on failed persistence",()=>{
            var p=Player(new Clock());p.Data.PrologueCompleted=true;var session=new CampaignSession();True(session.Enter("c1s02",p.Data,false,out var error));
            var initial=session.Capture();True(session.FinishStory(p.Data,out var changed,out error));Equal(CampaignPhase.Battle,session.Phase);session.Restore(initial);Equal(CampaignPhase.BeforeStory,session.Phase);
            session.FinishStory(p.Data,out changed,out error);var battle=CampaignBattleFactory.Create(session.Active,p.Data,0);Win(battle);var progressCheckpoint=p.Capture();var campaignCheckpoint=session.Capture();
            True(session.AcceptVictory(battle,p,out var reward,out error));Equal(CampaignPhase.AfterStory,session.Phase);p.Restore(progressCheckpoint);session.Restore(campaignCheckpoint);
            Equal(CampaignPhase.Battle,session.Phase);True(session.AcceptVictory(battle,p,out reward,out error));Equal(1,p.Data.Activities.WeeklyVictories);
        });
        Test("activity progress, claimed quests, quota and high-water keys survive save and deep rollback",()=>{
            var p=Unlocked(new Clock());p.CheckIn();var key=p.GetActivities().DailyKey;p.ClaimQuest("daily_checkin",key,out var reward,out var error);p.ClaimVictory(WonMain(p,true),out var normal);
            var store=new SaveStore(Path.Combine(Temp(),"progress.xml"));store.Save(p.Data);Equal(Snapshot(p.Data),Snapshot(store.Load()));var checkpoint=p.Capture();checkpoint.Activities.DailyClaimedQuestIds.Clear();Equal(1,p.Data.Activities.DailyClaimedQuestIds.Count);
            p.Restore(checkpoint);checkpoint.Activities.DailyClashes=0;Equal(3,p.Data.Activities.DailyClashes);
        });
        Test("invalid activity state is rejected with the previous good save preserved",()=>{
            var p=Player(new Clock());p.CheckIn();var path=Path.Combine(Temp(),"progress.xml");var store=new SaveStore(path);store.Save(p.Data);string valid=File.ReadAllText(path);
            foreach(Action<RecurringProgress> corrupt in new Action<RecurringProgress>[] { a=>a.DailyClashes=-1,a=>a.WeeklyDungeonRewardsClaimed=4,a=>a.WeeklyKey="2026-10-16",a=>a.DailyKey="oops",a=>a.DailyKey="9999-12-31",a=>a.DailyClaimedQuestIds.Add("weekly_clash"),a=>a.DailyClaimedQuestIds.Add("daily_victory") }){
                var copy=p.Capture();corrupt(copy.Activities);bool failed=false;try{store.Save(copy);}catch(InvalidOperationException){failed=true;}True(failed);Equal(valid,File.ReadAllText(path));var checkedStore=new SaveStore(path);checkedStore.Load();Equal(null,checkedStore.LastWarning);
            }
        });
        Directory.CreateDirectory(Path.Combine("QA","RecurringContent","2026-10-16"));
        File.WriteAllLines(Path.Combine("QA","RecurringContent","2026-10-16","core-dungeon-balance-runs.txt"),balanceRuns);
        Console.WriteLine("Recurring content: "+passed+" checks passed; "+balanceRuns.Count+" deterministic dungeon wins.");
    }
    static ProgressService Player(Clock clock) { return new ProgressService(new ProgressData(),null,()=>clock.Now); }
    static ProgressService Unlocked(Clock clock) { var p=Player(clock);CampaignRules.Complete(p.Data,CampaignCatalog.Get("c1s02"));return p; }
    static StageData Boss() { return CampaignCatalog.Get("chapter1_beacon_defense"); }
    static BattleEngine WonMain(ProgressService p,bool defenses=false)
    {
        var battle=CampaignBattleFactory.Create(Boss(),p.Data,0);
        if(defenses)for(int round=0;round<2;round++){
            foreach(var ally in battle.State.Allies){var skill=ally.Skills.Single(BattleRules.IsDefensive);True(battle.TryLink(ally.Id,0,skill.Id,LinkKind.Guard,ally.Id,out var error));}
            battle.ResolveRound();if(battle.State.Outcome==BattleOutcome.Ongoing)battle.BeginNextRound();
        }
        Win(battle);return battle;
    }
    static void Win(BattleEngine battle)
    {
        for(int i=0;i<100&&battle.State.Outcome==BattleOutcome.Ongoing;i++){
            battle.RecommendLinks();True(battle.PlannedCost<=battle.State.Ember);battle.ResolveRound(IgniteGrade.Good);
            if(battle.State.Outcome==BattleOutcome.Ongoing)battle.BeginNextRound();
        }
        Equal(BattleOutcome.Victory,battle.State.Outcome);
    }
    static void FailSave(ProgressService p,ProgressData checkpoint)
    {
        string blocker=Path.Combine(Temp(),"not-a-directory");File.WriteAllText(blocker,"isolated save failure fixture");bool failed=false;
        try{new SaveStore(Path.Combine(blocker,"progress.xml")).Save(p.Data);}catch(IOException){failed=true;p.Restore(checkpoint);}True(failed);
    }
    static DateTime Utc(string value) { return DateTime.Parse(value,null,System.Globalization.DateTimeStyles.AdjustToUniversal|System.Globalization.DateTimeStyles.AssumeUniversal); }
    static string Snapshot(object value) { return JsonSerializer.Serialize(value,new JsonSerializerOptions { IncludeFields=true }); }
    static string Temp() { string path=Path.Combine("Tests","test-output","recurring-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path; }
    static void Test(string name,Action test) { try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception ex){Console.Error.WriteLine("FAIL "+name+"\n"+ex);Environment.Exit(1);} }
    static void True(bool value) { if(!value)throw new Exception("Expected true"); }
    static void False(bool value) { if(value)throw new Exception("Expected false"); }
    static void Equal<T>(T expected,T actual) { if(!Equals(expected,actual))throw new Exception("Expected "+expected+", actual "+actual); }
}
