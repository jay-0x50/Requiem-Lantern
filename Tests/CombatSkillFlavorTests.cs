using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RequiemLantern.Core;

internal static class CombatSkillFlavorTests
{
    static int passed;
    sealed class Samples:IBattleRandom
    { public int Calls;readonly Queue<double> values;public Samples(params double[] source){values=new Queue<double>(source);}public double NextDouble(){Calls++;return values.Count>0?values.Dequeue():.99;} }
    static void Main()
    {
        Test("all seven kits have two short normal variants, unique S3 cues and three awakening definitions",()=>{
            foreach(string id in Ids()){
                var unit=PrototypeData.CreateCharacter(id);Equal(id=="lian"||id=="sera"||id=="noa"?3:0,unit.AwakeningMaxStacks);Equal(0,unit.AwakeningStacks);Equal(0,unit.Barrier);Equal(0,unit.SupportPower);
                foreach(int number in new[]{0,1,2}){var a=CombatSkillFlavor.Quote(id,number,false,0);var b=CombatSkillFlavor.Quote(id,number,false,1);True(a.Length>0&&a!=b);True(a.Length<40&&b.Length<40);}
                var third=unit.Skills.Single(x=>x.Number==3);Equal(id+"_s3",third.AnimationKey);True(!string.IsNullOrWhiteSpace(third.Quote));
                if(unit.AwakeningMaxStacks>0){True(unit.AwakeningDescription.Contains("소모"));True(!string.IsNullOrWhiteSpace(third.AwakenedName));True(CombatSkillFlavor.Quote(id,3,true,0)!=third.Quote);}
            }
        });
        Test("one valid skill emits SkillStart before ignition and all contact metadata matches",()=>{
            var battle=Fixture("sera");NoIntents(battle);Force(battle,"sera",1);Link(battle,"sera",1,LinkKind.Strike,"enemy_arm");battle.ResolveRound();
            var events=battle.State.Events;var start=events.Single(e=>e.Type=="SkillStart");Equal("enemy",start.TargetId);Equal("sera_s1",start.AnimationKey);True(!string.IsNullOrWhiteSpace(start.Quote));
            Equal("enemy_arm",events.Single(e=>e.Type=="Ignite").TargetId);
            True(events.IndexOf(start)<events.FindIndex(e=>e.Type=="Ignite"));True(events.IndexOf(start)<events.FindIndex(e=>e.Type=="FlameRoll"));
            foreach(var e in events.Where(e=>e.ActorId=="sera"&&(e.Type=="Damage"||e.Type=="FlameRoll"))){Equal(start.Quote,e.Quote);Equal(start.AnimationKey,e.AnimationKey);Equal(start.Awakened,e.Awakened);Equal(start.SkillId,e.SkillId);}
            battle=Fixture("lian");EasyIntent(battle);Force(battle,"lian",1);Link(battle,"lian",1,LinkKind.Clash,"forecast");battle.ResolveRound();
            Equal("enemy",battle.State.Events.Single(e=>e.Type=="SkillStart").TargetId);var ignite=battle.State.Events.Single(e=>e.Type=="Ignite");Equal("forecast",ignite.TargetId);Equal("forecast",ignite.IntentId);Equal("lian",ignite.OriginalTargetId);
        });
        Test("quote alternation is engine-local and never consumes combat randomness",()=>{
            var samples=new Samples();var battle=Fixture("mare",samples);var quotes=new List<string>();
            for(int i=0;i<3;i++){NoIntents(battle);Force(battle,"mare",1);Link(battle,"mare",1,LinkKind.Strike,"enemy");battle.ResolveRound();quotes.Add(battle.State.Events.Single(e=>e.Type=="SkillStart").Quote);battle.BeginNextRound();}
            True(quotes[0]!=quotes[1]);Equal(quotes[0],quotes[2]);Equal(6,samples.Calls);
        });
        Test("cancelled or dead targets produce no start quote and consume no awakening or support buff",()=>{
            var battle=Fixture("lian");var actor=battle.Unit("lian");actor.AwakeningStacks=3;actor.SupportPower=3;Force(battle,"lian",3);Link(battle,"lian",3,LinkKind.Strike,"enemy");battle.Unit("enemy").Hp=0;battle.ResolveRound();
            Equal(3,actor.AwakeningStacks);Equal(3,actor.SupportPower);False(battle.State.Events.Any(e=>e.Type=="SkillStart"||e.Type=="AwakeningUsed"));
            battle=Fixture("lian");actor=battle.Unit("lian");actor.AwakeningStacks=3;Force(battle,"lian",3);Link(battle,"lian",3,LinkKind.Support,"friend");battle.Unit("friend").Hp=0;battle.ResolveRound();Equal(3,actor.AwakeningStacks);False(battle.State.Events.Any(e=>e.Type=="SkillStart"&&e.ActorId=="lian"));
        });
        Test("illusion consumes the offered action but no quote, awakening or next attack buff",()=>{
            var samples=new Samples();var battle=Fixture("noa",samples);var actor=battle.Unit("noa");actor.AwakeningStacks=3;actor.SupportPower=3;battle.State.Intents[0].Kind=IntentKind.Illusion;
            Force(battle,"noa",3);Link(battle,"noa",3,LinkKind.Clash,"forecast");battle.ResolveRound();Equal(3,actor.AwakeningStacks);Equal(3,actor.SupportPower);Equal(0,battle.ResolvedClashes);Equal(0,samples.Calls);
            False(battle.State.Events.Any(e=>e.Type=="SkillStart"||e.Type=="AwakeningUsed"));True(battle.State.Events.Any(e=>e.Type=="Illusion"));Equal(1,actor.DiscardPile.Count);
        });
        Test("Lian guards and real clash wins charge once with one ready event at three",()=>{
            var battle=Fixture("lian");var actor=battle.Unit("lian");
            for(int i=0;i<4;i++){NoIntents(battle);Guard(battle,"lian");battle.ResolveRound();Equal(Math.Min(3,i+1),actor.AwakeningStacks);Equal(i==2?1:0,battle.State.Events.Count(e=>e.Type=="AwakeningReady"));battle.BeginNextRound();}
            battle=Fixture("lian");actor=battle.Unit("lian");EasyIntent(battle);Force(battle,"lian",1);Link(battle,"lian",1,LinkKind.Clash,"forecast");battle.ResolveRound();Equal(1,actor.AwakeningStacks);Equal(1,battle.ResolvedClashes);
        });
        Test("Sera charges on actual HP loss only and never on a shield-only hit",()=>{
            var battle=Fixture("sera");NoIntents(battle);battle.Unit("enemy").Barrier=40;Force(battle,"sera",1);Link(battle,"sera",1,LinkKind.Strike,"enemy");battle.ResolveRound();Equal(0,battle.Unit("sera").AwakeningStacks);Equal(0,battle.State.Events.Single(e=>e.Type=="Damage").Amount);
            battle.BeginNextRound();NoIntents(battle);battle.Unit("enemy").Barrier=0;Force(battle,"sera",1);Link(battle,"sera",1,LinkKind.Strike,"enemy");battle.ResolveRound();Equal(1,battle.Unit("sera").AwakeningStacks);
        });
        Test("Noa charges only on winning a real clash and a stalemate gives none",()=>{
            var battle=Fixture("noa");NoIntents(battle);Force(battle,"noa",1);Link(battle,"noa",1,LinkKind.Strike,"enemy");battle.ResolveRound();Equal(0,battle.Unit("noa").AwakeningStacks);
            battle=Fixture("noa");EasyIntent(battle);Force(battle,"noa",1);Link(battle,"noa",1,LinkKind.Clash,"forecast");battle.ResolveRound();Equal(1,battle.Unit("noa").AwakeningStacks);
            battle=Fixture("noa");var actor=battle.Unit("noa");var skill=actor.Skills.Single(s=>s.Number==1);skill.Element=Element.Radiance;skill.BasePower=10;skill.FlamePower=0;skill.FlameCount=1;var intent=battle.State.Intents[0];intent.BasePower=10;intent.FlamePower=0;intent.FlameCount=1;
            Force(battle,"noa",1);Link(battle,"noa",1,LinkKind.Clash,"forecast");battle.ResolveRound();Equal(0,actor.AwakeningStacks);True(battle.State.Events.Any(e=>e.Type=="Stalemate"));
        });
        Test("awakened S3 consumes exactly three, boosts only its action copy and cannot recharge itself",()=>{
            foreach(string id in new[]{"lian","sera","noa"}){
                var battle=Fixture(id);NoIntents(battle);var actor=battle.Unit(id);var original=actor.Skills.Single(s=>s.Number==3);int basis=original.BasePower,flame=original.FlamePower;actor.AwakeningStacks=3;actor.SupportPower=3;
                Force(battle,id,3);Link(battle,id,3,LinkKind.Strike,"enemy");battle.ResolveRound();var start=battle.State.Events.Single(e=>e.Type=="SkillStart");True(start.Awakened);Equal(id+"_s3_awakened",start.AnimationKey);Equal(basis+7,start.BasePower);Equal(flame+1,start.FlamePower);Equal(0,actor.AwakeningStacks);Equal(0,actor.SupportPower);Equal(0,start.AwakeningStacks);Equal(0,start.SupportPower);
                Equal(basis,original.BasePower);Equal(flame,original.FlamePower);False(original.Awakened);Equal(1,battle.State.Events.Count(e=>e.Type=="AwakeningUsed"));False(battle.State.Events.Any(e=>e.Type=="AwakeningCharge"));
            }
        });
        Test("awakening is checked at execution after an earlier guard in the same round",()=>{
            var battle=Fixture("lian");NoIntents(battle);battle.State.Round=2;BattleRules.ApplyActionProgression(battle.State);var actor=battle.Unit("lian");actor.AwakeningStacks=2;Force(battle,"lian",3);Guard(battle,"lian");Link(battle,"lian",3,LinkKind.Strike,"enemy",1);battle.ResolveRound();
            var starts=battle.State.Events.Where(e=>e.Type=="SkillStart").ToList();Equal(2,starts.Count);False(starts[0].Awakened);True(starts[1].Awakened);Equal(0,actor.AwakeningStacks);Equal(1,battle.State.Events.Count(e=>e.Type=="AwakeningUsed"));
        });
        Test("an unawakened S3 reaching three stacks during its own clash does not transform midway",()=>{
            var battle=Fixture("noa");EasyIntent(battle);var actor=battle.Unit("noa");actor.AwakeningStacks=2;Force(battle,"noa",3);Link(battle,"noa",3,LinkKind.Clash,"forecast");battle.ResolveRound();Equal(3,actor.AwakeningStacks);False(battle.State.Events.Single(e=>e.Type=="SkillStart").Awakened);False(battle.State.Events.Any(e=>e.Type=="AwakeningUsed"));
        });
        Test("losing an awakened S3 clash still spends its stacks and the next attack buff once",()=>{
            var battle=Fixture("noa");var actor=battle.Unit("noa");actor.AwakeningStacks=3;actor.SupportPower=3;var intent=battle.State.Intents[0];intent.BasePower=100;Force(battle,"noa",3);Link(battle,"noa",3,LinkKind.Clash,"forecast");battle.ResolveRound();
            Equal(0,actor.AwakeningStacks);Equal(0,actor.SupportPower);True(battle.State.Events.Any(e=>e.Type=="ClashWin"&&e.ActorId=="enemy"));Equal(1,battle.State.Events.Count(e=>e.Type=="SkillStart"));
        });
        Test("a persistent next attack bonus applies to every contest and is spent once for the action",()=>{
            var samples=new Samples(.99,.99,0,.99,.99,.99,0,.99,.99,.99,0,.99);var battle=Fixture("noa",samples);var actor=battle.Unit("noa");actor.SupportPower=3;var skill=actor.Skills.Single(s=>s.Number==1);skill.BasePower=5;skill.FlamePower=10;skill.FlameCount=1;var intent=battle.State.Intents[0];intent.BasePower=10;intent.FlamePower=1;intent.FlameCount=1;
            Force(battle,"noa",1);Link(battle,"noa",1,LinkKind.Clash,"forecast");battle.ResolveRound();var rolls=battle.State.Events.Where(e=>e.Type=="FlameRoll"&&e.ActorId=="noa").ToList();True(rolls.Count>=2);True(rolls.All(e=>e.BasePower==8));Equal(0,actor.SupportPower);Equal(1,battle.State.Events.Count(e=>e.Type=="SkillStart"));
        });
        Test("Lian S3 can support an ally with fixed normal and awakened barrier values without damage",()=>{
            foreach(bool awakened in new[]{false,true}){
                var battle=Fixture("lian");NoIntents(battle);var actor=battle.Unit("lian");actor.AwakeningStacks=awakened?3:0;actor.SupportPower=3;Force(battle,"lian",3);Link(battle,"lian",3,LinkKind.Support,"friend");battle.ResolveRound();
                var friend=battle.Unit("friend");Equal(awakened?30:18,friend.Barrier);Equal(awakened?4:2,friend.Sanity);Equal(3,actor.SupportPower);Equal(0,actor.AwakeningStacks);False(battle.State.Events.Any(e=>e.Type=="Damage"));Equal(1,battle.State.Events.Count(e=>e.Type=="SkillStart"));Equal(1,battle.State.Events.Count(e=>e.Type=="SupportApplied"));Equal(awakened,battle.State.Events.Single(e=>e.Type=="SupportApplied").Awakened);
            }
        });
        Test("Kai S2 supports a teammate or himself while retaining the attack branch",()=>{
            foreach(string target in new[]{"friend","kai","enemy"}){
                var battle=Fixture("kai");NoIntents(battle);Force(battle,"kai",2);Link(battle,"kai",2,target=="enemy"?LinkKind.Strike:LinkKind.Support,target);battle.ResolveRound();
                if(target=="enemy")True(battle.State.Events.Any(e=>e.Type=="Damage"&&e.TargetId==target));else{Equal(3,battle.Unit(target).SupportPower);Equal(2,battle.Unit(target).Sanity);False(battle.State.Events.Any(e=>e.Type=="Damage"));}
            }
        });
        Test("Minato healing carries sanity and S3 barrier with matching dialogue metadata",()=>{
            foreach(int number in new[]{2,3}){
                var battle=Fixture("minato");NoIntents(battle);var actor=battle.Unit("minato");actor.SupportPower=3;var friend=battle.Unit("friend");friend.Hp=50;Force(battle,"minato",number);Link(battle,"minato",number,LinkKind.Support,"friend");battle.ResolveRound();
                True(friend.Hp>50);Equal(number==2?2:3,friend.Sanity);Equal(number==2?0:12,friend.Barrier);Equal(3,actor.SupportPower);var start=battle.State.Events.Single(e=>e.Type=="SkillStart");Equal(start.Quote,battle.State.Events.Single(e=>e.Type=="Heal").Quote);Equal(start.AnimationKey,battle.State.Events.Single(e=>e.Type=="SupportApplied").AnimationKey);
            }
        });
        Test("barriers refresh by maximum, clamp to forty and persist through rounds",()=>{
            var battle=Fixture("lian");var actor=battle.Unit("lian");var third=actor.Skills.Single(s=>s.Number==3);var friend=battle.Unit("friend");
            foreach(int amount in new[]{18,12,80}){NoIntents(battle);third.SupportShield=amount;Force(battle,"lian",3);Link(battle,"lian",3,LinkKind.Support,"friend");battle.ResolveRound();Equal(amount==80?40:18,friend.Barrier);battle.BeginNextRound();Equal(amount==80?40:18,friend.Barrier);}
        });
        Test("shield absorption occurs after guard mitigation and emits one zero-damage contact",()=>{
            var battle=Fixture("lian");var actor=battle.Unit("lian");actor.Barrier=40;actor.SupportPower=3;Guard(battle,"lian");battle.ResolveRound();var hit=battle.State.Events.Single(e=>e.Type=="Damage"&&e.TargetId=="lian");var shield=battle.State.Events.Single(e=>e.Type=="ShieldAbsorb");Equal(0,hit.Amount);Equal(500,actor.Hp);True(shield.Amount>0&&shield.Amount<20);Equal(40-shield.Amount,actor.Barrier);Equal(actor.Barrier,hit.Barrier);Equal(3,shield.SupportPower);Equal(3,actor.SupportPower);True(battle.State.Events.IndexOf(shield)<battle.State.Events.IndexOf(hit));
        });
        Test("a larger hit depletes the barrier then damages HP by only the remainder",()=>{
            var battle=Fixture("mare");var actor=battle.Unit("mare");actor.Barrier=5;battle.ResolveRound();var absorbed=battle.State.Events.Single(e=>e.Type=="ShieldAbsorb");var damage=battle.State.Events.Single(e=>e.Type=="Damage");Equal(5,absorbed.Amount);Equal(0,actor.Barrier);True(damage.Amount>0);Equal(500-damage.Amount,actor.Hp);
        });
        Test("allied support validates offered cards slots synchronization costs and living allies atomically",()=>{
            var battle=Fixture("lian");var actor=battle.Unit("lian");Force(battle,"lian",1);string before=Snapshot(battle.State);False(battle.TryLink("lian",0,"lian_glimmer",LinkKind.Support,"friend",out var error));Equal(before,Snapshot(battle.State));
            Force(battle,"lian",3);actor.SyncLevel=0;before=Snapshot(battle.State);False(battle.TryLink("lian",0,"lian_crescendo",LinkKind.Support,"friend",out error));Equal(before,Snapshot(battle.State));actor.SyncLevel=1;battle.State.Ember=2;before=Snapshot(battle.State);False(battle.TryLink("lian",0,"lian_crescendo",LinkKind.Support,"friend",out error));Equal(before,Snapshot(battle.State));
            battle.State.Ember=6;battle.Unit("friend").Hp=0;before=Snapshot(battle.State);False(battle.TryLink("lian",0,"lian_crescendo",LinkKind.Support,"friend",out error));Equal(before,Snapshot(battle.State));battle.Unit("friend").Hp=500;before=Snapshot(battle.State);False(battle.TryLink("lian",1,"lian_crescendo",LinkKind.Support,"friend",out error));Equal(before,Snapshot(battle.State));
            True(battle.TryLink("lian",0,"lian_crescendo",LinkKind.Support,"friend",out error));Equal(3,battle.PlannedCost);Equal(1,battle.State.Links.Count);
        });
        Test("previews account for awakening and attack bonuses without RNG or any state mutation",()=>{
            var samples=new Samples();var battle=Fixture("noa",samples);var actor=battle.Unit("noa");actor.AwakeningStacks=3;actor.SupportPower=3;Force(battle,"noa",3);var link=new LinkCommand { ActorId="noa",SkillId="noa_crescendo",TargetId="forecast",Kind=LinkKind.Clash,Slot=0 };string before=Snapshot(battle.State);
            var boosted=battle.Preview(link);for(int i=0;i<5;i++){battle.Preview(link);var skill=battle.PreviewSkill("noa","noa_crescendo",LinkKind.Clash);True(skill.Awakened);Equal(21,skill.BasePower);Equal(6,skill.FlamePower);}Equal(0,samples.Calls);Equal(before,Snapshot(battle.State));
            actor.AwakeningStacks=0;actor.SupportPower=0;var normal=battle.Preview(link);True(boosted.AllyPower>normal.AllyPower);Equal(0,samples.Calls);
        });
        Test("new battles and restart reset all temporary modifiers and reset quote order without saving anything",()=>{
            var progress=new ProgressService(new ProgressData());var before=Snapshot(progress.Data);var battle=BattleEngine.CreatePrototype(progress.Data,0);var actor=battle.Unit("lian");actor.AwakeningStacks=3;actor.Barrier=40;actor.SupportPower=3;NoIntents(battle);Force(battle,"lian",1);Link(battle,"lian",1,LinkKind.Strike,"anger");battle.ResolveRound();var first=battle.State.Events.Single(e=>e.Type=="SkillStart").Quote;battle.Restart();actor=battle.Unit("lian");Equal(0,actor.AwakeningStacks);Equal(0,actor.Barrier);Equal(0,actor.SupportPower);NoIntents(battle);Force(battle,"lian",1);Link(battle,"lian",1,LinkKind.Strike,"anger");battle.ResolveRound();Equal(first,battle.State.Events.Single(e=>e.Type=="SkillStart").Quote);Equal(before,Snapshot(progress.Data));
        });
        string output=Path.Combine("QA","SkillFlavor","2026-10-16");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"core-validation.json"),"{\n  \"status\": \"PASS\",\n  \"checks\": "+passed+",\n  \"playerSaveModified\": false\n}\n");
        Console.WriteLine("Combat skill flavor: "+passed+" checks passed.");
    }
    static string[] Ids(){return new[]{"lian","sera","noa","minato","heidel","kai","mare"};}
    static BattleEngine Fixture(string actorId,IBattleRandom random=null)
    {
        var actor=PrototypeData.CreateCharacter(actorId,1);actor.Hp=actor.MaxHp=500;actor.Element=Element.Radiance;actor.Attack=10;var friend=PrototypeData.CreateCharacter(actorId=="minato"?"mare":"minato",1);friend.Id="friend";friend.Hp=friend.MaxHp=500;
        var enemy=new UnitState { Id="enemy",Name="검수 표적",IsEnemy=true,Element=Element.Radiance,Hp=100000,MaxHp=100000,Attack=10,Defense=1,Speed=10,Rift=100000,MaxRift=100000 };
        enemy.Parts.Add(new PartState { Id="enemy_arm",Name="검수 무기",Hp=100000,MaxHp=100000 });
        var state=new BattleState { BattleId=Guid.NewGuid().ToString("N"),StageId="test_skill",Ember=6,Gloom=0,RandomSeed=0,IsBoss=false,Allies=new List<UnitState>{actor,friend},Enemies=new List<UnitState>{enemy} };
        var battle=new BattleEngine(state,null,random??new Samples());state.Intents.Add(new IntentState { Id="forecast",ActorId="enemy",TargetId=actorId,Name="검수 공격",BasePower=12,FlamePower=2,FlameCount=1,HitPowers=new[]{12},Speed=5,Element=Element.Radiance });return battle;
    }
    static void NoIntents(BattleEngine battle){battle.State.Intents.Clear();}
    static void EasyIntent(BattleEngine battle){var i=battle.State.Intents[0];i.BasePower=1;i.FlamePower=1;i.FlameCount=1;}
    static void Force(BattleEngine battle,string actorId,int number)
    { var actor=battle.Unit(actorId);var skill=actor.Skills.Single(x=>x.Number==number);actor.SkillHand=new List<SkillCard>{new SkillCard { Id=actorId+":forced:"+battle.State.Round,SkillId=skill.Id }};actor.DrawPile.Clear();actor.DiscardPile.Clear(); }
    static void Link(BattleEngine battle,string actorId,int number,LinkKind kind,string target,int slot=0)
    { var skill=battle.Unit(actorId).Skills.Single(x=>x.Number==number);True(battle.TryLink(actorId,slot,skill.Id,kind,target,out var error)); }
    static void Guard(BattleEngine battle,string actorId){var skill=battle.Unit(actorId).Skills.Single(BattleRules.IsDefensive);True(battle.TryLink(actorId,0,skill.Id,LinkKind.Guard,actorId,out var error));}
    static string Snapshot(object value){return JsonSerializer.Serialize(value,new JsonSerializerOptions { IncludeFields=true });}
    static void Test(string name,Action test){try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception ex){Console.Error.WriteLine("FAIL "+name+"\n"+ex);Environment.Exit(1);}}
    static void True(bool value){if(!value)throw new Exception("Expected true");}static void False(bool value){if(value)throw new Exception("Expected false");}static void Equal<T>(T expected,T actual){if(!Equals(expected,actual))throw new Exception("Expected "+expected+", actual "+actual);}
}
