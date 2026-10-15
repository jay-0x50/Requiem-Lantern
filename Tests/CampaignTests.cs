using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RequiemLantern.Core;

internal static class CampaignTests
{
    static readonly List<StageData> Stages=CampaignCatalog.Chapters.SelectMany(c=>c.Stages).ToList();
    static readonly StageData Opening=Stages[0],Mixed=Stages[1];
    static readonly string Root=FindRoot();
    static int passed,failed;
    static void Main()
    {
        Test("catalog has five chapters, ten sequential stages and all stage kinds",()=>{
            Equal(5,CampaignCatalog.Chapters.Count);Equal(50,Stages.Count);Equal(50,Stages.Select(s=>s.Id).Distinct().Count());
            for(int i=0;i<5;i++){
                var chapter=CampaignCatalog.Chapters[i];Equal(i+1,chapter.Number);Equal(10,chapter.Stages.Count);
                for(int j=0;j<10;j++){Equal((i+1)+"-"+(j+1),chapter.Stages[j].Number);Equal(i+1,chapter.Stages[j].Chapter);}
            }
            Equal(25,Stages.Count(s=>s.Kind==StageKind.Story));Equal(7,Stages.Count(s=>s.Kind==StageKind.Battle));Equal(18,Stages.Count(s=>s.Kind==StageKind.StoryBattle));
            foreach(var stage in Stages){
                if(stage.Kind==StageKind.Story){True(stage.Before.Count>0);Equal(0,stage.After.Count);Equal(0,stage.Party.Length);}
                else{True(stage.Party.Length>=1&&stage.Party.Length<=PrototypeData.PartyCapacity);True(!string.IsNullOrEmpty(stage.EnemyName));True(!string.IsNullOrEmpty(stage.EnemyArt));}
                if(stage.Kind==StageKind.Battle){Equal(0,stage.Before.Count);Equal(0,stage.After.Count);}
                if(stage.Kind==StageKind.StoryBattle){True(stage.Before.Count>0);True(stage.After.Count>0);}
            }
        });
        Test("compiled campaign matches source and every chapter includes ensemble conversations",()=>{
            using(var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"기획서","메인스토리","campaign.json")))){
                var chapters=source.RootElement.GetProperty("chapters").EnumerateArray().ToList();Equal(5,chapters.Count);
                int authoredLines=0,compiledLines=0;
                foreach(var chapter in chapters){
                    var compiledChapter=CampaignCatalog.Chapters.Single(c=>c.Number==chapter.GetProperty("number").GetInt32());
                    Equal(chapter.GetProperty("focusCharacterId").GetString(),compiledChapter.FocusCharacterId);
                    Equal(chapter.GetProperty("focusStorySummary").GetString(),compiledChapter.FocusStorySummary);
                    foreach(var sourceStage in chapter.GetProperty("stages").EnumerateArray()){
                        var stage=CampaignCatalog.Get(sourceStage.GetProperty("id").GetString());True(stage!=null);
                        Equal(sourceStage.GetProperty("number").GetString(),stage.Number);Equal(sourceStage.GetProperty("kind").GetString(),stage.Kind.ToString());
                        Equal(sourceStage.GetProperty("enemyCount").GetInt32(),stage.EnemyCount);
                        Equal(string.Join(",",sourceStage.GetProperty("party").EnumerateArray().Select(x=>x.GetString())),string.Join(",",stage.Party));
                        Equal(string.Join(",",sourceStage.GetProperty("participants").EnumerateArray().Select(x=>x.GetString())),string.Join(",",stage.Participants));
                        foreach(string part in new[]{"before","after"}){
                            var lines=sourceStage.GetProperty(part).EnumerateArray().ToList();var compiled=part=="before"?stage.Before:stage.After;
                            Equal(lines.Count,compiled.Count);authoredLines+=lines.Count;compiledLines+=compiled.Count;
                            for(int i=0;i<lines.Count;i++){Equal(lines[i][0].GetString(),compiled[i].Speaker);Equal(lines[i][1].GetString(),compiled[i].Text);}
                        }
                    }
                }
                Equal(authoredLines,compiledLines);True(compiledLines>=400);
            }
            foreach(var chapter in CampaignCatalog.Chapters){
                True(chapter.Stages.Count(s=>Speakers(s).Count>=3)>=4,"Chapter "+chapter.Number+" needs more ensemble dialogue.");
                True(chapter.Stages.SelectMany(s=>Speakers(s)).Distinct().Count()>=4);
            }
            True(Stages.SelectMany(s=>s.Before.Concat(s.After)).Any(line=>!string.IsNullOrEmpty(line.ListenerPortrait)));
        });
        Test("four-person campaign parties preserve staged joins and focal character roles",()=>{
            Equal(4,PrototypeData.PartyCapacity);var established=Stages.Where(s=>s.HasBattle&&(s.Chapter>2||(s.Chapter==2&&int.Parse(s.Number.Split('-')[1])>=4))).ToList();
            Equal(19,established.Count);
            foreach(var stage in established){
                Equal(4,stage.Party.Length,stage.Number);Equal(4,stage.Party.Distinct().Count(),stage.Number);
                True(stage.Party.All(id=>stage.Participants.Contains(id)),"Battle actor is missing from story participants: "+stage.Number);
                True(stage.Party.Contains("minato"),"Four-person party lost healing coverage: "+stage.Number);
                string focus=CampaignCatalog.Chapters.Single(c=>c.Number==stage.Chapter).FocusCharacterId;
                if(stage.Number!="3-2"&&stage.Number!="3-3")True(stage.Party.Contains(focus),"Focal character is missing from battle: "+stage.Number);
            }
            foreach(var pair in new[]{new[]{"1-2","lian"},new[]{"1-4","lian,sera"},new[]{"1-5","lian,sera"},new[]{"1-8","lian,sera,minato"},new[]{"1-9","lian,sera,minato"},new[]{"2-2","lian,sera,minato"}})
                Equal(pair[1],string.Join(",",Stages.Single(s=>s.Number==pair[0]).Party));
            True(Stages.Where(s=>s.Chapter<3||(s.Chapter==3&&int.Parse(s.Number.Split('-')[1])<6)).All(s=>!s.Party.Contains("noa")));
            True(Stages.Where(s=>s.Chapter==1).All(s=>!s.Party.Contains("heidel")));
            True(Stages.Single(s=>s.Number=="3-6").Party.Contains("noa"));
        });
        Test("campaign rejects oversized and repeated parties before mutating player growth",()=>{
            foreach(var party in new[]{new[]{"lian","sera","minato","noa","heidel"},new[]{"lian","sera","minato","lian"},new string[0]}){
                var stage=new StageData{Kind=StageKind.Battle,Party=party};var data=new ProgressData();bool rejected=false;
                try{CampaignBattleFactory.Create(stage,data,0);}catch(InvalidOperationException){rejected=true;}
                True(rejected);Equal(0,data.Characters.Count);
            }
        });
        Test("authored encounter counts distinguish groups, tutorial and single bosses",()=>{
            foreach(var stage in Stages){
                if(!stage.HasBattle){Equal(0,stage.EnemyCount);continue;}
                if(stage.IsBoss||stage.Number=="1-2")Equal(1,stage.EnemyCount,stage.Number);
                else True(stage.EnemyCount>=2&&stage.EnemyCount<=3,stage.Number);
                var battle=CampaignBattleFactory.Create(stage,seed:0);Equal(stage.EnemyCount,battle.State.Enemies.Count,stage.Number);
                Equal(stage.IsBoss,battle.State.IsBoss,stage.Number);True(battle.State.Allies.All(a=>a.SlotCount==1),stage.Number);
                battle.State.Round=5;BattleRules.ApplyActionProgression(battle.State);
                for(int index=0;index<battle.State.Allies.Count;index++)Equal(stage.IsBoss&&index>=2?1:2,battle.State.Allies[index].SlotCount,stage.Number);
                Equal(stage.EnemyCount,battle.State.Enemies.Select(e=>e.Id).Distinct().Count());
                Equal(stage.EnemyCount*2,battle.State.Enemies.SelectMany(e=>e.Parts).Select(p=>p.Id).Distinct().Count());
                Equal(battle.State.Intents.Count,battle.State.Intents.Select(i=>i.Id).Distinct().Count());
                foreach(var intent in battle.State.Intents){
                    var owner=battle.Unit(intent.ActorId);True(owner!=null&&owner.IsEnemy);True(owner.Parts.Any(p=>p.Id==intent.PartId));
                    True(intent.TargetId=="all"||battle.State.Allies.Any(a=>a.Id==intent.TargetId&&a.Alive));
                }
                if(stage.HumanEnemy&&stage.EnemyCount>1)True(battle.State.Enemies.Skip(1).All(e=>e.Portrait=="HumanGuard"&&e.IsHuman));
            }
            var group=Stages.Single(s=>s.Number=="2-4");Equal(3,group.EnemyCount);
            var tutorial=CampaignBattleFactory.Create(Stages.Single(s=>s.Number=="1-2"),seed:0);
            False(tutorial.State.IsBoss);Equal(1,tutorial.State.Enemies.Count);tutorial.State.Round=2;BattleRules.ApplyActionProgression(tutorial.State);Equal(2,tutorial.State.Allies.Single().SlotCount);
        });
        Test("group restarts rebuild every enemy, target, part and cost without changing rewards",()=>{
            var stage=Stages.Single(s=>s.Number=="2-4");var battle=CampaignBattleFactory.Create(stage,seed:42);
            var ids=string.Join(",",battle.State.Enemies.Select(e=>e.Id));var intents=string.Join(",",battle.State.Intents.Select(i=>i.Id+"="+i.ActorId+"/"+i.TargetId));
            int coin=battle.State.CoinReward,lantern=battle.State.FirstLanternReward,memory=battle.State.MemoryReward;
            battle.RecommendLinks();battle.ResolveRound();battle.Restart();
            Equal(ids,string.Join(",",battle.State.Enemies.Select(e=>e.Id)));Equal(intents,string.Join(",",battle.State.Intents.Select(i=>i.Id+"="+i.ActorId+"/"+i.TargetId)));
            True(battle.State.Enemies.All(e=>e.Alive&&e.Hp==e.MaxHp&&e.Parts.All(p=>p.Hp==p.MaxHp)));
            Equal(coin,battle.State.CoinReward);Equal(lantern,battle.State.FirstLanternReward);Equal(memory,battle.State.MemoryReward);
            Equal(0,battle.PlannedCost);Equal(0,battle.State.Links.Count);Equal(8,battle.State.Allies.Sum(a=>a.SkillHand.Count));
            False(battle.State.IsBoss);True(battle.State.Allies.All(a=>a.SlotCount==1));
        });
        Test("invalid encounter sizes are rejected before player growth changes",()=>{
            foreach(int count in new[]{0,4,-1}){
                var stage=new StageData{Kind=StageKind.Battle,Party=new[]{"lian"},EnemyCount=count};var data=new ProgressData();bool rejected=false;
                try{CampaignBattleFactory.Create(stage,data,0);}catch(InvalidOperationException){rejected=true;}
                True(rejected);Equal(0,data.Characters.Count);
            }
        });
        Test("the five main chapters carry character arcs and human friend encounters",()=>{
            Equal("lian,sera,noa,heidel,minato",string.Join(",",CampaignCatalog.Chapters.Select(c=>c.FocusCharacterId)));
            var names=new Dictionary<string,string>{{"lian","리안"},{"sera","세라"},{"noa","노아"},{"heidel","하이델"},{"minato","미나토"}};
            foreach(var chapter in CampaignCatalog.Chapters){
                string name=names[chapter.FocusCharacterId];True(!string.IsNullOrWhiteSpace(chapter.FocusStorySummary));
                True(chapter.Stages.Count(stage=>stage.Before.Concat(stage.After).Any(line=>line.Speaker==name))>=6,"The focal character disappeared from the main arc: "+name);
                True(chapter.Stages.Last().Before.Concat(chapter.Stages.Last().After).Any(line=>line.Speaker==name),"The final main stage did not conclude the focal character's arc: "+name);
                foreach(var stage in chapter.Stages)foreach(var line in stage.Before.Concat(stage.After))
                    True(line.Caption.Contains("제"+chapter.Number+"장")&&line.Caption.Contains(name)&&line.Caption.Contains(stage.Number),"Main story caption lost its character/chapter/stage context.");
            }
            foreach(var pair in new[]{new[]{"lian","로웬"},new[]{"sera","엘렌"},new[]{"noa","이브"}}){
                var chapter=CampaignCatalog.Chapters.Single(c=>c.FocusCharacterId==pair[0]);
                True(chapter.Stages.Any(stage=>stage.HasBattle&&stage.Before.Any(line=>line.Speaker==pair[1])),"Human friend dialogue is missing from the main battle flow: "+pair[1]);
            }
        });
        Test("old prologue and isolated legacy victory do not bypass intervening stages",()=>{
            var data=new ProgressData{PrologueCompleted=true};data.ClearedStages.Add("chapter1_beacon_defense");
            True(CampaignRules.IsCleared(data,Opening));True(CampaignRules.IsUnlocked(data,Mixed));
            False(CampaignRules.IsUnlocked(data,Stages[2]));False(CampaignRules.IsUnlocked(data,Stages[9]));
            False(CampaignRules.IsUnlocked(data,CampaignCatalog.Chapters[1].Stages[0]));
            foreach(var stage in Stages.Take(9))CampaignRules.Complete(data,stage);
            True(CampaignRules.IsUnlocked(data,Stages[9]));False(CampaignRules.IsUnlocked(data,Stages[10]));
        });
        Test("story-only completion is free, saved and sequentially unlocks the next stage",()=>{
            var progress=new ProgressService(new ProgressData());var session=new CampaignSession();
            True(session.Enter(Opening.Id,progress.Data,false,out var error));Equal(CampaignPhase.BeforeStory,session.Phase);
            True(session.FinishStory(progress.Data,out var changed,out error));True(changed);Equal(CampaignPhase.Complete,session.Phase);
            True(progress.Data.PrologueCompleted);True(CampaignRules.IsCleared(progress.Data,Opening));True(CampaignRules.IsUnlocked(progress.Data,Mixed));
            Equal(0,progress.Data.Coins);Equal(0,progress.Data.LanternFragments);Equal(0,progress.Data.MemoryFragments);Equal(0,progress.Data.ClaimedBattles.Count);
            var loaded=RoundTrip(progress.Data,"story-only");True(CampaignRules.IsCleared(loaded,Opening));True(CampaignRules.IsUnlocked(loaded,Mixed));
        });
        Test("mixed before-story cannot clear the stage or claim a victory",()=>{
            var progress=UnlockedMixed();var session=new CampaignSession();True(session.Enter(Mixed.Id,progress.Data,false,out var error));
            var victory=Won(Mixed,progress.Data,0);
            False(session.AcceptVictory(victory,progress,out var reward,out error));Equal(0,progress.Data.Coins);True(reward==null);
            True(session.FinishStory(progress.Data,out var changed,out error));False(changed);Equal(CampaignPhase.Battle,session.Phase);
            False(CampaignRules.IsCleared(progress.Data,Mixed));False(CampaignRules.IsUnlocked(progress.Data,Stages[2]));
            False(session.FinishStory(progress.Data,out changed,out error));Equal(CampaignPhase.Battle,session.Phase);
        });
        Test("mixed victory grants rewards once and defers completion until after-story",()=>{
            var progress=UnlockedMixed();var session=BattleSession(progress,Mixed);var battle=Won(Mixed,progress.Data,0);
            True(session.AcceptVictory(battle,progress,out var reward,out var error));True(reward.FirstClear);
            Equal(battle.State.CoinReward,progress.Data.Coins);True(progress.Data.RewardedStages.Contains(Mixed.Id));Equal(CampaignPhase.AfterStory,session.Phase);
            False(CampaignRules.IsCleared(progress.Data,Mixed));False(CampaignRules.IsUnlocked(progress.Data,Stages[2]));Equal(Mixed.After.Count,session.CurrentStory.Count);
            int coins=progress.Data.Coins;False(session.AcceptVictory(battle,progress,out reward,out error));Equal(coins,progress.Data.Coins);
            True(session.FinishStory(progress.Data,out var changed,out error));True(changed);Equal(CampaignPhase.Complete,session.Phase);
            True(CampaignRules.IsCleared(progress.Data,Mixed));True(CampaignRules.IsUnlocked(progress.Data,Stages[2]));
        });
        Test("winning then exiting resumes saved after-story without replaying the fight",()=>{
            var progress=UnlockedMixed();var session=BattleSession(progress,Mixed);True(session.AcceptVictory(Won(Mixed,progress.Data,1),progress,out var reward,out var error));
            var restored=RoundTrip(progress.Data,"after-win");int coins=restored.Coins;session.Exit();
            True(session.Enter(Mixed.Id,restored,false,out error));Equal(CampaignPhase.AfterStory,session.Phase);Equal(Mixed.After.Count,session.CurrentStory.Count);
            False(CampaignRules.IsCleared(restored,Mixed));True(session.FinishStory(restored,out var changed,out error));True(changed);
            Equal(coins,restored.Coins);True(CampaignRules.IsUnlocked(restored,Stages[2]));
        });
        Test("replay combines before and after stories and changes no progress or rewards",()=>{
            var progress=UnlockedMixed();CampaignRules.Complete(progress.Data,Mixed);progress.Data.Coins=91;progress.Data.LanternFragments=7;
            string before=Snapshot(progress.Data);var session=new CampaignSession();
            True(session.Enter(Mixed.Id,progress.Data,true,out var error));True(session.Replay);Equal(Mixed.Before.Count+Mixed.After.Count,session.CurrentStory.Count);
            // The rejected battle belongs to an independent fixture. Building it must
            // not populate the replay player's growth records before this assertion.
            False(session.AcceptVictory(Won(Mixed,new ProgressData(),2),progress,out var reward,out error));
            True(session.FinishStory(progress.Data,out var changed,out error));False(changed);Equal(CampaignPhase.Complete,session.Phase);Equal(before,Snapshot(progress.Data));
            var battleOnly=Stages.First(s=>s.Kind==StageKind.Battle);CampaignRules.Complete(progress.Data,battleOnly);
            True(session.Enter(battleOnly.Id,progress.Data,true,out error));Equal(CampaignPhase.Complete,session.Phase);Equal(0,session.CurrentStory.Count);
        });
        Test("invalid and locked entry preserve the current session",()=>{
            var data=new ProgressData();var session=new CampaignSession();True(session.Enter(Opening.Id,data,false,out var error));
            var active=session.Active;var phase=session.Phase;bool replay=session.Replay;
            foreach(var request in new[]{"missing-stage",Stages[49].Id,Mixed.Id}){
                False(session.Enter(request,data,false,out error));True(object.ReferenceEquals(active,session.Active));Equal(phase,session.Phase);Equal(replay,session.Replay);
            }
            False(session.Enter(Opening.Id,data,true,out error));True(object.ReferenceEquals(active,session.Active));Equal(phase,session.Phase);
        });
        Test("defeat grants no reward and restart retains stage identity without advancing",()=>{
            var progress=UnlockedMixed();var session=BattleSession(progress,Mixed);var battle=CampaignBattleFactory.Create(Mixed,progress.Data,0);
            foreach(var unit in battle.State.Allies)unit.Hp=0;battle.ResolveRound(IgniteGrade.Good);Equal(BattleOutcome.Defeat,battle.State.Outcome);
            False(session.AcceptVictory(battle,progress,out var reward,out var error));Equal(0,progress.Data.Coins);Equal(CampaignPhase.Battle,session.Phase);
            False(CampaignRules.IsCleared(progress.Data,Mixed));False(CampaignRules.IsUnlocked(progress.Data,Stages[2]));
            string oldBattle=battle.State.BattleId;battle.Restart();Equal(Mixed.Id,battle.State.StageId);Equal(BattleOutcome.Ongoing,battle.State.Outcome);Equal(1,battle.State.Round);
            True(oldBattle!=battle.State.BattleId);True(battle.State.Allies.All(u=>u.Hp==u.MaxHp));False(CampaignRules.IsUnlocked(progress.Data,Stages[2]));
        });
        Test("battle-only stage completes on victory without a dialogue gate",()=>{
            var stage=Stages.First(s=>s.Kind==StageKind.Battle);var progress=new ProgressService(new ProgressData());
            foreach(var previous in Stages.TakeWhile(s=>s.Id!=stage.Id))CampaignRules.Complete(progress.Data,previous);
            var session=BattleSession(progress,stage);Equal(0,session.CurrentStory.Count);
            True(session.AcceptVictory(Won(stage,progress.Data,0),progress,out var reward,out var error));Equal(CampaignPhase.Complete,session.Phase);True(CampaignRules.IsCleared(progress.Data,stage));
        });
        Test("character synchronization persists separately from level and survives factory restart",()=>{
            var progress=new ProgressService(new ProgressData{LanternFragments=50});
            foreach(string id in new[]{"lian","sera","minato","heidel","noa"}){
                True(progress.Synchronize(id,out var error));Equal(1,progress.GetCharacter(id).SyncLevel);Equal(1,progress.GetCharacter(id).Level);
            }
            Equal(0,progress.Data.LanternFragments);var restored=RoundTrip(progress.Data,"sync");
            foreach(var stage in Stages.Where(s=>s.HasBattle)){
                var battle=CampaignBattleFactory.Create(stage,restored,0);
                foreach(var unit in battle.State.Allies){
                    Equal(1,unit.SyncLevel);True(BattleRules.IsSkillUnlocked(unit,unit.Skills.Single(s=>s.Number==3)));Equal(2,unit.SkillHand.Count);
                    var bag=unit.SkillHand.Select(c=>c.SkillId).Concat(unit.DrawPile).Concat(unit.DiscardPile).Select(id=>battle.Skill(unit.Id,id).Number).ToList();
                    Equal(3,bag.Count(n=>n==1));Equal(2,bag.Count(n=>n==2));Equal(1,bag.Count(n=>n==3));
                    unit.SyncLevel=0;unit.Sanity=-20;unit.Hp=1;
                }
                battle.Restart();foreach(var unit in battle.State.Allies){Equal(1,unit.SyncLevel);Equal(0,unit.Sanity);Equal(unit.MaxHp,unit.Hp);}
            }
        });
        Test("old version-one XML initializes new campaign and growth fields",()=>{
            string path=OutputPath("old-save.xml");
            File.WriteAllText(path,"<?xml version=\"1.0\"?><ProgressData><Version>1</Version><PlayerName>카이</PlayerName><PrologueCompleted>true</PrologueCompleted><Coins>120</Coins><LanternFragments>10</LanternFragments><MemoryFragments>1</MemoryFragments><KeepLevel>1</KeepLevel><ClearedStages><string>chapter1_beacon_defense</string></ClearedStages><ClaimedBattles/></ProgressData>");
            var store=new SaveStore(path);var data=store.Load();True(store.LastWarning==null);Equal(120,data.Coins);Equal(10,data.LanternFragments);
            True(data.RewardedStages!=null);True(data.Characters!=null);Equal(0,data.RewardedStages.Count);Equal(0,data.Characters.Count);
            True(CampaignRules.IsUnlocked(data,Mixed));False(CampaignRules.IsUnlocked(data,Stages[9]));
            var progress=new ProgressService(data);True(progress.Synchronize("lian",out var error));store.Save(data);Equal(1,store.Load().Characters.Single().SyncLevel);
            var oldBattle=Won(Stages[8],data,0);True(progress.ClaimVictory(oldBattle,out var reward));False(reward.FirstClear);Equal(oldBattle.State.RepeatLanternReward,reward.LanternFragments);
        });
        Test("all authored human bosses and story portraits have matching art resources",()=>{
            foreach(string art in Stages.Where(s=>s.HasBattle).Select(s=>s.EnemyArt).Concat(Stages.SelectMany(s=>s.Before.Concat(s.After)).SelectMany(l=>new[]{l.Portrait,l.ListenerPortrait})).Where(a=>!string.IsNullOrEmpty(a)).Distinct())
                True(File.Exists(Path.Combine(Root,"RequiemLantern","Assets","Resources","Art",art+".png")),"Missing portrait asset: "+art);
            True(Stages.Any(s=>s.HumanEnemy&&s.EnemyArt=="Rowan"));True(Stages.Any(s=>s.HumanEnemy&&s.EnemyArt=="Ellen"));True(Stages.Any(s=>s.HumanEnemy&&s.EnemyArt=="Eve"));True(Stages.Any(s=>s.HumanEnemy&&s.EnemyArt=="Orphe"));
        });
        Test("all twenty-five campaign battles win under AI at Good for seeds zero, one and two",()=>{
            var failures=new List<string>();var runs=new List<string>();
            foreach(var stage in Stages.Where(s=>s.HasBattle))foreach(int seed in new[]{0,1,2}){
                var battle=CampaignBattleFactory.Create(stage,new ProgressData(),seed);int stalemates=0;
                var offeredIds=new HashSet<string>();
                Equal(string.Join(",",stage.Party),string.Join(",",battle.State.Allies.Select(u=>u.Id)));True(battle.State.Allies.All(u=>u.SyncLevel==0&&u.Sanity==0));
                while(battle.State.Outcome==BattleOutcome.Ongoing&&battle.State.Round<=40){
                    for(int index=0;index<battle.State.Allies.Count;index++)
                        Equal(battle.State.Round>=index+2&&(!stage.IsBoss||index<2)?2:1,battle.State.Allies[index].SlotCount,"Action capacity drifted in "+stage.Number+" round "+battle.State.Round);
                    foreach(var unit in battle.State.Allies.Where(u=>u.Alive)){
                        Equal(2,unit.SkillHand.Count,"Expected two offered cards for "+stage.Number+" / "+unit.Name);
                        True(unit.SkillHand.All(card=>offeredIds.Add(card.Id)),"A prior-round offer survived in "+stage.Number+" / "+unit.Name);
                        var bag=unit.SkillHand.Select(c=>c.SkillId).Concat(unit.DrawPile).Concat(unit.DiscardPile).Select(id=>battle.Skill(unit.Id,id).Number).ToList();
                        Equal(3,bag.Count(n=>n==1));Equal(2,bag.Count(n=>n==2));Equal(0,bag.Count(n=>n==3));
                    }
                    battle.RecommendLinks();True(battle.State.Links.Count>0,"AI produced no action in "+stage.Number);
                    True(battle.State.Links.All(l=>BattleRules.IsSkillUnlocked(battle.Unit(l.ActorId),battle.Skill(l.ActorId,l.SkillId))));
                    True(battle.State.Links.All(l=>l.Slot>=0&&l.Slot<battle.Unit(l.ActorId).SlotCount),"AI used a locked action in "+stage.Number);
                    var numbered=battle.State.Links.Where(l=>battle.Skill(l.ActorId,l.SkillId).Number>0).ToList();
                    Equal(numbered.Count,numbered.Select(l=>l.CardId).Distinct().Count(),"One offered card was reserved for multiple actions in "+stage.Number);
                    True(numbered.All(l=>battle.Unit(l.ActorId).SkillHand.Any(c=>c.Id==l.CardId&&c.SkillId==l.SkillId)),"AI selected an unavailable card in "+stage.Number);
                    var events=battle.ResolveRound(IgniteGrade.Good);stalemates+=events.Count(e=>e.Type=="Stalemate");
                    foreach(var unit in battle.State.Allies.Concat(battle.State.Enemies))True(unit.Sanity>=-20&&unit.Sanity<=20,"Sanity overflow in "+stage.Number);
                    if(battle.State.Outcome==BattleOutcome.Ongoing){
                        battle.BeginNextRound();Equal(0,battle.State.Links.Count);
                        True(battle.State.Allies.SelectMany(unit=>unit.SkillHand).All(card=>!offeredIds.Contains(card.Id)),"Round refresh retained an old card in "+stage.Number);
                    }
                }
                string line=stage.Number+" seed="+seed+" outcome="+battle.State.Outcome+" rounds="+battle.State.Round+" stalemates="+stalemates;
                runs.Add(line);if(battle.State.Outcome!=BattleOutcome.Victory||stalemates>0)failures.Add(line);
            }
            File.WriteAllLines(OutputPath("balance-runs.txt"),runs);
            Equal(75,runs.Count);True(failures.Count==0,"Campaign balance failures:\n"+string.Join("\n",failures));
        });
        Console.WriteLine("Campaign tests: "+passed+" passed, "+failed+" failed.");
        Environment.ExitCode=failed==0?0:1;
    }
    static HashSet<string> Speakers(StageData stage)
    { return new HashSet<string>(stage.Before.Concat(stage.After).Where(l=>!string.IsNullOrEmpty(l.Speaker)&&l.Speaker!="기억 속 목소리").Select(l=>l.Speaker)); }
    static ProgressService UnlockedMixed(){var value=new ProgressService(new ProgressData());CampaignRules.Complete(value.Data,Opening);return value;}
    static CampaignSession BattleSession(ProgressService progress,StageData stage)
    {
        var session=new CampaignSession();True(session.Enter(stage.Id,progress.Data,false,out var error));
        if(session.Phase==CampaignPhase.BeforeStory)True(session.FinishStory(progress.Data,out var changed,out error));
        Equal(CampaignPhase.Battle,session.Phase);return session;
    }
    static BattleEngine Won(StageData stage,ProgressData progress,int seed)
    {
        var battle=CampaignBattleFactory.Create(stage,progress,seed);
        while(battle.State.Outcome==BattleOutcome.Ongoing&&battle.State.Round<=40){battle.RecommendLinks();battle.ResolveRound(IgniteGrade.Good);if(battle.State.Outcome==BattleOutcome.Ongoing)battle.BeginNextRound();}
        Equal(BattleOutcome.Victory,battle.State.Outcome,"Expected actual victory for "+stage.Number+" seed "+seed);return battle;
    }
    static string Snapshot(ProgressData data){return JsonSerializer.Serialize(data,new JsonSerializerOptions{IncludeFields=true});}
    static ProgressData RoundTrip(ProgressData data,string name){var store=new SaveStore(OutputPath(name+".xml"));store.Save(data);return store.Load();}
    static string OutputPath(string file){string directory=Path.Combine(Root,"Logs","CampaignTests");Directory.CreateDirectory(directory);return Path.Combine(directory,file);}
    static string FindRoot(){var path=new DirectoryInfo(Directory.GetCurrentDirectory());while(path!=null&&!Directory.Exists(Path.Combine(path.FullName,"RequiemLantern")))path=path.Parent;if(path==null)throw new InvalidOperationException("Run tests from the workspace.");return path.FullName;}
    static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception error){failed++;Console.WriteLine("FAIL "+name+"\n"+error.Message);}}
    static void True(bool value,string message=null){if(!value)throw new Exception(message??"Expected true.");}
    static void False(bool value,string message=null){True(!value,message??"Expected false.");}
    static void Equal<T>(T expected,T actual,string message=null){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception((message==null?"":message+": ")+"Expected "+expected+", got "+actual+".");}
}
