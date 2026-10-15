using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using RequiemLantern.Core;

internal static class CoreTests
{
    private static int passed;
    private static void Main()
    {
        Test("fresh battle deterministic intents", () => {
            var b = BattleEngine.CreatePrototype(); Equal(3, b.State.Ember); Equal(4, b.State.Intents.Count);
            Equal("난타", b.State.Intents[0].Name); Equal(2, b.State.Intents[0].HitPowers.Length); Equal(3, b.State.Intents[1].HitPowers.Length);
            Equal(1, b.State.Allies[0].SlotCount); True(b.State.IsBoss);
        });
        Test("four-person default party offers eight distinct cards and keeps the ember budget", () => {
            var b=BattleEngine.CreatePrototype(seed:31415);Equal(4,PrototypeData.PartyCapacity);
            Equal("lian,sera,minato,noa",string.Join(",",b.State.Allies.Select(u=>u.Id)));
            Equal(4,b.State.Allies.Sum(u=>u.SlotCount));Equal(3,b.State.Ember);
            var cards=b.State.Allies.SelectMany(u=>u.SkillHand).ToList();Equal(8,cards.Count);Equal(8,cards.Select(c=>c.Id).Distinct().Count());
            b.State.Intents.Clear();var enemy=b.Unit("anger");enemy.Hp=enemy.MaxHp=10000;enemy.Rift=enemy.MaxRift=10000;
            foreach(var part in enemy.Parts)part.Hp=part.MaxHp=10000;
            foreach(var unit in b.State.Allies){
                var skill=unit.Skills.Single(s=>s.Number==1);ForceHand(unit,skill.Id,skill.Id);string error;
                True(b.TryLink(unit.Id,0,skill.Id,LinkKind.Strike,enemy.Id,out error,unit.SkillHand[0].Id));
            }
            Equal(4,b.State.Links.Count);Equal(0,b.PlannedCost);b.ResolveRound();
            foreach(var unit in b.State.Allies){
                True(b.State.Events.Any(e=>e.Type=="Damage"&&e.ActorId==unit.Id));Equal(1,unit.SkillHand.Count);Equal(1,unit.DiscardPile.Count);Equal(5,Bag(unit).Count);
            }
            b.BeginNextRound();Equal(8,b.State.Allies.Sum(u=>u.SkillHand.Count));
            b.Restart();Equal(4,b.State.Allies.Count);Equal(8,b.State.Allies.Sum(u=>u.SkillHand.Count));
        });
        Test("boss forecasts match the fixed roster's growing actions and distribute their targets", () => {
            foreach(var stage in CampaignCatalog.Chapters.SelectMany(chapter=>chapter.Stages).Where(stage=>stage.IsBoss)){
                var b=CampaignBattleFactory.Create(stage,seed:0);int roster=b.State.Allies.Count;
                True(roster==3||roster==4);
                for(int round=1;round<=5;round++){
                    b.State.Round=round;b.BeginCampaign();int expected=roster+Math.Min(2,round-1);
                    Equal(expected,b.State.Intents.Count);Equal(expected,b.State.Allies.Sum(actor=>actor.SlotCount));
                    Equal(expected,b.State.Intents.Select(intent=>intent.Id).Distinct().Count());
                    True(b.State.Intents.All(intent=>intent.ActorId=="anger"&&BattleRules.Flames(intent)>=1));
                    True(b.State.Intents.Count(intent=>intent.Kind==IntentKind.Area)<=1);
                    var aimed=b.State.Intents.Where(intent=>intent.TargetId!="all").ToList();
                    Equal(Math.Min(roster,aimed.Count),aimed.Select(intent=>intent.TargetId).Distinct().Count());
                    if(stage.HumanEnemy){True(b.State.Intents.All(intent=>intent.Kind!=IntentKind.Area));Equal(1,b.State.Intents.Count(intent=>intent.Kind==IntentKind.Piercing));}
                    b.State.Allies[0].Hp=0;b.BeginCampaign();Equal(expected,b.State.Intents.Count);
                    True(b.State.Intents.All(intent=>intent.TargetId=="all"||intent.TargetId!=b.State.Allies[0].Id));
                    b.State.Allies[0].Hp=b.State.Allies[0].MaxHp;
                }
            }
            var tutorial=CampaignBattleFactory.Create(CampaignCatalog.Get("c1s02"),seed:0);
            for(int round=1;round<=5;round++){tutorial.State.Round=round;tutorial.BeginCampaign();Equal(1,tutorial.State.Intents.Count);False(tutorial.State.IsBoss);}
        });
        Test("broken boss parts remove their repeated actions instead of transferring them", () => {
            var b=BattleEngine.CreatePrototype(seed:0);b.State.Round=3;b.BeginCampaign();Equal(6,b.State.Intents.Count);
            var boss=b.Unit("anger");var arm=boss.Parts.Single(part=>part.Id=="anger_arm");var core=boss.Parts.Single(part=>part.Id=="anger_core");
            Equal(3,b.State.Intents.Count(intent=>intent.PartId==arm.Id));Equal(3,b.State.Intents.Count(intent=>intent.PartId==core.Id));
            arm.Hp=0;b.BeginCampaign();Equal(3,b.State.Intents.Count);True(b.State.Intents.All(intent=>intent.PartId==core.Id));
            core.Hp=0;b.BeginCampaign();Equal(1,b.State.Intents.Count);Equal("3:anger_body",b.State.Intents.Single().Id);
            b.Restart();Equal(1,b.State.Round);Equal(4,b.State.Intents.Count);True(b.Unit("anger").Parts.All(part=>!part.Destroyed));
        });
        Test("breaking a boss weapon cancels all of its repeated forecasts and preserves the core", () => {
            var b=BattleEngine.CreatePrototype(seed:0);b.State.Round=3;b.BeginCampaign();var boss=b.Unit("anger");
            boss.Hp=boss.MaxHp=10000;boss.Rift=boss.MaxRift=10000;boss.Parts[0].Hp=1;boss.Parts[1].Hp=boss.Parts[1].MaxHp=10000;
            ForceHand(b.Unit("sera"),"sera_glimmer","sera_glimmer");string error;
            True(b.TryLink("sera",0,"sera_glimmer",LinkKind.Strike,"anger_arm",out error));b.ResolveRound();
            Equal(3,b.State.Intents.Count(intent=>intent.PartId=="anger_arm"&&intent.Cancelled));
            True(b.State.Intents.Where(intent=>intent.PartId=="anger_core").All(intent=>!intent.Cancelled));
        });
        Test("a teammate intercepts an extra boss action and reuse finds that exact fresh forecast", () => {
            var b=BattleEngine.CreatePrototype(seed:0);b.State.Round=3;b.BeginCampaign();
            foreach(var unit in b.State.Allies.Concat(b.State.Enemies))unit.Hp=unit.MaxHp=100000;
            var boss=b.Unit("anger");boss.Rift=boss.MaxRift=10000;foreach(var part in boss.Parts)part.Hp=part.MaxHp=10000;
            var actor=b.Unit("sera");actor.Skills.Single(skill=>skill.Number==1).BasePower=1000;ForceHand(actor,"sera_glimmer","sera_glimmer");string error;
            var extra=b.Intent("3:anger_core_2");True(extra.TargetId!="sera");string oldCard=actor.SkillHand[0].Id;
            True(b.TryLink("sera",0,"sera_glimmer",LinkKind.Clash,extra.Id,out error,oldCard));b.ResolveRound();
            True(extra.Resolved);Equal(1,b.State.Events.Count(e=>e.Type=="Intercept"&&e.IntentId==extra.Id&&e.ActorId=="sera"));
            True(b.State.Intents.Where(intent=>intent.Id!=extra.Id).All(intent=>!intent.Cancelled));
            b.BeginNextRound();string hand=BagTrace(actor);b.ReusePreviousLinks();Equal(hand,BagTrace(actor));
            var reused=b.State.Links.Single();Equal("4:anger_core_2",reused.TargetId);Equal("anger",reused.IntentActorId);Equal("anger_core",reused.IntentPartId);
            True(reused.CardId!=oldCard&&actor.SkillHand.Any(card=>card.Id==reused.CardId));
        });
        Test("normal encounters unlock one extra action per round in fixed roster order", () => {
            var b=CampaignBattleFactory.Create(CampaignCatalog.Get("c2s04"),seed:0);False(b.State.IsBoss);
            var expected=new[]{"1,1,1,1","2,1,1,1","2,2,1,1","2,2,2,1","2,2,2,2","2,2,2,2"};
            var unlockRounds=b.State.Allies.Select(a=>BattleRules.ActionSlotUnlockRound(b.State,a)).ToArray();Equal("2,3,4,5",string.Join(",",unlockRounds));
            for(int round=1;round<=expected.Length;round++){
                Equal(round,b.State.Round);Equal(expected[round-1],string.Join(",",b.State.Allies.Select(a=>a.SlotCount)));
                Equal("2,2,2,2",string.Join(",",b.State.Allies.Select(a=>a.MaxActionSlots)));
                True(b.State.Allies.All(a=>a.SkillHand.Count==2));
                b.State.Intents.Clear();b.ResolveRound();b.BeginNextRound();
            }
        });
        Test("boss progression caps later allies even after a teammate falls", () => {
            var b=BattleEngine.CreatePrototype(seed:0);Equal("2,3,0,0",string.Join(",",b.State.Allies.Select(a=>BattleRules.ActionSlotUnlockRound(b.State,a))));
            Equal("2,2,1,1",string.Join(",",b.State.Allies.Select(a=>a.MaxActionSlots)));b.Unit("lian").Hp=0;
            for(int round=1;round<=6;round++){
                Equal(round<2?1:2,b.Unit("lian").SlotCount);Equal(round<3?1:2,b.Unit("sera").SlotCount);
                Equal(1,b.Unit("minato").SlotCount);Equal(1,b.Unit("noa").SlotCount);
                b.State.Intents.Clear();b.ResolveRound();b.BeginNextRound();
            }
            b.Restart();Equal(1,b.State.Round);True(b.State.Allies.All(a=>a.SlotCount==1&&a.SkillHand.Count==2));
        });
        Test("one-to-three-person parties preserve positions and supports never gain a second action", () => {
            foreach(int count in new[]{1,2,3}){
                var state=PrototypeData.CreateBattle(seed:0);state.IsBoss=false;state.Allies=state.Allies.Take(count).ToList();
                for(int round=1;round<=5;round++){
                    state.Round=round;BattleRules.ApplyActionProgression(state);
                    for(int index=0;index<count;index++)Equal(round>=index+2?2:1,state.Allies[index].SlotCount);
                }
                var support=state.Allies[count-1];support.IsSupport=true;BattleRules.ApplyActionProgression(state);
                Equal(1,support.SlotCount);Equal(1,support.MaxActionSlots);Equal(0,BattleRules.ActionSlotUnlockRound(state,support));
                state.Round=1;BattleRules.ApplyActionProgression(state);True(state.Allies.All(a=>a.SlotCount==1));
            }
        });
        Test("locked action rejects attacks, defenses and custom two-slot skills atomically while keeping both choices", () => {
            var data=new ProgressData();data.Characters.Add(new CharacterProgress{Id="lian",SyncLevel=1});
            var b=BattleEngine.CreatePrototype(data,0);var actor=b.Unit("lian");ForceHand(actor,"lian_glimmer","lian_crescendo");
            actor.Skills.Single(s=>s.Number==3).Slots=2; // A custom two-action skill exercises generic occupancy rules.
            string error;True(b.TryLink(actor.Id,0,"lian_glimmer",LinkKind.Strike,"anger",out error,actor.SkillHand[0].Id));
            string before=BagTrace(actor);var original=b.State.Links.Single();int cost=b.PlannedCost,ember=b.State.Ember;
            False(b.TryLink(actor.Id,1,"lian_guard",LinkKind.Guard,actor.Id,out error));
            False(b.TryLink(actor.Id,1,"lian_glimmer",LinkKind.Strike,"anger",out error,actor.SkillHand[0].Id));
            False(b.TryLink(actor.Id,0,"lian_crescendo",LinkKind.Strike,"anger",out error,actor.SkillHand[1].Id));
            Equal(original,b.State.Links.Single());Equal(cost,b.PlannedCost);Equal(ember,b.State.Ember);Equal(before,BagTrace(actor));
            False(b.IsCardAvailable(actor,actor.SkillHand[1].Id));False(b.OfferedSkills(actor).Any(s=>s.Number==3));
            b.State.Intents.Clear();b.ResolveRound();Equal(1,actor.DiscardPile.Count);Equal("lian_crescendo",actor.SkillHand.Single().SkillId);
            var expired=actor.SkillHand.Single().Id;b.BeginNextRound();Equal(2,actor.SlotCount);
            False(b.IsCardAvailable(actor,expired));True(actor.SkillHand.All(c=>c.Id!=expired));
            True(b.IsCardAvailable(actor,actor.SkillHand.First().Id,1));
        });
        Test("every synchronized skill three costs one action even in the boss party's third and fourth positions", () => {
            foreach(string id in new[]{"lian","sera","minato","noa","heidel"})foreach(int index in new[]{2,3}){
                var state=PrototypeData.CreateBattle(seed:0);state.IsBoss=true;
                state.Allies=new[]{"lian","sera","minato","noa","heidel"}.Where(other=>other!=id).Take(3).Select(other=>PrototypeData.CreateCharacter(other)).ToList();
                var actor=PrototypeData.CreateCharacter(id,1);state.Allies.Insert(index,actor);actor.Hp=actor.MaxHp-50;
                var b=new BattleEngine(state);var skill=actor.Skills.Single(s=>s.Number==3);Equal(1,skill.Slots);Equal(1,actor.SlotCount);Equal(1,actor.MaxActionSlots);
                ForceHand(actor,skill.Id,actor.Skills.Single(s=>s.Number==1).Id);string error;int cost=skill.Cost;
                string target=skill.Kind==SkillKind.Heal?actor.Id:"anger";var kind=skill.Kind==SkillKind.Heal?LinkKind.Support:LinkKind.Strike;
                True(b.TryLink(actor.Id,0,skill.Id,kind,target,out error,actor.SkillHand[0].Id));Equal(cost,b.PlannedCost);
                False(b.TryLink(actor.Id,1,actor.Skills.Single(BattleRules.IsDefensive).Id,LinkKind.Guard,actor.Id,out error));Equal(1,b.State.Links.Count);
                b.ResolveRound();Equal(1,actor.DiscardPile.Count);Equal(1,actor.SkillHand.Count);
                True(b.State.Events.Any(e=>e.Type=="FlameRoll"&&e.SkillId==skill.Id&&e.SkillNumber==3));
                if(skill.Kind==SkillKind.Heal)True(actor.Hp>actor.MaxHp-50);
            }
        });
        Test("recommendation and reuse respect the newly available capacity without reserving extra cards", () => {
            var b=BattleEngine.CreatePrototype(seed:0);b.RecommendLinks();
            foreach(var actor in b.State.Allies){True(b.State.Links.Count(l=>l.ActorId==actor.Id)<=1);True(actor.SkillHand.Count==2);}
            b.State.Intents.Clear();b.ClearLinks();ForceHand(b.Unit("lian"),"lian_glimmer","lian_glimmer");string error;
            True(b.TryLink("lian",0,"lian_glimmer",LinkKind.Strike,"anger",out error));b.ResolveRound();b.BeginNextRound();
            string hand=BagTrace(b.Unit("lian"));b.ReusePreviousLinks();Equal(1,b.State.Links.Count);Equal(0,b.State.Links.Single().Slot);Equal(hand,BagTrace(b.Unit("lian")));
            True(b.TryLink("lian",1,"lian_guard",LinkKind.Guard,"lian",out error));Equal(2,b.State.Links.Count);
            b.Restart();Equal(0,b.State.Links.Count);True(b.State.Allies.All(a=>a.SlotCount==1));b.ReusePreviousLinks();Equal(0,b.State.Links.Count);
        });
        Test("fourth party member can be forecast and healing target", () => {
            var state=PrototypeData.CreateBattle(seed:31415);state.Allies.Single(u=>u.Id=="noa").Hp=50;
            var b=new BattleEngine(state);b.BeginCampaign();
            True(b.State.Intents.Any(i=>i.Kind==IntentKind.Normal&&i.TargetId=="noa"));
            b.State.Intents.Clear();ForceHand(b.Unit("minato"),"minato_heal","minato_glimmer");string error;
            True(b.TryLink("minato",0,"minato_heal",LinkKind.Support,"noa",out error));b.ResolveRound();
            True(b.Unit("noa").Hp>50);True(b.State.Events.Any(e=>e.Type=="Heal"&&e.TargetId=="noa"));
        });
        Test("three enemies own distinct parts and independently addressable forecasts", () => {
            var b=CampaignBattleFactory.Create(CampaignCatalog.Get("c2s04"),seed:0);
            Equal("anger,enemy_2,enemy_3",string.Join(",",b.State.Enemies.Select(u=>u.Id)));
            var parts=b.State.Enemies.SelectMany(u=>u.Parts).ToList();Equal(6,parts.Select(p=>p.Id).Distinct().Count());
            Equal(4,b.State.Intents.Count);Equal(4,b.State.Intents.Select(i=>i.Id).Distinct().Count());
            True(b.Intent("1:anger_arm")!=null);True(b.Intent("1:enemy_2_arm")!=null);True(b.Intent("1:enemy_3_arm")!=null);
            foreach(var intent in b.State.Intents){
                var owner=b.Unit(intent.ActorId);True(owner.IsEnemy);True(owner.Parts.Any(p=>p.Id==intent.PartId));
                Equal(owner.Id,b.TargetUnit(intent.PartId).Id);
            }
        });
        Test("forecasts rotate across all living party members and never name a fallen ally", () => {
            var b=BattleEngine.CreatePrototype(seed:0);var named=new HashSet<string>();
            for(int round=1;round<=4;round++){
                b.State.Round=round;b.BeginCampaign();named.Add(b.State.Intents.First(i=>i.Kind==IntentKind.Normal).TargetId);
            }
            Equal(4,named.Count);True(named.SetEquals(b.State.Allies.Select(u=>u.Id)));
            b.Unit("sera").Hp=0;
            for(int round=5;round<=8;round++){b.State.Round=round;b.BeginCampaign();False(b.State.Intents.Any(i=>i.TargetId=="sera"));}
        });
        Test("slower teammate intercepts a forecast and preserves the original recipient", () => {
            var b=Fixture(1,8,1,new ScriptedRandom());var defender=InterceptAlly(b,20);defender.Skills[0].Speed=1;b.State.Intents[0].Speed=99;
            string error;True(b.TryLink("other",0,"intercept",LinkKind.Clash,"forecast",out error,defender.SkillHand[0].Id));
            Equal("hero",b.Intent("forecast").TargetId);b.ResolveRound();Equal(100,b.Unit("hero").Hp);Equal(100,defender.Hp);
            var take=b.State.Events.Single(e=>e.Type=="Intercept");Equal("hero",take.OriginalTargetId);Equal("forecast",take.IntentId);Equal("other",take.ActorId);
            var hit=b.State.Events.Single(e=>e.Type=="Damage");Equal("other",hit.ActorId);Equal("enemy",hit.TargetId);Equal("hero",hit.OriginalTargetId);Equal("forecast",hit.IntentId);
            Equal(1,defender.DiscardPile.Count);True(b.Intent("forecast").Resolved);
        });
        Test("losing teammate interception takes normal and piercing hits instead of the original ally", () => {
            foreach(var kind in new[]{IntentKind.Normal,IntentKind.Piercing}){
                var b=Fixture(1,20,1,new ScriptedRandom());var defender=InterceptAlly(b,1);b.State.Intents[0].Kind=kind;
                string error;True(b.TryLink("other",0,"intercept",LinkKind.Clash,"forecast",out error));b.ResolveRound();
                Equal(100,b.Unit("hero").Hp);Equal(80,defender.Hp);Equal(1,b.State.Events.Count(e=>e.Type=="Damage"));
                var hit=b.State.Events.Single(e=>e.Type=="Damage");Equal("other",hit.TargetId);Equal("hero",hit.OriginalTargetId);Equal("forecast",hit.IntentId);
            }
        });
        Test("winning one intercepted forecast leaves the same enemy's other attack intact", () => {
            var b=Fixture(1,1,1,new ScriptedRandom());var defender=InterceptAlly(b,20);
            b.State.Intents.Add(new IntentState{Id="second",ActorId="enemy",TargetId="hero",Name="다른 공격",BasePower=9,FlamePower=1,FlameCount=1,Element=Element.Flame,Speed=1});
            string error;True(b.TryLink("other",0,"intercept",LinkKind.Clash,"forecast",out error));b.ResolveRound();
            True(b.Intent("forecast").Resolved);False(b.Intent("second").Cancelled);True(b.Intent("second").Resolved);
            Equal(91,b.Unit("hero").Hp);Equal(100,defender.Hp);Equal(1,b.State.Events.Count(e=>e.Type=="Damage"&&e.ActorId=="enemy"&&e.IntentId=="second"));
        });
        Test("unaffordable interception preserves the existing plan, ember and both hands", () => {
            var b=BattleEngine.CreatePrototype(seed:0);var actor=b.Unit("lian");var sera=b.Unit("sera");
            ForceHand(actor,"lian_breakthrough","lian_glimmer");ForceHand(sera,"sera_slash","sera_glimmer");b.State.Ember=1;string error;
            True(b.TryLink("sera",0,"sera_slash",LinkKind.Strike,"anger",out error,sera.SkillHand[0].Id));
            string lianHand=BagTrace(actor),seraHand=BagTrace(sera);var old=b.State.Links.Single();
            False(b.TryLink("lian",0,"lian_breakthrough",LinkKind.Clash,b.State.Intents[0].Id,out error,actor.SkillHand[0].Id));
            Equal(1,b.State.Links.Count);Equal(old,b.State.Links.Single());Equal(1,b.PlannedCost);Equal(0,b.AvailableEmber);
            Equal(lianHand,BagTrace(actor));Equal(seraHand,BagTrace(sera));
        });
        Test("a dead original recipient cancels an unopposed forecast but a living interceptor still acts", () => {
            var b=Fixture(1,20,1,new ScriptedRandom());var defender=InterceptAlly(b,1);b.Unit("hero").Hp=0;
            b.ResolveRound();Equal(100,defender.Hp);True(b.Intent("forecast").Resolved);
            var cancelled=b.State.Events.Single(e=>e.Type=="Cancel");Equal("forecast",cancelled.IntentId);Equal("hero",cancelled.OriginalTargetId);
            b=Fixture(1,20,1,new ScriptedRandom());defender=InterceptAlly(b,1);string error;
            True(b.TryLink("other",0,"intercept",LinkKind.Clash,"forecast",out error));b.Unit("hero").Hp=0;
            b.ResolveRound();Equal(80,defender.Hp);True(b.State.Events.Any(e=>e.Type=="ClashWin"));
        });
        Test("part break and rift cancel only the owning enemy forecasts", () => {
            foreach(bool breakPart in new[]{true,false}){
                var b=CampaignBattleFactory.Create(CampaignCatalog.Get("c2s04"),seed:0);var foe=b.Unit("enemy_2");
                foreach(var enemy in b.State.Enemies){enemy.Hp=enemy.MaxHp=10000;enemy.Rift=enemy.MaxRift=10000;}
                if(breakPart)foe.Parts[0].Hp=1;else foe.Rift=1;
                ForceHand(b.Unit("sera"),"sera_glimmer","sera_glimmer");string error;
                True(b.TryLink("sera",0,"sera_glimmer",LinkKind.Strike,breakPart?foe.Parts[0].Id:foe.Id,out error));b.ResolveRound();
                True(b.State.Intents.Where(i=>i.ActorId==foe.Id).All(i=>i.Cancelled));
                True(b.State.Intents.Where(i=>i.ActorId!=foe.Id).All(i=>!i.Cancelled));
                True(b.Unit("anger").Parts.All(p=>!p.Destroyed));True(b.Unit("enemy_3").Parts.All(p=>!p.Destroyed));
            }
        });
        Test("enemy defeat preserves other enemies and victory waits for the final enemy", () => {
            var b=CampaignBattleFactory.Create(CampaignCatalog.Get("c2s04"),seed:0);var actor=b.Unit("lian");
            actor.Skills.First(s=>s.Number==1).BasePower=1000;ForceHand(actor,"lian_glimmer","lian_glimmer");string error;
            True(b.TryLink("lian",0,"lian_glimmer",LinkKind.Strike,"enemy_2",out error));b.ResolveRound();
            Equal(BattleOutcome.Ongoing,b.State.Outcome);False(b.Unit("enemy_2").Alive);Equal(2,b.State.Enemies.Count(u=>u.Alive));
            True(b.State.Intents.Where(i=>i.ActorId=="enemy_2").All(i=>i.Cancelled));True(b.State.Intents.Where(i=>i.ActorId!="enemy_2").All(i=>!i.Cancelled));
            b.BeginNextRound();False(b.State.Intents.Any(i=>i.ActorId=="enemy_2"));ForceHand(actor,"lian_glimmer","lian_glimmer");
            True(b.TryLink("lian",0,"lian_glimmer",LinkKind.Strike,"anger",out error,actor.SkillHand[0].Id));
            True(b.TryLink("lian",1,"lian_glimmer",LinkKind.Strike,"enemy_3",out error,actor.SkillHand[1].Id));b.ResolveRound();
            Equal(BattleOutcome.Victory,b.State.Outcome);True(b.State.Enemies.All(u=>!u.Alive));
        });
        Test("reuse retains the same enemy owner and drops a dead owner without reserving cards", () => {
            var b=CampaignBattleFactory.Create(CampaignCatalog.Get("c2s04"),seed:0);var actor=b.Unit("lian");
            foreach(var foe in b.State.Enemies){foe.Hp=foe.MaxHp=10000;foe.Rift=foe.MaxRift=10000;}
            ForceHand(actor,"lian_glimmer","lian_glimmer");string error;
            True(b.TryLink("lian",0,"lian_glimmer",LinkKind.Clash,"1:enemy_2_arm",out error));
            b.ResolveRound();b.BeginNextRound();string hand=BagTrace(actor);b.ReusePreviousLinks();
            var link=b.State.Links.Single();Equal("2:enemy_2_arm",link.TargetId);Equal("enemy_2",link.IntentActorId);Equal("enemy_2_arm",link.IntentPartId);Equal(hand,BagTrace(actor));
            b.ResolveRound();b.Unit("enemy_2").Hp=0;b.BeginNextRound();hand=BagTrace(actor);int ember=b.State.Ember;b.ReusePreviousLinks();
            Equal(0,b.State.Links.Count);Equal(0,b.PlannedCost);Equal(ember,b.AvailableEmber);Equal(hand,BagTrace(actor));
        });
        Test("fresh characters have sanity zero, three numbered skills and one defense", () => {
            foreach (string id in new[] { "lian", "sera", "minato", "noa", "heidel" }) {
                var unit = PrototypeData.CreateCharacter(id); Equal(0, unit.Sanity); Equal(0, unit.SyncLevel);
                Equal("1,2,3", string.Join(",", unit.Skills.Where(x=>x.Number>0).Select(x=>x.Number)));
                Equal(1, unit.Skills.Count(BattleRules.IsDefensive)); True(unit.Skills.All(x=>BattleRules.Flames(x)>=1));
                True(unit.Skills.All(x=>x.Slots==1));
                False(BattleRules.IsSkillUnlocked(unit,unit.Skills.Single(x=>x.Number==3)));
                True(BattleRules.IsSkillUnlocked(PrototypeData.CreateCharacter(id,1),unit.Skills.Single(x=>x.Number==3)));
            }
            Equal(SkillKind.Evade,PrototypeData.CreateCharacter("sera").Skills.Single(BattleRules.IsDefensive).Kind);
            Equal(SkillKind.Counter,PrototypeData.CreateCharacter("heidel").Skills.Single(BattleRules.IsDefensive).Kind);
            False(BattleRules.IsSkillUnlocked(new UnitState(),new SkillData{Number=3}));
        });
        Test("slot occupancy, synchronization lock and budget are atomic", () => {
            var b = BattleEngine.CreatePrototype(seed:1); string error;
            False(b.TryLink("lian",0,"lian_crescendo",LinkKind.Strike,"anger",out error)); Equal(0,b.State.Links.Count);
            b.State.Round=2;BattleRules.ApplyActionProgression(b.State);
            b.Skill("lian","lian_crescendo").Slots=2; // Custom two-action fixture; production skill three uses one action.
            b.Unit("lian").SyncLevel=1; ForceHand(b.Unit("lian"),"lian_crescendo","lian_glimmer"); ForceHand(b.Unit("sera"),"sera_slash","sera_glimmer");
            True(b.TryLink("lian", 0, "lian_crescendo", LinkKind.Strike, "anger", out error)); Equal(3, b.PlannedCost);
            False(b.TryLink("sera", 0, "sera_slash", LinkKind.Strike, "anger", out error)); Equal(1, b.State.Links.Count);
            True(b.TryLink("lian", 1, "lian_glimmer", LinkKind.Strike, "anger", out error)); Equal(0, b.PlannedCost); Equal(1, b.State.Links.Count);
            False(b.TryLink("lian", 1, "lian_crescendo", LinkKind.Strike, "anger", out error)); Equal(0, b.PlannedCost);
            False(b.TryLink("lian", 2, "lian_glimmer", LinkKind.Strike, "anger", out error));
            b.RemoveLink("lian",1); True(b.TryLink("lian", 0, "lian_glimmer", LinkKind.Clash, b.State.Intents[0].Id, out error));
            False(b.TryLink("sera", 0, "sera_glimmer", LinkKind.Clash, b.State.Intents[0].Id, out error));
        });
        Test("support slot and target restrictions", () => {
            var b = BattleEngine.CreatePrototype(); ForceHand(b.Unit("minato"),"minato_heal","minato_glimmer"); b.Unit("minato").IsSupport = true; string error;
            Equal(1, b.Unit("minato").SlotCount);
            False(b.TryLink("minato", 0, "minato_glimmer", LinkKind.Strike, "anger", out error));
            False(b.TryLink("minato", 1, "minato_heal", LinkKind.Support, "lian", out error));
            True(b.TryLink("minato", 0, "minato_heal", LinkKind.Support, "lian", out error));
            False(b.TryLink("lian", 0, "lian_glimmer", LinkKind.Support, "lian", out error));
        });
        Test("sanity chances clamp at ten and ninety percent for both sides", () => {
            Equal(.1,BattleRules.FlameChance(-20)); Equal(.5,BattleRules.FlameChance(0)); Equal(.9,BattleRules.FlameChance(20));
            Equal(.1,BattleRules.FlameChance(-200));Equal(.9,BattleRules.FlameChance(200));
            var b = Fixture(6,6,1,new ScriptedRandom(.89,.91,.89,.91));
            b.Unit("hero").Sanity=20;b.Unit("enemy").Sanity=20; Link(b);b.ResolveRound();
            var roll=b.State.Events.First(x=>x.Type=="FlameRoll");True(roll.FlameStates[0]);False(roll.EnemyFlameStates[0]);
            Equal(20,b.Unit("hero").Sanity);Equal(18,b.Unit("enemy").Sanity);
        });
        Test("speed orders actions but cannot break equal-power clashes", () => {
            Equal(1,BattleRules.CompareHit(10,1,9,100));Equal(0,BattleRules.CompareHit(9,12,9,11));Equal(0,BattleRules.CompareHit(9,11,9,12));
            var random=new ScriptedRandom();var b=Fixture(6,6,1,random);b.Unit("hero").Speed=99;Link(b);b.ResolveRound();
            Equal(100,b.Unit("hero").Hp);Equal(300,b.Unit("enemy").Hp);
            Equal(64,b.State.Events.Count(x=>x.Type=="Tie"));Equal(128,random.Calls);
            True(b.State.Events.Any(x=>x.Type=="Stalemate"));False(b.State.Events.Any(x=>x.Type=="Damage"));
        });
        Test("alternating clash victories continue until two consecutive wins", () => {
            var random=new ScriptedRandom(0,.99,.99,0,0,.99,0,.99);var b=Fixture(6,6,1,random);Link(b);b.ResolveRound();
            Equal("1,-1,1,1",string.Join(",",b.State.Events.Where(x=>x.Type=="Clash").Select(x=>x.Amount)));
            Equal("1,1,1,2",string.Join(",",b.State.Events.Where(x=>x.Type=="Clash").Select(x=>x.WinnerStreak)));
            Equal(100,b.Unit("hero").Hp);Equal(288,b.Unit("enemy").Hp);Equal(8,random.Calls);
            Equal(2,b.Unit("hero").Sanity);Equal(-2,b.Unit("enemy").Sanity);
            Equal(1,b.State.Events.Count(x=>x.Type=="Damage"));
            True(b.State.Events.FindIndex(x=>x.Type=="ClashWin")<b.State.Events.FindIndex(x=>x.Type=="Damage"));
        });
        Test("a tie resets the streak and only the final winning roll deals damage", () => {
            var b=Fixture(6,6,1,new ScriptedRandom(0,.99,.99,.99,0,.99,0,.99));Link(b);b.ResolveRound();
            Equal("1,0,1,2",string.Join(",",b.State.Events.Where(x=>x.Type=="Clash"||x.Type=="Tie").Select(x=>x.WinnerStreak)));
            Equal(288,b.Unit("enemy").Hp);Equal(100,b.Unit("hero").Hp);
        });
        Test("enemy two consecutive wins allow one attack and lower ally sanity", () => {
            var b=Fixture(6,6,1,new ScriptedRandom(.99,0,.99,0));Link(b);b.ResolveRound();
            Equal(88,b.Unit("hero").Hp);Equal(300,b.Unit("enemy").Hp);Equal(-2,b.Unit("hero").Sanity);Equal(2,b.Unit("enemy").Sanity);
            var win=b.State.Events.Single(x=>x.Type=="ClashWin");Equal("enemy",win.ActorId);Equal("hero",win.TargetId);
        });
        Test("unopposed skill rolls once and total power does not multiply by flames", () => {
            var random=new ScriptedRandom(0,0,0,0,0);var b=Fixture(4,6,5,random);b.State.Intents.Clear();string error;
            True(b.TryLink("hero",0,"hit",LinkKind.Strike,"enemy",out error));b.ResolveRound();
            Equal(266,b.Unit("enemy").Hp);Equal(5,random.Calls);Equal(1,b.State.Events.Count(x=>x.Type=="Damage"));
        });
        Test("per-slot ignition grades remain independent of light rolls", () => {
            var b=Fixture(8,9,1,new ScriptedRandom());b.State.Intents.Clear();string error;
            b.State.Round=2;BattleRules.ApplyActionProgression(b.State);
            True(b.TryLink("hero",0,"hit",LinkKind.Strike,"enemy",out error));True(b.TryLink("hero",1,"hit",LinkKind.Strike,"enemy",out error));
            b.ResolveRound(new Dictionary<string,IgniteGrade>{{"hero:0",IgniteGrade.Perfect},{"hero:1",IgniteGrade.Miss}});
            Equal(284,b.Unit("enemy").Hp);Equal(0,b.State.IgniteChain);Equal(3,b.State.Lantern);
        });
        Test("winning area intent applies the final roll to each target once", () => {
            var b=Fixture(1,20,1,new ScriptedRandom());b.Unit("hero").Hp=5;b.State.Intents[0].Kind=IntentKind.Area;
            b.State.Allies.Add(new UnitState{Id="other",Name="동료",Element=Element.Flame,Hp=100,MaxHp=100,Defense=0});
            Link(b);b.ResolveRound();Equal(80,b.Unit("other").Hp);Equal(-3,b.Unit("other").Sanity);
        });
        Test("area clash defeat damages all four party members once", () => {
            var b=Fixture(1,20,1,new ScriptedRandom());b.State.Intents[0].Kind=IntentKind.Area;
            foreach(string id in new[]{"sera","minato","noa"})
                b.State.Allies.Add(new UnitState{Id=id,Name=id,Element=Element.Flame,Hp=100,MaxHp=100,Defense=0});
            Link(b);b.ResolveRound();Equal(4,b.State.Allies.Count);
            foreach(var unit in b.State.Allies){
                Equal(80,unit.Hp);Equal(1,b.State.Events.Count(e=>e.Type=="Damage"&&e.TargetId==unit.Id));
            }
            Equal(4,b.State.Events.Count(e=>e.Type=="Damage"));Equal(1,b.State.Events.Count(e=>e.Type=="ClashWin"));
        });
        Test("unopposed forecast rolls once", () => {
            var b=Fixture(1,9,1,new ScriptedRandom(0));b.ResolveRound();Equal(85,b.Unit("hero").Hp);
        });
        Test("part break cancels forecast and redirects clash", () => {
            var b = BattleEngine.CreatePrototype(); ForceHand(b.Unit("sera"),"sera_glimmer","sera_glimmer"); ForceHand(b.Unit("lian"),"lian_glimmer","lian_glimmer"); b.Unit("anger").Parts[0].Hp = 1; string error;
            True(b.TryLink("sera", 0, "sera_glimmer", LinkKind.Strike, "anger_arm", out error));
            True(b.TryLink("lian", 0, "lian_glimmer", LinkKind.Clash, b.State.Intents[0].Id, out error));
            b.ResolveRound(); True(b.State.Intents[0].Cancelled); True(b.State.Events.Any(x => x.Type == "Relink"));
            True(b.State.Events.Any(x => x.Type == "PartBreak")); b.BeginNextRound(); False(b.State.Intents.Any(x => x.Name == "난타"));
        });
        Test("rift cancels now and skips exactly next round", () => {
            var b = BattleEngine.CreatePrototype(); ForceHand(b.Unit("sera"),"sera_glimmer","sera_glimmer"); b.Unit("anger").Rift = 1; string error;
            True(b.TryLink("sera", 0, "sera_glimmer", LinkKind.Strike, "anger", out error));
            b.ResolveRound(); True(b.State.Intents.All(x => x.Cancelled)); Equal(150, b.Unit("lian").Hp);
            b.BeginNextRound(); Equal(2, b.State.Round); Equal(0, b.State.Intents.Count);
            b.ResolveRound(); b.BeginNextRound(); Equal(3, b.State.Round); Equal(125, b.Unit("anger").Rift); Equal(6, b.State.Intents.Count);
        });
        Test("guard redirects but piercing bypasses", () => {
            var b = BattleEngine.CreatePrototype(); string error; b.State.Intents.RemoveRange(1,b.State.Intents.Count-1); b.State.Intents[0].TargetId = "minato";
            True(b.TryLink("lian", 0, "lian_guard", LinkKind.Guard, "minato", out error)); b.ResolveRound();
            Equal(100, b.Unit("minato").Hp); True(b.Unit("lian").Hp < 150);
            b = BattleEngine.CreatePrototype(); b.State.Intents.RemoveRange(1,b.State.Intents.Count-1); b.State.Intents[0].TargetId = "minato"; b.State.Intents[0].Kind = IntentKind.Piercing;
            True(b.TryLink("lian", 0, "lian_guard", LinkKind.Guard, "minato", out error)); b.ResolveRound();
            True(b.Unit("minato").Hp < 100); Equal(150, b.Unit("lian").Hp);
        });
        Test("support heals and clamps at maximum", () => {
            var b = BattleEngine.CreatePrototype(); ForceHand(b.Unit("minato"),"minato_heal","minato_glimmer"); b.State.Intents.Clear(); b.Unit("lian").Hp = 140; string error;
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
            var b = BattleEngine.CreatePrototype(); ForceHand(b.Unit("sera"),"sera_glimmer","sera_glimmer"); ForceHand(b.Unit("lian"),"lian_glimmer","lian_glimmer"); b.State.Intents.Clear(); string error;
            True(b.TryLink("sera", 0, "sera_glimmer", LinkKind.Strike, "anger", out error));
            True(b.TryLink("lian", 0, "lian_glimmer", LinkKind.Strike, "anger", out error)); b.ResolveRound();
            Equal(0, b.State.Gloom); True(b.State.Events.Any(x => x.Type == "Resonance" && x.Message.Contains("정화")));
        });
        Test("steam reduces explicit enemy flame count before its next roll", () => {
            var b=Fixture(6,6,1,new ScriptedRandom());var intent=b.State.Intents[0];intent.FlameCount=3;intent.HitPowers=new[]{6,6,6};
            var other=new UnitState{Id="other",Name="물결 잔영",Element=Element.Tide,Hp=100,MaxHp=100,Defense=0};
            other.Skills.Add(new SkillData{Id="water",Name="물결",Number=1,Element=Element.Tide,BasePower=6,FlamePower=2,FlameCount=1,Speed=13,RiftDamage=0});
            b.State.Allies.Add(other);ForceHand(other,"water","water");string error;True(b.TryLink("hero",0,"hit",LinkKind.Strike,"enemy",out error));True(b.TryLink("other",0,"water",LinkKind.Strike,"enemy",out error));
            b.ResolveRound();Equal(2,intent.FlameCount);Equal(2,b.State.Events.Single(x=>x.Type=="FlameRoll"&&x.ActorId=="enemy").FlameStates.Length);
            True(b.State.Events.Any(x=>x.Type=="Resonance"&&x.Message.Contains("증기")));
        });
        Test("losing at minimum sanity and winning at maximum stay within limits", () => {
            var b=Fixture(1,20,1,new ScriptedRandom());b.Unit("hero").Sanity=-20;b.Unit("enemy").Sanity=20;Link(b);b.ResolveRound();
            Equal(-20,b.Unit("hero").Sanity);Equal(20,b.Unit("enemy").Sanity);
        });
        Test("auto clears with locked skill3 at Good", () => {
            var b = PlayAuto(); Console.WriteLine("  auto rounds=" + b.State.Round + " surviving=" + b.State.Allies.Count(x => x.Alive));
            Equal(BattleOutcome.Victory, b.State.Outcome); True(b.State.Round <= 12); False(b.State.Links.Any(x=>b.Skill(x.ActorId,x.SkillId).Number==3));
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
        Test("synchronization is atomic, persistent and independent of character level", () => {
            var p=new ProgressService(new ProgressData());string error;False(p.Synchronize("lian",out error));Equal(0,p.Data.Characters.Count);
            p.Data.LanternFragments=10;False(p.Synchronize("missing",out error));Equal(10,p.Data.LanternFragments);
            True(p.Synchronize("lian",out error));Equal(0,p.Data.LanternFragments);Equal(1,p.GetCharacter("lian").SyncLevel);Equal(1,p.GetCharacter("lian").Level);
            p.Data.LanternFragments=10;False(p.Synchronize("lian",out error));Equal(10,p.Data.LanternFragments);
            var b=BattleEngine.CreatePrototype(p.Data,1234);True(BattleRules.IsSkillUnlocked(b.Unit("lian"),b.Skill("lian","lian_crescendo")));
            b.Unit("lian").Sanity=-20;string old=b.State.BattleId;b.Restart();Equal(0,b.Unit("lian").Sanity);Equal(1,b.Unit("lian").SyncLevel);True(old!=b.State.BattleId);
            var d=Path.Combine(Directory.GetCurrentDirectory(),"Tests","test-output","sync-"+Guid.NewGuid().ToString("N"));
            try { var save=new SaveStore(Path.Combine(d,"progress.xml"));save.Save(p.Data);var loaded=new ProgressService(save.Load());Equal(1,loaded.GetCharacter("lian").SyncLevel);Equal(1,loaded.GetCharacter("lian").Level); }
            finally { if(Directory.Exists(d))Directory.Delete(d,true); }
        });
        Test("recommendation and previews refuse locked skills without spending RNG", () => {
            var b=BattleEngine.CreatePrototype(seed:1234);b.RecommendLinks();False(b.State.Links.Any(x=>b.Skill(x.ActorId,x.SkillId).RequiredSyncLevel>0));
            var preview=b.Preview(new LinkCommand{ActorId="lian",SkillId="lian_crescendo",TargetId=b.State.Intents[0].Id,Kind=LinkKind.Clash});
            True(preview.Summary.Contains("잠금"));Equal(PreviewGrade.Disadvantage,preview.Grade);
            var random=new ScriptedRandom();var fixture=Fixture(6,6,1,random);fixture.Preview(new LinkCommand{ActorId="hero",SkillId="hit",TargetId="forecast"});Equal(0,random.Calls);
        });
        Test("illusion previews and API summaries promise no attack without changing state or RNG", () => {
            var random=new ScriptedRandom();var b=Fixture(6,20,1,random);b.Intent("forecast").Kind=IntentKind.Illusion;
            var actor=b.Unit("hero");var card=actor.SkillHand[0];
            var candidate=new LinkCommand{ActorId=actor.Id,Slot=0,SkillId=card.SkillId,CardId=card.Id,Kind=LinkKind.Clash,TargetId="forecast"};
            var first=b.Preview(candidate);Equal(PreviewGrade.Close,first.Grade);Equal(0,first.AllyHits);Equal(0,first.EnemyHits);Equal(0,first.Ties);
            Equal("허상 — 피해 없이 행동만 소모합니다.",first.Summary);False(first.Summary.Contains("2연승"));
            string error;True(b.TryLink(actor.Id,0,card.SkillId,LinkKind.Clash,"forecast",out error,card.Id));
            var json=new JsonSerializerOptions{IncludeFields=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new JsonStringEnumConverter()}};
            string before=JsonSerializer.Serialize(b.State,json);
            for(int i=0;i<10;i++){
                var payload=new{previews=b.State.Links.Where(link=>link.Kind==LinkKind.Clash).Select(link=>new{actorId=link.ActorId,slot=link.Slot,preview=b.Preview(link)})};
                using(var api=JsonDocument.Parse(JsonSerializer.Serialize(payload,json))){
                    var preview=api.RootElement.GetProperty("previews")[0].GetProperty("preview");
                    Equal("Close",preview.GetProperty("grade").GetString());Equal(first.Summary,preview.GetProperty("summary").GetString());
                    Equal(0,preview.GetProperty("allyHits").GetInt32());Equal(0,preview.GetProperty("enemyHits").GetInt32());
                }
                Equal(before,JsonSerializer.Serialize(b.State,json));Equal(0,random.Calls);
            }
            b.ResolveRound();Equal(0,random.Calls);Equal(100,actor.Hp);Equal(300,b.Unit("enemy").Hp);
            Equal(1,b.State.Events.Count(e=>e.Type=="Illusion"));False(b.State.Events.Any(e=>e.Type=="Damage"||e.Type=="FlameRoll"));
            Equal(1,actor.DiscardPile.Count);
        });
        Test("lit guard flames strengthen mitigation", () => {
            Func<double,int> run=roll=> { var b=Fixture(6,20,1,new ScriptedRandom(roll,.99));var hero=b.Unit("hero");
                hero.Skills.Add(new SkillData{Id="guard",Name="방벽",Kind=SkillKind.Guard,Speed=30,BasePower=5,FlamePower=10,FlameCount=1});string error;
                True(b.TryLink("hero",0,"guard",LinkKind.Guard,"hero",out error));b.ResolveRound();return hero.Hp; };
            True(run(0)>run(.99));
        });
        Test("evade compares rolled defense and counter retaliates once after contact", () => {
            var b=Fixture(6,8,1,new ScriptedRandom(0,.99));var hero=b.Unit("hero");
            hero.Skills.Add(new SkillData{Id="evade",Name="회피",Kind=SkillKind.Evade,Speed=30,BasePower=6,FlamePower=6,FlameCount=1});string error;
            True(b.TryLink("hero",0,"evade",LinkKind.Guard,"hero",out error));b.ResolveRound();Equal(100,hero.Hp);True(b.State.Events.Any(x=>x.Type=="Evade"));
            b=Fixture(6,8,1,new ScriptedRandom(0,.99));hero=b.Unit("hero");
            hero.Skills.Add(new SkillData{Id="counter",Name="반격",Kind=SkillKind.Counter,Element=Element.Flame,Speed=30,BasePower=6,FlamePower=6,FlameCount=1,RiftDamage=0});
            True(b.TryLink("hero",0,"counter",LinkKind.Guard,"hero",out error));b.ResolveRound();Equal(93,hero.Hp);Equal(288,b.Unit("enemy").Hp);
            Equal(1,b.State.Events.Count(x=>x.Type=="Counter"));True(b.State.Events.FindIndex(x=>x.Type=="Damage")<b.State.Events.FindIndex(x=>x.Type=="Counter"));
        });
        Test("numbered skill bag preserves three-two-one ratios independently of flames", () => {
            foreach(string id in new[]{"lian","sera","minato","noa","heidel"}) foreach(int sync in new[]{0,1}) {
                var unit=PrototypeData.CreateCharacter(id,sync);var state=new BattleState{RandomSeed=1234};state.Allies.Add(unit);
                new BattleEngine(state);Equal(2,unit.SkillHand.Count);Equal(sync==1?6:5,Bag(unit).Count);
                Equal(3,Bag(unit).Count(x=>x==unit.Skills.Single(k=>k.Number==1).Id));
                Equal(2,Bag(unit).Count(x=>x==unit.Skills.Single(k=>k.Number==2).Id));
                Equal(sync==1?1:0,Bag(unit).Count(x=>x==unit.Skills.Single(k=>k.Number==3).Id));
                True(unit.SkillHand.Select(x=>x.Id).Distinct().Count()==2);False(Bag(unit).Any(x=>unit.Skills.Single(k=>k.Id==x).Number==0));
                Equal(2,unit.Skills.Single(k=>k.Number==1).FlameCount);
            }
        });
        Test("duplicate hand cards reserve different identities and refill only next round", () => {
            var b=DeckBattle(false);var unit=b.Unit("lian");ForceHand(unit,"lian_glimmer","lian_glimmer");string error;
            True(b.TryLink(unit.Id,0,"lian_glimmer",LinkKind.Strike,"anger",out error));
            True(b.TryLink(unit.Id,1,"lian_glimmer",LinkKind.Strike,"anger",out error));
            True(b.State.Links[0].CardId!=b.State.Links[1].CardId);var committed=b.State.Links.Select(x=>x.CardId).ToList();Equal(2,unit.SkillHand.Count);Equal(0,unit.DiscardPile.Count);
            b.ResolveRound();Equal(0,unit.SkillHand.Count);Equal(2,unit.DiscardPile.Count);Equal(5,Bag(unit).Count);
            b.ResolveRound();Equal(2,unit.DiscardPile.Count);b.BeginNextRound();Equal(2,unit.SkillHand.Count);Equal(5,Bag(unit).Count);
            True(unit.SkillHand.All(x=>!committed.Contains(x.Id)));
        });
        Test("explicit card validation and replacements leave rejected plans atomic", () => {
            var b=DeckBattle(false);var unit=b.Unit("lian");ForceHand(unit,"lian_glimmer","lian_breakthrough");string error;
            var first=unit.SkillHand[0];var second=unit.SkillHand[1];string snapshot=BagTrace(unit);
            False(b.TryLink(unit.Id,0,first.SkillId,LinkKind.Strike,"anger",out error,"missing"));
            False(b.TryLink(unit.Id,0,first.SkillId,LinkKind.Strike,"anger",out error,second.Id));Equal(0,b.State.Links.Count);Equal(snapshot,BagTrace(unit));
            True(b.TryLink(unit.Id,0,first.SkillId,LinkKind.Strike,"anger",out error,first.Id));
            False(b.TryLink(unit.Id,1,first.SkillId,LinkKind.Strike,"anger",out error,first.Id));Equal(1,b.State.Links.Count);
            True(b.TryLink(unit.Id,1,second.SkillId,LinkKind.Strike,"anger",out error,second.Id));
            False(b.TryLink(unit.Id,0,second.SkillId,LinkKind.Strike,"anger",out error,second.Id));Equal(2,b.State.Links.Count);Equal(first.Id,b.State.Links[0].CardId);Equal(snapshot,BagTrace(unit));
            True(b.TryLink(unit.Id,0,"lian_guard",LinkKind.Guard,unit.Id,out error));Equal(null,b.State.Links.Single(x=>x.Slot==0).CardId);
            True(b.IsCardAvailable(unit,first.Id));True(b.TryLink(unit.Id,1,first.SkillId,LinkKind.Strike,"anger",out error,first.Id));
            Equal(0,b.PlannedCost);b.ClearLinks();Equal(snapshot,BagTrace(unit));Equal(0,unit.DiscardPile.Count);
        });
        Test("a custom two-slot skill replaces both reservations but consumes one card", () => {
            var b=DeckBattle(true);var unit=b.Unit("lian");ForceHand(unit,"lian_glimmer","lian_crescendo");string error;
            unit.Skills.Single(s=>s.Number==3).Slots=2;
            string third=unit.SkillHand[1].Id;True(b.TryLink(unit.Id,0,"lian_glimmer",LinkKind.Strike,"anger",out error,unit.SkillHand[0].Id));
            True(b.TryLink(unit.Id,1,"lian_guard",LinkKind.Guard,unit.Id,out error));
            True(b.IsCardAvailable(unit,third,0));False(b.IsCardAvailable(unit,third,1));
            True(b.TryLink(unit.Id,0,"lian_crescendo",LinkKind.Strike,"anger",out error,third));Equal(1,b.State.Links.Count);Equal(3,b.PlannedCost);
            b.ResolveRound();Equal(1,unit.SkillHand.Count);Equal("lian_glimmer",unit.SkillHand[0].SkillId);Equal(1,unit.DiscardPile.Count);
        });
        Test("defense and rejected budgets preserve current offers until both expire next round", () => {
            var b=DeckBattle(false);var unit=b.Unit("lian");ForceHand(unit,"lian_glimmer","lian_breakthrough");b.State.Ember=0;string error;
            var oldIds=unit.SkillHand.Select(c=>c.Id).ToArray();
            string trace=BagTrace(unit);False(b.TryLink(unit.Id,0,"lian_breakthrough",LinkKind.Strike,"anger",out error,unit.SkillHand[1].Id));Equal(trace,BagTrace(unit));
            True(b.TryLink(unit.Id,0,"lian_guard",LinkKind.Guard,unit.Id,out error));True(b.TryLink(unit.Id,1,"lian_guard",LinkKind.Guard,unit.Id,out error));
            b.ResolveRound();Equal(trace,BagTrace(unit));b.BeginNextRound();Equal(2,unit.SkillHand.Count);Equal(5,Bag(unit).Count);
            True(unit.SkillHand.All(c=>!oldIds.Contains(c.Id)));Equal(2,unit.DiscardPile.Count);
        });
        Test("every round replaces both offers and preserves the weighted bag after zero one or two used cards", () => {
            foreach(bool sync in new[]{false,true})foreach(int used in new[]{0,1,2}) {
                var b=DeckBattle(sync);var unit=b.Unit("lian");var allIds=new HashSet<string>(unit.SkillHand.Select(c=>c.Id));string error;
                for(int i=0;i<14;i++) {
                    b.State.Intents.Clear();b.State.Ember=6;var old=unit.SkillHand.ToArray();int before=unit.NextCardNumber;
                    for(int action=0;action<used;action++)True(b.TryLink(unit.Id,action,old[action].SkillId,LinkKind.Strike,"anger",out error,old[action].Id));
                    b.ResolveRound();Equal(2-used,unit.SkillHand.Count);int discards=unit.DiscardPile.Count;
                    b.ResolveRound();Equal(discards,unit.DiscardPile.Count);b.BeginNextRound();
                    Equal(2,unit.SkillHand.Count);Equal(before+2,unit.NextCardNumber);True(unit.SkillHand.All(c=>allIds.Add(c.Id)));
                    Equal(sync?6:5,Bag(unit).Count);Equal(3,Bag(unit).Count(x=>x=="lian_glimmer"));Equal(2,Bag(unit).Count(x=>x=="lian_breakthrough"));Equal(sync?1:0,Bag(unit).Count(x=>x=="lian_crescendo"));
                    string trace=BagTrace(unit);foreach(var stale in old){
                        False(b.IsCardAvailable(unit,stale.Id));False(b.TryLink(unit.Id,0,stale.SkillId,LinkKind.Strike,"anger",out error,stale.Id));
                        Equal("이번 손패에 없는 스킬 카드",b.Preview(new LinkCommand{ActorId=unit.Id,Slot=0,SkillId=stale.SkillId,CardId=stale.Id,Kind=LinkKind.Strike,TargetId="anger"}).Summary);
                    }
                    Equal(0,b.State.Links.Count);Equal(trace,BagTrace(unit));
                    b.BeginNextRound();Equal(trace,BagTrace(unit));Equal(before+2,unit.NextCardNumber);
                }
            }
        });
        Test("unused third skill returns to the bag and reuse binds only fresh offered identities", () => {
            var b=DeckBattle(true);var unit=b.Unit("lian");ForceHand(unit,"lian_crescendo","lian_glimmer");var old=unit.SkillHand.ToArray();string error;
            True(b.TryLink(unit.Id,0,old[1].SkillId,LinkKind.Strike,"anger",out error,old[1].Id));b.ResolveRound();b.BeginNextRound();
            True(unit.DiscardPile.Contains("lian_crescendo"));True(unit.SkillHand.All(c=>!old.Any(o=>o.Id==c.Id)));
            string trace=BagTrace(unit);b.ReusePreviousLinks();Equal(trace,BagTrace(unit));Equal(1,b.State.Links.Count);
            var reused=b.State.Links.Single();Equal("lian_glimmer",reused.SkillId);True(reused.CardId!=old[1].Id);
            True(unit.SkillHand.Any(c=>c.Id==reused.CardId&&c.SkillId==reused.SkillId));
        });
        Test("cancelled committed actions discard once even when the actor falls", () => {
            var b=DeckBattle(false);var unit=b.Unit("lian");ForceHand(unit,"lian_glimmer","lian_glimmer");unit.Hp=1;string error;
            b.State.Intents.Add(new IntentState{Id="fast",ActorId="anger",TargetId="lian",Name="선제 공격",Speed=99,Element=Element.Shadow,BasePower=100,FlamePower=1,FlameCount=1});
            True(b.TryLink(unit.Id,0,"lian_glimmer",LinkKind.Strike,"anger",out error));b.ResolveRound();
            Equal(1,unit.SkillHand.Count);Equal(1,unit.DiscardPile.Count);True(b.State.Events.Any(x=>x.Type=="Cancel"));
            b.ResolveRound();Equal(1,unit.DiscardPile.Count);
            string unused=unit.SkillHand.Single().Id;b.BeginNextRound();Equal(0,unit.SkillHand.Count);Equal(2,unit.DiscardPile.Count);Equal(5,Bag(unit).Count);
            False(b.IsCardAvailable(unit,unused));b.BeginNextRound();Equal(2,unit.DiscardPile.Count);
        });
        Test("recommendation, clear, reuse and previews cannot reroll the hand", () => {
            var b=DeckBattle(false);var unit=b.Unit("lian");ForceHand(unit,"lian_glimmer","lian_breakthrough");string initial=BagTrace(unit);
            for(int i=0;i<10;i++){b.RecommendLinks();b.ClearLinks();b.ReusePreviousLinks();b.Preview(new LinkCommand{ActorId=unit.Id,SkillId=unit.SkillHand[0].SkillId,CardId=unit.SkillHand[0].Id,TargetId="missing"});Equal(initial,BagTrace(unit));}
            b.RecommendLinks();foreach(var link in b.State.Links.Where(x=>x.CardId!=null))True(b.Unit(link.ActorId).SkillHand.Any(x=>x.Id==link.CardId));
            b.ResolveRound();b.BeginNextRound();b.State.Intents.Clear();string after=BagTrace(unit);b.ReusePreviousLinks();Equal(after,BagTrace(unit));
            foreach(var link in b.State.Links.Where(x=>x.CardId!=null))True(b.Unit(link.ActorId).SkillHand.Any(x=>x.Id==link.CardId));
        });
        Test("card shuffles are deterministic and never consume flame RNG", () => {
            var random=new ScriptedRandom(0,0);var state=PrototypeData.CreateBattle(seed:31415);var b=new BattleEngine(state,null,random);
            Equal(0,random.Calls);var unit=b.Unit("lian");var same=new BattleEngine(PrototypeData.CreateBattle(seed:31415));
            Equal(BagTrace(unit),BagTrace(same.Unit("lian")));b.RecommendLinks();b.ClearLinks();Equal(0,random.Calls);
            state.Intents.Clear();string error;var card=unit.SkillHand.First();True(b.TryLink(unit.Id,0,card.SkillId,LinkKind.Strike,"anger",out error,card.Id));
            b.ResolveRound();Equal(b.Skill(unit.Id,card.SkillId).FlameCount,random.Calls);b.BeginNextRound();Equal(b.Skill(unit.Id,card.SkillId).FlameCount,random.Calls);
        });
        Test("restarts rebuild the seeded bag and preserve synchronization locks", () => {
            var data=new ProgressData();data.Characters.Add(new CharacterProgress{Id="lian",SyncLevel=1});
            var b=BattleEngine.CreatePrototype(data,99);string initial=BagTrace(b.Unit("lian"));
            b.RecommendLinks();b.ResolveRound();b.Restart();Equal(initial,BagTrace(b.Unit("lian")));Equal(1,b.Unit("lian").SyncLevel);
            Equal(1,Bag(b.Unit("lian")).Count(x=>x=="lian_crescendo"));Equal(0,Bag(b.Unit("sera")).Count(x=>x=="sera_crescendo"));
        });
        Test("motion events identify numbered skill and retain actual winning flames", () => {
            var b=Fixture(6,6,1,new ScriptedRandom(0,.99,0,.99));Link(b);b.ResolveRound();
            var roll=b.State.Events.Last(x=>x.Type=="FlameRoll");var win=b.State.Events.Single(x=>x.Type=="ClashWin");var hit=b.State.Events.Single(x=>x.Type=="Damage");
            Equal("hit",roll.SkillId);Equal("시험",roll.SkillName);Equal(1,roll.SkillNumber);Equal(6,roll.BasePower);Equal(6,roll.FlamePower);Equal(6,roll.EnemyBasePower);
            Equal(roll.ActorPower,win.ActorPower);Equal(win.ActorPower,hit.ActorPower);Equal(win.SkillId,hit.SkillId);Equal(1,hit.SkillNumber);
            Equal(string.Join(",",roll.FlameStates),string.Join(",",hit.FlameStates));
        });
        Test("story preserves names, quotations and prototype flags", () => {
            True(CampaignCatalog.Get("c1s02").Before.Any(x => x.Speaker == "리안" && x.Text == "…움직이지 마. 그림자가 소리에 반응해."));
            True(CampaignCatalog.Get("c1s02").Before.Any(x => x.IsPrototype)); True(CampaignCatalog.Get("c1s07").Before.Any(x => x.Speaker == "미나토"));
            False(CampaignCatalog.Get("c1s01").Before.Any(x => x.Text.Contains("실험체")));
        });
        Console.WriteLine("PASS: " + passed + " tests");
    }

    private sealed class ScriptedRandom : IBattleRandom
    {
        private readonly Queue<double> values; public int Calls;
        public ScriptedRandom(params double[] values) { this.values=new Queue<double>(values); }
        public double NextDouble() { Calls++;return values.Count>0?values.Dequeue():.99; }
    }
    private static UnitState InterceptAlly(BattleEngine battle,int basePower)
    {
        var ally=new UnitState{Id="other",Name="대리 잔영",Hp=100,MaxHp=100,Attack=10,Defense=0,Element=Element.Flame};
        ally.Skills.Add(new SkillData{Id="intercept",Name="대리 맞불",Number=1,BasePower=basePower,FlamePower=1,FlameCount=1,Speed=1,Element=Element.Flame,RiftDamage=0});
        battle.State.Allies.Add(ally);ForceHand(ally,"intercept","intercept");return ally;
    }
    private static BattleEngine Fixture(int allyBase,int enemyBase,int flames,IBattleRandom random)
    {
        var state=new BattleState{Gloom=0};
        var hero=new UnitState{Id="hero",Name="테스트 잔영",Element=Element.Flame,Hp=100,MaxHp=100,Attack=10,Defense=0,Speed=12};
        hero.Skills.Add(new SkillData{Id="hit",Name="시험",Number=1,Element=Element.Flame,BasePower=allyBase,FlamePower=6,FlameCount=flames,Speed=12,RiftDamage=0});
        var enemy=new UnitState{Id="enemy",Name="테스트 군체",IsEnemy=true,Element=Element.Flame,Hp=300,MaxHp=300,Attack=10,Defense=0,Rift=100,MaxRift=100,Speed=11};
        state.Allies.Add(hero);state.Enemies.Add(enemy);
        state.Intents.Add(new IntentState{Id="forecast",ActorId="enemy",TargetId="hero",Name="시험 예고선",BasePower=enemyBase,FlamePower=6,FlameCount=1,Element=Element.Flame,Speed=11});
        return new BattleEngine(state,null,random);
    }
    // Explicit hands isolate rule tests without weakening production card validation.
    private static void ForceHand(UnitState unit, params string[] skills)
    {
        var bag=new List<string>();
        foreach(var skill in unit.Skills.Where(x=>x.Number>=1&&x.Number<=3&&BattleRules.IsSkillUnlocked(unit,x)))
            for(int n=0;n<(skill.Number==1?3:skill.Number==2?2:1);n++)bag.Add(skill.Id);
        unit.SkillHand.Clear();unit.DrawPile.Clear();unit.DiscardPile.Clear();unit.DeckInitialized=true;unit.NextCardNumber=0;
        foreach(string id in skills) {True(bag.Remove(id));unit.SkillHand.Add(new SkillCard{Id=unit.Id+":fixture:"+(++unit.NextCardNumber),SkillId=id});}
        unit.DrawPile.AddRange(bag);
    }
    private static List<string> Bag(UnitState unit)
    { return unit.SkillHand.Select(x=>x.SkillId).Concat(unit.DrawPile).Concat(unit.DiscardPile).ToList(); }
    private static string BagTrace(UnitState unit)
    { return string.Join(",",unit.SkillHand.Select(x=>x.Id+"="+x.SkillId))+"/"+string.Join(",",unit.DrawPile)+"/"+string.Join(",",unit.DiscardPile); }
    private static BattleEngine DeckBattle(bool synchronized)
    {
        var data=new ProgressData();if(synchronized)data.Characters.Add(new CharacterProgress{Id="lian",SyncLevel=1});
        var b=BattleEngine.CreatePrototype(data,1234);b.State.Round=2;BattleRules.ApplyActionProgression(b.State);b.State.Intents.Clear();var enemy=b.Unit("anger");enemy.Hp=enemy.MaxHp=100000;enemy.Rift=enemy.MaxRift=10000;
        foreach(var part in enemy.Parts)part.Hp=part.MaxHp=10000;return b;
    }
    private static void Link(BattleEngine b) { string error; True(b.TryLink("hero", 0, "hit", LinkKind.Clash, "forecast", out error)); }
    private static BattleEngine PlayAuto()
    {
        var b = BattleEngine.CreatePrototype(seed:1234);
        for (int i = 0; i < 15 && b.State.Outcome == BattleOutcome.Ongoing; i++)
        { b.RecommendLinks(); True(b.PlannedCost <= b.State.Ember); b.ResolveRound(); b.BeginNextRound(); }
        return b;
    }
    private static string TraceAuto()
    {
        var b = BattleEngine.CreatePrototype(seed:1234); var trace = new List<string>();
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
