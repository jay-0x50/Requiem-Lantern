using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using RequiemLantern.Core;

internal static class SynchronizationMaterialsTests
{
    static int passed;
    static readonly List<string> balanceRuns=new List<string>();
    static void Main()
    {
        Test("seven materials use distinct names, correct portraits and rarity costs",()=>{
            Equal(7,MaterialCatalog.Definitions.Count);Equal(7,MaterialCatalog.Definitions.Select(x=>x.MaterialName).Distinct().Count());
            foreach(var material in MaterialCatalog.Definitions){var character=RecruitmentCatalog.Character(material.CharacterId);True(character!=null);Equal(character.Portrait,material.Portrait);Equal(character.Rarity==5?10:6,material.RequiredCount);Equal(3,material.DropCount);True(!string.IsNullOrWhiteSpace(material.Description));}
            Equal("파수꾼의 심지",MaterialCatalog.Get("lian").MaterialName);Equal("잿빛 맹세",MaterialCatalog.Get("sera").MaterialName);Equal("이름 없는 인장",MaterialCatalog.Get("noa").MaterialName);
            Equal("해류 유리구슬",MaterialCatalog.Get("minato").MaterialName);Equal("서리 렌즈",MaterialCatalog.Get("heidel").MaterialName);Equal("귀항 나침반",MaterialCatalog.Get("kai").MaterialName);Equal("붉은 인양 고리",MaterialCatalog.Get("mare").MaterialName);Equal(null,MaterialCatalog.Get("missing"));
        });
        Test("old synchronized saves retain skill three without materials or extra charges",()=>{
            var p=Player();p.Data.LanternFragments=123;p.Data.Characters.Add(new CharacterProgress { Id="lian",Level=8,SyncLevel=1 });p.Data.RecruitPity=66;p.Data.PickupGuaranteed=true;
            var path=Path.Combine(Temp(),"old.xml");new SaveStore(path).Save(p.Data);var xml=XDocument.Load(path);xml.Root.Element("SyncMaterials").Remove();xml.Save(path);
            var store=new SaveStore(path);p=new ProgressService(store.Load());Equal(null,store.LastWarning);Equal(0,p.Data.SyncMaterials.Count);Equal(1,p.GetCharacter("lian").SyncLevel);Equal(8,p.GetCharacter("lian").Level);Equal(123,p.Data.LanternFragments);Equal(66,p.Data.RecruitPity);True(p.Data.PickupGuaranteed);
            var battle=BattleEngine.CreatePrototype(p.Data,0);True(BattleRules.IsSkillUnlocked(battle.Unit("lian"),battle.Skill("lian","lian_crescendo")));
            string before=Snapshot(p.Data);False(p.Synchronize("lian",out var error));Equal(before,Snapshot(p.Data));
        });
        Test("material snapshots never grant ownership, growth or inventory by viewing",()=>{
            var p=Player();string before=Snapshot(p.Data);var view=p.GetSynchronizationMaterials();Equal(7,view.Entries.Count);False(view.Unlocked);Equal(before,Snapshot(p.Data));
            foreach(var entry in view.Entries){Equal(0,entry.Count);False(entry.Synchronized);False(entry.CanSynchronize);Equal(p.IsOwned(entry.CharacterId),entry.Owned);}
            var json=JsonSerializer.Serialize(view,new JsonSerializerOptions { IncludeFields=true });True(json.Contains("MaterialName"));True(json.Contains("RequiredCount"));
            view.Entries.Clear();Equal(7,p.GetSynchronizationMaterials().Entries.Count);
        });
        Test("common and matching dedicated requirements reject atomically at exact boundaries",()=>{
            var p=Player();p.Data.LanternFragments=10;Give(p,"lian",9);Give(p,"sera",999);
            string before=Snapshot(p.Data);False(p.Synchronize("lian",out var error));True(error.Contains("파수꾼의 심지"));Equal(before,Snapshot(p.Data));
            Give(p,"lian",1);p.Data.LanternFragments=9;before=Snapshot(p.Data);False(p.Synchronize("lian",out error));Equal(before,Snapshot(p.Data));
            p.Data.LanternFragments=10;True(p.GetSynchronizationMaterials().Entries.Single(x=>x.CharacterId=="lian").CanSynchronize);True(p.Synchronize("lian",out error));Equal(0,p.Data.LanternFragments);Equal(0,Count(p,"lian"));Equal(999,Count(p,"sera"));Equal(1,p.GetCharacter("lian").SyncLevel);Equal(1,p.GetCharacter("lian").Level);
            before=Snapshot(p.Data);False(p.Synchronize("lian",out error));Equal(before,Snapshot(p.Data));
        });
        Test("every owned character has an independent recipe and skill three unlock",()=>{
            var p=Player();p.Data.OwnedCharacters=RecruitmentCatalog.Characters.Select(x=>x.Id).ToList();p.Data.LanternFragments=70;
            foreach(var material in MaterialCatalog.Definitions)Give(p,material.CharacterId,material.RequiredCount+2);
            foreach(var material in MaterialCatalog.Definitions){True(p.Synchronize(material.CharacterId,out var error));Equal(2,Count(p,material.CharacterId));var unit=PrototypeData.CreateCharacter(material.CharacterId,p.GetCharacter(material.CharacterId).SyncLevel);True(BattleRules.IsSkillUnlocked(unit,unit.Skills.Single(x=>x.Number==3)));True(unit.Skills.Where(x=>x.Number==3).All(x=>x.Slots==1));}
            Equal(0,p.Data.LanternFragments);True(p.GetSynchronizationMaterials().Entries.All(x=>x.Synchronized&&!x.CanSynchronize));
        });
        Test("unowned characters may be farmed ahead but cannot synchronize until obtained",()=>{
            var p=Unlocked();p.Data.LanternFragments=30;False(p.IsOwned("sera"));var session=new MaterialDungeonSession();
            for(int i=0;i<4;i++){True(session.Enter("sera",p,out var error));Win(session.Battle);True(session.ClaimVictory(p,out var reward,out error));Equal("sera",reward.CharacterId);Equal(3,reward.Count);session.Exit();}
            Equal(12,Count(p,"sera"));string before=Snapshot(p.Data);False(p.Synchronize("sera",out var why));Equal(before,Snapshot(p.Data));False(p.GetSynchronizationMaterials().Entries.Single(x=>x.CharacterId=="sera").CanSynchronize);
            p.Data.OwnedCharacters.Add("sera");True(p.Synchronize("sera",out why));Equal(2,Count(p,"sera"));Equal(20,p.Data.LanternFragments);
        });
        Test("material dungeon unlock waits for 1-2 after-story, not just its battle reward",()=>{
            var p=Player();var session=new MaterialDungeonSession();string before=Snapshot(p.Data);False(session.Enter("lian",p,out var error));Equal(before,Snapshot(p.Data));
            p.Data.RewardedStages.Add("c1s02");False(p.GetSynchronizationMaterials().Unlocked);False(session.Enter("lian",p,out error));CampaignRules.Complete(p.Data,CampaignCatalog.Get("c1s02"));True(p.GetSynchronizationMaterials().Unlocked);True(session.Enter("lian",p,out error));
            before=Snapshot(p.Data);False(session.Enter("mare",p,out error));Equal(before,Snapshot(p.Data));session.Exit();False(session.Enter("missing",p,out error));Equal(before,Snapshot(p.Data));
        });
        Test("all seven routes really win with unsynchronized starter party for three seeds",()=>{
            var p=Unlocked();foreach(var material in MaterialCatalog.Definitions)foreach(int seed in new[]{0,1,42}){
                var battle=MaterialDungeonSession.Create(material,p.Data,seed);Equal("material_"+material.CharacterId,battle.State.StageId);Equal(2,battle.State.Enemies.Count);False(battle.State.IsBoss);
                Equal("lian,minato,heidel,mare",string.Join(",",battle.State.Allies.Select(x=>x.Id)));True(battle.State.Allies.All(x=>x.SyncLevel==0&&x.SkillHand.Count==2));Equal(0,battle.State.CoinReward);Equal(0,battle.State.FirstLanternReward);Equal(0,battle.State.MemoryReward);
                Win(battle);True(battle.State.Allies.All(x=>x.SlotCount>=1&&x.SlotCount<=2));
                balanceRuns.Add(material.CharacterId+" seed="+seed+" result="+battle.State.Outcome+" rounds="+battle.State.Round+" clashes="+battle.ResolvedClashes);
            }
            Equal(21,balanceRuns.Count);
        });
        Test("victory grants only three selected materials and records real quest actions once",()=>{
            var p=Unlocked();p.GetActivities();p.Data.Activities.WeeklyDungeonRewardsClaimed=2;var session=new MaterialDungeonSession();True(session.Enter("mare",p,out var error));
            DefendRound(session.Battle);Win(session.Battle);var money=Wallet(p);string clears=string.Join(",",p.Data.ClearedStages),rewards=string.Join(",",p.Data.RewardedStages);
            False(p.ClaimVictory(session.Battle,out var normal));Equal(0,Count(p,"mare"));True(session.ClaimVictory(p,out var reward,out error));Equal(3,Count(p,"mare"));Equal(money,Wallet(p));
            Equal(1,p.Data.Activities.DailyVictories);Equal(Math.Min(3,session.Battle.ResolvedClashes),p.Data.Activities.DailyClashes);Equal(2,p.Data.Activities.DailyDefenses);Equal(2,p.Data.Activities.WeeklyDungeonRewardsClaimed);
            Equal(clears,string.Join(",",p.Data.ClearedStages));Equal(rewards,string.Join(",",p.Data.RewardedStages));string before=Snapshot(p.Data);False(session.ClaimVictory(p,out reward,out error));Equal(before,Snapshot(p.Data));
        });
        Test("material farming has no entry cost or daily and weekly quota",()=>{
            var p=Unlocked();p.GetActivities();p.Data.Activities.WeeklyDungeonRewardsClaimed=3;var session=new MaterialDungeonSession();var money=Wallet(p);
            for(int i=0;i<5;i++){True(session.Enter("lian",p,out var error));Win(session.Battle);True(session.ClaimVictory(p,out var reward,out error));session.Exit();}
            Equal(15,Count(p,"lian"));Equal(3,p.Data.Activities.WeeklyDungeonRewardsClaimed);Equal(5,p.Data.Activities.WeeklyVictories);Equal(money,Wallet(p));
        });
        Test("defeat and retreat award no materials or quest progress; restart preserves the selected recipe",()=>{
            var p=Unlocked();p.GetActivities();var session=new MaterialDungeonSession();True(session.Enter("noa",p,out var error));string first=session.Battle.State.BattleId;
            foreach(var ally in session.Battle.State.Allies)ally.Hp=0;session.Battle.ResolveRound();Equal(BattleOutcome.Defeat,session.Battle.State.Outcome);False(session.ClaimVictory(p,out var reward,out error));Equal(0,Count(p,"noa"));Equal(0,p.Data.Activities.WeeklyVictories);
            session.Restart(p);True(first!=session.Battle.State.BattleId);Equal("noa",session.Active.CharacterId);Equal(BattleOutcome.Ongoing,session.Battle.State.Outcome);Equal(0,session.Battle.ResolvedClashes);session.Exit();Equal(null,session.Active);Equal(0,p.Data.SyncMaterials.Count);
        });
        Test("material reward overflow keeps the same victory claimable without any partial progress",()=>{
            var p=Unlocked();p.GetActivities();Give(p,"lian",int.MaxValue-2);var session=new MaterialDungeonSession();session.Enter("lian",p,out var error);Win(session.Battle);string before=Snapshot(p.Data);
            False(session.ClaimVictory(p,out var reward,out error));True(error.Contains("한도"));Equal(before,Snapshot(p.Data));p.Data.SyncMaterials.Single().Count=int.MaxValue-3;
            True(session.ClaimVictory(p,out reward,out error));Equal(int.MaxValue,Count(p,"lian"));Equal(1,p.Data.Activities.WeeklyVictories);
        });
        Test("failed reward and synchronization persistence fully roll back and can retry once",()=>{
            var p=Unlocked();p.Data.LanternFragments=10;Give(p,"mare",3);var session=new MaterialDungeonSession();session.Enter("mare",p,out var error);Win(session.Battle);var checkpoint=p.Capture();string before=Snapshot(p.Data);
            True(session.ClaimVictory(p,out var reward,out error));FailSave(p,checkpoint);Equal(before,Snapshot(p.Data));True(session.Active!=null);True(session.ClaimVictory(p,out reward,out error));Equal(6,Count(p,"mare"));
            checkpoint=p.Capture();before=Snapshot(p.Data);True(p.Synchronize("mare",out error));FailSave(p,checkpoint);Equal(before,Snapshot(p.Data));True(p.Synchronize("mare",out error));Equal(0,Count(p,"mare"));Equal(0,p.Data.LanternFragments);Equal(1,p.GetCharacter("mare").SyncLevel);
        });
        Test("new material coupon is independent from both old coupons and rewards all seven stacks",()=>{
            var p=Player();True(p.RedeemCode(RecruitmentCatalog.TestCurrencyCode,out var reward,out var error));Equal(null,reward.SyncMaterials);True(p.RedeemCode(RecruitmentCatalog.TestMaterialsCode,out reward,out error));Equal(null,reward.SyncMaterials);
            var money=Wallet(p);True(p.RedeemCode(" lantern-sync- materials \n",out reward,out error));Equal(MaterialCatalog.TestCouponCode,reward.Code);Equal(7,reward.SyncMaterials.Count);Equal(money,Wallet(p));Equal(3,p.Data.RedeemedCodes.Count);
            foreach(var material in MaterialCatalog.Definitions)Equal(RecruitmentCatalog.TestAmount,Count(p,material.CharacterId));reward.SyncMaterials[0].Count=0;Equal(RecruitmentCatalog.TestAmount,Count(p,"lian"));
            string before=Snapshot(p.Data);False(p.RedeemCode(MaterialCatalog.TestCouponCode,out reward,out error));Equal(before,Snapshot(p.Data));
        });
        Test("any overflowing coupon stack prevents every material and the code from being consumed",()=>{
            foreach(var material in MaterialCatalog.Definitions){var p=Player();Give(p,material.CharacterId,int.MaxValue);string before=Snapshot(p.Data);False(p.RedeemCode(MaterialCatalog.TestCouponCode,out var reward,out var error));True(error.Contains("한도"));Equal(before,Snapshot(p.Data));}
        });
        Test("failed material coupon persistence rolls back all seven stacks and redeemed status",()=>{
            var p=Player();var checkpoint=p.Capture();string before=Snapshot(p.Data);True(p.RedeemCode(MaterialCatalog.TestCouponCode,out var reward,out var error));FailSave(p,checkpoint);Equal(before,Snapshot(p.Data));True(p.RedeemCode(MaterialCatalog.TestCouponCode,out reward,out error));Equal(7,p.Data.SyncMaterials.Count);Equal(1,p.Data.RedeemedCodes.Count);
        });
        Test("inventory, synchronization and all coupon codes survive save and detached snapshots",()=>{
            var p=Unlocked();p.RedeemCode(RecruitmentCatalog.TestCurrencyCode,out var reward,out var error);p.RedeemCode(RecruitmentCatalog.TestMaterialsCode,out reward,out error);p.RedeemCode(MaterialCatalog.TestCouponCode,out reward,out error);True(p.Synchronize("mare",out error));
            var store=new SaveStore(Path.Combine(Temp(),"progress.xml"));store.Save(p.Data);var loaded=store.Load();Equal(null,store.LastWarning);Equal(Snapshot(p.Data),Snapshot(loaded));
            var checkpoint=p.Capture();checkpoint.SyncMaterials[0].Count=7;True(Count(p,"lian")!=7);p.Restore(checkpoint);checkpoint.SyncMaterials[0].Count=8;Equal(7,Count(p,"lian"));
        });
        Test("negative duplicate unknown and null inventory entries are rejected before replacing a good save",()=>{
            var p=Player();Give(p,"lian",3);var path=Path.Combine(Temp(),"progress.xml");var store=new SaveStore(path);store.Save(p.Data);string good=File.ReadAllText(path);
            foreach(Action<ProgressData> corrupt in new Action<ProgressData>[] { d=>d.SyncMaterials[0].Count=-1,d=>d.SyncMaterials.Add(new SyncMaterialStack { CharacterId="lian",Count=1 }),d=>d.SyncMaterials.Add(new SyncMaterialStack { CharacterId="missing",Count=1 }),d=>d.SyncMaterials.Add(null) }){
                var bad=p.Capture();corrupt(bad);bool failed=false;try{store.Save(bad);}catch(InvalidOperationException){failed=true;}True(failed);Equal(good,File.ReadAllText(path));
            }
        });
        string output=Path.Combine("QA","SynchronizationMaterials","2026-10-16");Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"core-route-balance-runs.txt"),balanceRuns);
        Console.WriteLine("Synchronization materials: "+passed+" checks passed; "+balanceRuns.Count+" deterministic material dungeon wins.");
    }
    static ProgressService Player() { return new ProgressService(new ProgressData(),null,()=>new DateTime(2026,10,16,0,0,0,DateTimeKind.Utc)); }
    static ProgressService Unlocked() { var p=Player();CampaignRules.Complete(p.Data,CampaignCatalog.Get("c1s02"));return p; }
    static int Count(ProgressService p,string id) { return p.GetSynchronizationMaterials().Entries.Single(x=>x.CharacterId==id).Count; }
    static void Give(ProgressService p,string id,int count)
    { var stack=p.Data.SyncMaterials.FirstOrDefault(x=>x.CharacterId==id);if(stack==null)p.Data.SyncMaterials.Add(new SyncMaterialStack { CharacterId=id,Count=count });else stack.Count+=count; }
    static string Wallet(ProgressService p) { return string.Join(",",p.Data.Stardust,p.Data.Coins,p.Data.LanternFragments,p.Data.MemoryFragments,p.Data.CharacterShards); }
    static void DefendRound(BattleEngine battle)
    { foreach(var ally in battle.State.Allies){var defense=ally.Skills.Single(BattleRules.IsDefensive);True(battle.TryLink(ally.Id,0,defense.Id,LinkKind.Guard,ally.Id,out var error));}battle.ResolveRound();if(battle.State.Outcome==BattleOutcome.Ongoing)battle.BeginNextRound(); }
    static void Win(BattleEngine battle)
    {
        for(int i=0;i<100&&battle.State.Outcome==BattleOutcome.Ongoing;i++){battle.RecommendLinks();True(battle.PlannedCost<=battle.State.Ember);battle.ResolveRound(IgniteGrade.Good);if(battle.State.Outcome==BattleOutcome.Ongoing)battle.BeginNextRound();}
        Equal(BattleOutcome.Victory,battle.State.Outcome);
    }
    static void FailSave(ProgressService p,ProgressData checkpoint)
    { string blocker=Path.Combine(Temp(),"not-a-directory");File.WriteAllText(blocker,"isolated write failure");bool failed=false;try{new SaveStore(Path.Combine(blocker,"progress.xml")).Save(p.Data);}catch(IOException){failed=true;p.Restore(checkpoint);}True(failed); }
    static string Snapshot(object value) { return JsonSerializer.Serialize(value,new JsonSerializerOptions { IncludeFields=true }); }
    static string Temp() { string path=Path.Combine("Tests","test-output","sync-materials-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path; }
    static void Test(string name,Action test) { try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception ex){Console.Error.WriteLine("FAIL "+name+"\n"+ex);Environment.Exit(1);} }
    static void True(bool value) { if(!value)throw new Exception("Expected true"); }
    static void False(bool value) { if(value)throw new Exception("Expected false"); }
    static void Equal<T>(T expected,T actual) { if(!Equals(expected,actual))throw new Exception("Expected "+expected+", actual "+actual); }
}
