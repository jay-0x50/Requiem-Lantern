using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RequiemLantern.Core;

internal static class RecruitmentTests
{
    static int passed;
    public static int Run()
    {
        Test("starter collection contains exactly one five-star and three four-stars without inventing growth",()=>{
            var p=Player();Equal(string.Join(",",RecruitmentCatalog.StarterParty),string.Join(",",p.Data.OwnedCharacters));
            Equal("lian,minato,heidel,mare",string.Join(",",p.Data.ActiveParty));False(p.IsOwned("kai"));True(p.IsOwned("mare"));
            Equal(1,p.Data.OwnedCharacters.Count(id=>RecruitmentCatalog.Character(id).Rarity==5));
            Equal(3,p.Data.OwnedCharacters.Count(id=>RecruitmentCatalog.Character(id).Rarity==4));Equal(0,p.Data.Characters.Count);
            string before=Snapshot(p.Data);foreach(var character in RecruitmentCatalog.Characters)p.GetCharacter(character.Id);
            Equal(before,Snapshot(p.Data));var battle=BattleEngine.CreatePrototype(p.Data,0);
            Equal(string.Join(",",p.Data.ActiveParty),string.Join(",",battle.State.Allies.Select(actor=>actor.Id)));Equal(before,Snapshot(p.Data));
            True(battle.State.Allies.All(actor=>actor.Skills.Count(skill=>skill.Number>0)==3&&actor.Skills.Count(BattleRules.IsDefensive)==1));
        });
        Test("ownership rejects direct synchronization and party requests atomically",()=>{
            var p=Player();p.Data.LanternFragments=100;string error;var before=Snapshot(p.Data);
            False(p.IsOwned("sera"));False(p.Synchronize("sera",out error));Equal(before,Snapshot(p.Data));
            foreach(var party in new[]{new[]{"lian","minato","heidel"},new[]{"lian","minato","heidel","sera"},new[]{"lian","lian","minato","heidel"},new[]{"lian","minato","heidel","missing"}})
            {False(p.SetParty(party,out error));Equal(before,Snapshot(p.Data));}
            False(p.EquipCharacter(-1,"lian",out error));False(p.EquipCharacter(4,"lian",out error));False(p.EquipCharacter(0,"noa",out error));Equal(before,Snapshot(p.Data));
            True(p.EquipCharacter(0,p.Data.ActiveParty[1],out error));Equal("minato",p.Data.ActiveParty[0]);Equal("lian",p.Data.ActiveParty[1]);Equal(4,p.Data.ActiveParty.Distinct().Count());
        });
        Test("test coupons normalize spaces and case and redeem each code once",()=>{
            var p=Player();p.Data.Stardust=10;p.Data.Coins=20;CouponReward reward;string error;
            True(p.RedeemCode("  lantern - test - 9999999\t",out reward,out error));Equal(RecruitmentCatalog.TestCurrencyCode,reward.Code);
            Equal(10000009,p.Data.Stardust);Equal(10000019,p.Data.Coins);Equal(1,p.Data.RedeemedCodes.Count);
            var before=Snapshot(p.Data);False(p.RedeemCode("LANTERN-TEST-9999999",out reward,out error));Equal(null,reward);Equal(before,Snapshot(p.Data));
            False(p.RedeemCode("missing",out reward,out error));Equal(before,Snapshot(p.Data));
            True(p.RedeemCode("\nlantern-materials ",out reward,out error));Equal(9999999,p.Data.LanternFragments);Equal(9999999,p.Data.MemoryFragments);Equal(9999999,p.Data.CharacterShards);
            before=Snapshot(p.Data);False(p.RedeemCode("LANTERN-MATERIALS",out reward,out error));Equal(before,Snapshot(p.Data));Equal(2,p.Data.RedeemedCodes.Count);
        });
        Test("coupon overflow rejects every resource grant before changing any field",()=>{
            foreach(string field in new[]{"Coins","Stardust","LanternFragments","MemoryFragments","CharacterShards"}){
                var p=Player();typeof(ProgressData).GetField(field).SetValue(p.Data,int.MaxValue);var before=Snapshot(p.Data);
                string code=field=="Coins"||field=="Stardust"?RecruitmentCatalog.TestCurrencyCode:RecruitmentCatalog.TestMaterialsCode;
                False(p.RedeemCode(code,out var reward,out var error));Equal(null,reward);Equal(before,Snapshot(p.Data));
            }
        });
        Test("five-star rarity and the fifty-fifty use exact probability boundaries",()=>{
            var hit=Player(new Samples(.007999999,.499999,0));True(hit.Recruit("lian",1,out var first,out var error));Equal(5,first.Pulls.Single().Rarity);True(first.Pulls.Single().IsPickup);
            var miss=Player(new Samples(.008,.1,0));True(miss.Recruit("lian",1,out var second,out error));Equal(4,second.Pulls.Single().Rarity);Equal(1,miss.Data.RecruitPity);
            var off=Player(new Samples(0,.5,0));True(off.Recruit("lian",1,out var third,out error));Equal("sera",third.Pulls.Single().CharacterId);False(third.Pulls.Single().IsPickup);True(off.Data.PickupGuaranteed);
        });
        Test("the eightieth pull forces five stars and an early five-star resets pity",()=>{
            var p=Player(new Samples(.99,.99,0,.99,.49,.99));p.Data.RecruitPity=78;
            True(p.Recruit("lian",1,out var first,out var error));Equal(4,first.Pulls.Single().Rarity);Equal(79,p.Data.RecruitPity);
            True(p.Recruit("lian",1,out var second,out error));Equal(5,second.Pulls.Single().Rarity);Equal(0,p.Data.RecruitPity);True(second.Pulls.Single().IsPickup);
            var early=Player(new Samples(.001,0,0));early.Data.RecruitPity=31;True(early.Recruit("sera",1,out var third,out error));Equal(5,third.Pulls.Single().Rarity);Equal(0,early.Data.RecruitPity);
        });
        Test("a lost fifty-fifty guarantees the next five-star across banners",()=>{
            var p=Player(new Samples(0,.99,0,.99,.99,0,0,.99,0));
            True(p.Recruit("lian",1,out var lost,out var error));Equal("sera",lost.Pulls.Single().CharacterId);True(p.Data.PickupGuaranteed);
            True(p.Recruit("noa",1,out var four,out error));Equal(4,four.Pulls.Single().Rarity);True(p.Data.PickupGuaranteed);Equal(1,p.Data.RecruitPity);
            True(p.Recruit("noa",1,out var won,out error));Equal("noa",won.Pulls.Single().CharacterId);True(won.Pulls.Single().IsPickup);False(p.Data.PickupGuaranteed);Equal(0,p.Data.RecruitPity);
        });
        Test("off-banner five-stars and four-stars cover equal-sized pool intervals",()=>{
            var offCounts=new Dictionary<string,int>();
            foreach(double member in new[]{0,.499999999,.5,.999999999}){
                var p=Player(new Samples(0,.99,member));True(p.Recruit("lian",1,out var result,out var error));var pull=result.Pulls.Single();
                False(pull.CharacterId=="lian");offCounts[pull.CharacterId]=offCounts.TryGetValue(pull.CharacterId,out var value)?value+1:1;
            }
            Equal(2,offCounts.Count);Equal(2,offCounts["sera"]);Equal(2,offCounts["noa"]);
            var fourIds=RecruitmentCatalog.Characters.Where(character=>character.Recruitable&&character.Rarity==4).Select(character=>character.Id).ToArray();
            Equal("minato,heidel,mare",string.Join(",",fourIds));False(RecruitmentCatalog.Character("kai").Recruitable);
            for(int index=0;index<fourIds.Length;index++){
                var p=Player(new Samples(.99,.99,(index+.5)/fourIds.Length));True(p.Recruit("sera",1,out var result,out var error));Equal(fourIds[index],result.Pulls.Single().CharacterId);
            }
        });
        Test("a ten-pull applies pity and pickup guarantees after each intermediate result",()=>{
            var rolls=new List<double>{.99,.99,0,.99,.99,0,0,.99,0};for(int index=3;index<10;index++)rolls.AddRange(new[]{.99,.99,0d});
            var p=Player(new Samples(rolls.ToArray()));p.Data.RecruitPity=78;int currency=p.Data.Stardust;
            True(p.Recruit("lian",10,out var result,out var error));Equal(10,result.Pulls.Count);Equal(1600,result.Cost);Equal(currency-1600,p.Data.Stardust);
            Equal(79,result.Pulls[0].PityAfter);Equal("sera",result.Pulls[1].CharacterId);True(result.Pulls[1].GuaranteedAfter);
            Equal("lian",result.Pulls[2].CharacterId);True(result.Pulls[2].IsPickup);False(result.Pulls[2].GuaranteedAfter);
            Equal(7,p.Data.RecruitPity);False(p.Data.PickupGuaranteed);Equal(260,p.Data.CharacterShards);
        });
        Test("duplicates grant only shards and new ownership can immediately enter an equipped party",()=>{
            var p=Player(new Samples(.99,.99,0,0,0,0,0,0,0));string error;
            True(p.Recruit("lian",1,out var four,out error));False(four.Pulls.Single().IsNew);Equal(20,four.Pulls.Single().DuplicateShards);
            True(p.Recruit("lian",1,out var five,out error));False(five.Pulls.Single().IsNew);Equal(100,five.Pulls.Single().DuplicateShards);Equal(120,p.Data.CharacterShards);
            Equal(0,p.GetCharacter("lian").SyncLevel);Equal(0,p.Data.Characters.Count);
            True(p.Recruit("sera",1,out var added,out error));True(added.Pulls.Single().IsNew);Equal(0,added.Pulls.Single().DuplicateShards);True(p.IsOwned("sera"));
            True(p.EquipCharacter(3,"sera",out error));var stage=CampaignCatalog.Get("c1s02");var b=CampaignBattleFactory.Create(stage,p.Data,0);
            Equal("sera",b.State.Allies[3].Id);Equal(4,b.State.Allies.Count);Equal(65,b.State.Enemies[0].MaxHp);Equal(0,b.Unit("sera").SyncLevel);
            b.Restart();Equal("sera",b.State.Allies[3].Id);Equal(0,p.Data.Characters.Count);
        });
        Test("invalid requests insufficient funds shard overflow and invalid RNG never consume state",()=>{
            var samples=new Samples(.99,.99,0);var p=Player(samples);string before=Snapshot(p.Data);RecruitmentResult result;string error;
            False(p.Recruit("missing",1,out result,out error));False(p.Recruit("lian",0,out result,out error));False(p.Recruit("lian",2,out result,out error));Equal(0,samples.Calls);Equal(before,Snapshot(p.Data));
            p.Data.Stardust=159;before=Snapshot(p.Data);False(p.Recruit("lian",1,out result,out error));Equal(before,Snapshot(p.Data));Equal(0,samples.Calls);
            p.Data.Stardust=1600;p.Data.CharacterShards=int.MaxValue;before=Snapshot(p.Data);False(p.Recruit("lian",10,out result,out error));Equal(null,result);Equal(before,Snapshot(p.Data));
            var bad=Player(new Samples(double.NaN));before=Snapshot(bad.Data);False(bad.Recruit("lian",1,out result,out error));Equal(null,result);Equal(before,Snapshot(bad.Data));
        });
        Test("stored recruitment RNG survives save reload and is independent of battles and banner views",()=>{
            string directory=Temp();try{
                var p=Player();p.Data.RecruitRandomState=123456789;True(p.Recruit("lian",1,out var first,out var error));var store=new SaveStore(Path.Combine(directory,"progress.xml"));store.Save(p.Data);
                var loaded=new ProgressService(store.Load());Equal(p.Data.RecruitRandomState,loaded.Data.RecruitRandomState);var before=Snapshot(loaded.Data);
                foreach(var banner in RecruitmentCatalog.Banners)RecruitmentCatalog.Banner(banner.Id);
                BattleEngine.CreatePrototype(loaded.Data,123).Restart();Equal(before,Snapshot(loaded.Data));
                True(p.Recruit("noa",10,out var expected,out error));True(loaded.Recruit("noa",10,out var actual,out error));Equal(Snapshot(expected),Snapshot(actual));Equal(Snapshot(p.Data),Snapshot(loaded.Data));
            }finally{Directory.Delete(directory,true);}
        });
        Test("legacy growth does not unlock characters and migrates once while preserving progress",()=>{
            string directory=Temp();try{
                var path=Path.Combine(directory,"legacy.xml");
                File.WriteAllText(path,"<ProgressData><Version>1</Version><PlayerName>보존</PlayerName><Coins>321</Coins><LanternFragments>12</LanternFragments><MemoryFragments>9</MemoryFragments><KeepLevel>2</KeepLevel><ClearedStages><string>c1s01</string></ClearedStages><Characters><CharacterProgress><Id>sera</Id><Level>7</Level><SyncLevel>1</SyncLevel></CharacterProgress><CharacterProgress><Id>noa</Id><Level>4</Level><SyncLevel>1</SyncLevel></CharacterProgress></Characters></ProgressData>");
                var store=new SaveStore(path);var data=store.Load();var p=new ProgressService(data);Equal(null,store.LastWarning);Equal(321,data.Coins);Equal(12,data.LanternFragments);Equal(9,data.MemoryFragments);Equal(2,data.KeepLevel);
                True(data.ClearedStages.Contains("c1s01"));Equal(2,data.Characters.Count);Equal(7,p.GetCharacter("sera").Level);False(p.IsOwned("sera"));False(p.IsOwned("noa"));Equal(4,data.OwnedCharacters.Count);
                True(p.EquipCharacter(0,data.ActiveParty[1],out var error));data.RecruitPity=79;data.PickupGuaranteed=true;data.RecruitRandomState=987654321;
                string before=Snapshot(data);store.Save(data);Equal(before,Snapshot(store.Load()));
            }finally{Directory.Delete(directory,true);}
        });
        Test("capture and restore roll back a failed save including RNG and allow the same retry",()=>{
            string directory=Temp();try{
                var p=Player();p.Data.RecruitRandomState=23456789;p.Data.RecruitPity=79;p.Data.PickupGuaranteed=true;
                var checkpoint=p.Capture();string before=Snapshot(p.Data);True(p.Recruit("noa",10,out var attempted,out var error));
                File.WriteAllText(Path.Combine(directory,"blocked"),"file");bool rejected=false;
                try{new SaveStore(Path.Combine(directory,"blocked","progress.xml")).Save(p.Data);}catch(IOException){rejected=true;p.Restore(checkpoint);}
                True(rejected);Equal(before,Snapshot(p.Data));True(p.Recruit("noa",10,out var retried,out error));Equal(Snapshot(attempted),Snapshot(retried));
                var isolated=p.Capture();isolated.OwnedCharacters.Clear();True(p.Data.OwnedCharacters.Count>=4);
                p.Restore(checkpoint);checkpoint.ActiveParty.Clear();Equal(4,p.Data.ActiveParty.Count);
            }finally{Directory.Delete(directory,true);}
        });
        Test("version-one collection grants Mare once without removing or reordering any existing record",()=>{
            string directory=Temp();try{
                var legacy=Player().Capture();legacy.CollectionVersion=1;legacy.PlayerName="보존";legacy.PrologueCompleted=true;
                legacy.OwnedCharacters=new List<string>{"lian","minato","heidel","kai","sera"};legacy.ActiveParty=new List<string>{"kai","sera","lian","minato"};
                legacy.Characters.Add(new CharacterProgress{Id="kai",Level=9,SyncLevel=1});legacy.Characters.Add(new CharacterProgress{Id="sera",Level=7,SyncLevel=1});
                legacy.Coins=321;legacy.Stardust=4567;legacy.LanternFragments=12;legacy.MemoryFragments=9;legacy.CharacterShards=234;legacy.KeepLevel=2;
                legacy.RecruitPity=67;legacy.PickupGuaranteed=true;legacy.RecruitRandomState=987654321;legacy.RedeemedCodes.Add(RecruitmentCatalog.TestMaterialsCode);
                legacy.ClearedStages.Add("c1s01");legacy.ClaimedBattles.Add("saved-battle");legacy.RewardedStages.Add("c1s02");
                string path=Path.Combine(directory,"v1.xml");using(var writer=File.Create(path))new System.Xml.Serialization.XmlSerializer(typeof(ProgressData)).Serialize(writer,legacy);
                string originalFile=File.ReadAllText(path);var expected=JsonSerializer.Deserialize<ProgressData>(Snapshot(legacy),new JsonSerializerOptions{IncludeFields=true});
                expected.CollectionVersion=2;expected.OwnedCharacters.Add("mare");
                var store=new SaveStore(path);var migrated=new ProgressService(store.Load());
                Equal(null,store.LastWarning);Equal(originalFile,File.ReadAllText(path));Equal(RecruitmentCatalog.CollectionVersion,migrated.Data.CollectionVersion);
                Equal("lian,minato,heidel,kai,sera,mare",string.Join(",",migrated.Data.OwnedCharacters));Equal("kai,sera,lian,minato",string.Join(",",migrated.Data.ActiveParty));
                Equal(Snapshot(expected),Snapshot(migrated.Data));Equal(9,migrated.GetCharacter("kai").Level);Equal(1,migrated.GetCharacter("kai").SyncLevel);
                var battle=BattleEngine.CreatePrototype(migrated.Data,0);Equal("kai",battle.State.Allies[0].Id);Equal(1,battle.State.Allies[0].SyncLevel);
                string once=Snapshot(migrated.Data);new ProgressService(migrated.Data);Equal(once,Snapshot(migrated.Data));
                store.Save(migrated.Data);Equal(once,Snapshot(store.Load()));Equal(1,migrated.Data.OwnedCharacters.Count(id=>id=="mare"));
            }finally{Directory.Delete(directory,true);}
        });
        Test("version-two collection preserves a custom party and never regrants or duplicates Mare",()=>{
            var p=Player();p.Data.OwnedCharacters.Add("kai");True(p.EquipCharacter(3,"kai",out var error));
            var before=Snapshot(p.Data);new ProgressService(p.Data);Equal(before,Snapshot(p.Data));
            p.Data.OwnedCharacters.Remove("mare");before=Snapshot(p.Data);new ProgressService(p.Data);Equal(before,Snapshot(p.Data));False(p.IsOwned("mare"));
            p.Data.CollectionVersion=1;p.Data.OwnedCharacters.Add("mare");new ProgressService(p.Data);Equal(1,p.Data.OwnedCharacters.Count(id=>id=="mare"));
        });
        Test("Mare has an independent one-action skill kit and synchronization leaves the protagonist separate",()=>{
            var p=Player();var battle=BattleEngine.CreatePrototype(p.Data,123);var mare=battle.Unit("mare");
            Equal("마레",mare.Name);Equal(Element.Flame,mare.Element);Equal(125,mare.MaxHp);Equal("mare",mare.Portrait);Equal(2,mare.SkillHand.Count);
            Equal(3,mare.Skills.Count(skill=>skill.Number>0));True(mare.Skills.Where(skill=>skill.Number>0).All(skill=>skill.Slots==1));
            True(mare.Skills.All(skill=>skill.Id.StartsWith("mare_")));Equal(SkillKind.Evade,mare.Skills.Single(BattleRules.IsDefensive).Kind);
            var third=mare.Skills.Single(skill=>skill.Number==3);False(BattleRules.IsSkillUnlocked(mare,third));p.Data.LanternFragments=10;
            p.Data.SyncMaterials.Add(new SyncMaterialStack { CharacterId="mare",Count=6 });
            True(p.Synchronize("mare",out var error));p.ApplyToBattle(battle.State);True(BattleRules.IsSkillUnlocked(mare,third));False(p.IsOwned("kai"));
        });
        Test("the real starter party clears all twenty-five campaign battles at one fixed seed",()=>{
            var progress=Player().Data;var runs=new List<string>();
            foreach(var stage in CampaignCatalog.Chapters.SelectMany(chapter=>chapter.Stages).Where(stage=>stage.HasBattle)){
                var battle=CampaignBattleFactory.Create(stage,progress,0);Equal(string.Join(",",progress.ActiveParty),string.Join(",",battle.State.Allies.Select(actor=>actor.Id)));
                while(battle.State.Outcome==BattleOutcome.Ongoing&&battle.State.Round<=40){battle.RecommendLinks();battle.ResolveRound(IgniteGrade.Good);if(battle.State.Outcome==BattleOutcome.Ongoing)battle.BeginNextRound();}
                runs.Add(stage.Number+" seed=0 outcome="+battle.State.Outcome+" rounds="+battle.State.Round+" party="+string.Join(",",progress.ActiveParty));Equal(BattleOutcome.Victory,battle.State.Outcome);
            }
            string output=Path.Combine(Directory.GetCurrentDirectory(),"Logs","RecruitmentTests");Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"starter-balance-runs.txt"),runs);Equal(25,runs.Count);
        });
        return passed;
    }
    static ProgressService Player(IRecruitRandom random=null)
    {var p=new ProgressService(new ProgressData(),random);p.Data.Stardust=100000;p.Data.RecruitRandomState=123456789;return p;}
    static string Snapshot<T>(T value){return JsonSerializer.Serialize(value,new JsonSerializerOptions{IncludeFields=true});}
    static string Temp(){var path=Path.Combine(Directory.GetCurrentDirectory(),"Tests","test-output","recruit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    sealed class Samples:IRecruitRandom
    {
        readonly Queue<double> values;public int Calls;
        public Samples(params double[] source){values=new Queue<double>(source);}
        public double NextDouble(ref uint state){Calls++;state++;return values.Count>0?values.Dequeue():.99;}
    }
    static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception error){throw new Exception("Recruitment: "+name,error);}}
    static void True(bool value){if(!value)throw new Exception("Expected true");}
    static void False(bool value){if(value)throw new Exception("Expected false");}
    static void Equal<T>(T expected,T actual){if(!Equals(expected,actual))throw new Exception("Expected "+expected+", actual "+actual);}
}
