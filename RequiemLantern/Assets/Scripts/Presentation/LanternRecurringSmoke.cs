using System;
using System.Collections;
using System.IO;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        string RecurringSmokeState(){return JsonUtility.ToJson(progress.Data);}
        void AssertRecurringTextLayout()
        {
            foreach(var label in screen.GetComponentsInChildren<Text>()){
                if(label.name!="Supply note"&&label.name!="Dungeon availability"&&label.name!="Dungeon pending rewards"
                    &&!label.name.StartsWith("Quest description ")&&!label.name.StartsWith("Quest reward ")
                    &&!label.name.StartsWith("Dungeon description ")&&!label.name.StartsWith("Dungeon rules ")&&!label.name.StartsWith("Dungeon reward "))continue;
                var rect=label.rectTransform.rect;var settings=label.GetGenerationSettings(rect.size);
                float height=label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text,settings)/label.pixelsPerUnit;
                if(height>rect.height+1)throw new Exception("Recurring text exceeds its allocated height: "+label.name+" "+height+" > "+rect.height);
            }
        }
        void RecurringSmokeClick(string name,bool modal=false)
        {
            var control=(modal?overlay:screen).GetComponentsInChildren<Button>().FirstOrDefault(value=>value.name==name);
            if(!control||!control.interactable)throw new Exception("Unavailable recurring control: "+name);
            control.onClick.Invoke();
        }
        void FinishRecurringSmokeBattle()
        {
            while(battle.State.Outcome==BattleOutcome.Ongoing&&battle.State.Round<80){
                battle.RecommendLinks();battle.ResolveRound(IgniteGrade.Good);
                if(battle.State.Outcome==BattleOutcome.Ongoing)battle.BeginNextRound();
            }
            if(battle.State.Outcome!=BattleOutcome.Victory)throw new Exception("Actual starter party did not win: "+battle.State.StageName);
        }
        IEnumerator SmokeRecurringSteps()
        {
            var args=Environment.GetCommandLineArgs();int captureIndex=Array.IndexOf(args,"--capture-dir");
            string directory=captureIndex>=0&&captureIndex+1<args.Length?args[captureIndex+1]:Path.Combine(Application.persistentDataPath,"QA");
            Directory.CreateDirectory(directory);smokeDirectory=directory;smokePhase="recurring quests and weekly salvage";smokeDeadline=Time.realtimeSinceStartup+120;
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"RUNNING: isolated native recurring activities.\n");
            var currentUtc=new DateTime(2026,10,16,0,0,0,DateTimeKind.Utc);
            var validStore=new SaveStore(Path.Combine(directory,"recurring-progress.xml"));save=validStore;
            progress=new ProgressService(new ProgressData(),utcNow:()=>currentUtc);campaign.Exit();ExitWeeklyDungeon();
            assisted=true;autoMode=false;reducedMotion=true;toastText="";toastUntil=0;recurringNextTick=float.MaxValue;
            ShowHub();
            if(!screen.Find("Supply board")||!screen.Find("Weekly dungeon"))throw new Exception("Hub has no recurring entry points.");
            yield return Capture(directory,"01-hub-supply-entry");RecurringSmokeClick("Weekly dungeon");
            if(screen.GetComponentsInChildren<Button>().Where(value=>value.name.StartsWith("Enter dungeon ")).Any(value=>value.interactable))throw new Exception("Weekly dungeon was available before 1-2 completion.");
            string before=RecurringSmokeState();StartWeeklyDungeon("coast",false);
            if(weeklyDungeon.Active!=null||before!=RecurringSmokeState())throw new Exception("Calling a locked dungeon granted access or rewards.");
            yield return Capture(directory,"02-locked-until-main-story");ShowRecurringBoard();
            var checkin=recurringSnapshot.DailyQuests.First(value=>value.Id=="daily_checkin");
            if(!checkin.CanClaim)throw new Exception("Hub visit did not complete the daily check-in.");
            var staleClaim=screen.GetComponentsInChildren<Button>().First(value=>value.name=="Claim quest daily_checkin");
            int stars=progress.Data.Stardust;staleClaim.onClick.Invoke();staleClaim.onClick.Invoke();
            if(progress.Data.Stardust!=stars+checkin.Reward.Stardust||!progress.GetActivities().DailyQuests.First(value=>value.Id==checkin.Id).Claimed)throw new Exception("Check-in claim or duplicate submission failed.");
            AssertRecurringTextLayout();yield return Capture(directory,"03-daily-claimed-and-progress");

            // Use an actual 1-2 victory and its authored after-story to test the
            // same collection/completion gates as the player's main campaign.
            progress.Data.ClearedStages.Add("c1s01");progress.Data.PrologueCompleted=true;save.Save(progress.Data);
            EnterCampaignStage("c1s02");storyIndex=story.Count;FinishCurrentStory();
            if(page!="battle"||campaign.Phase!=CampaignPhase.Battle)throw new Exception("1-2 did not enter its real battle.");
            FinishRecurringSmokeBattle();
            string blocked=Path.Combine(directory,"blocked-recurring-save");Directory.CreateDirectory(blocked);save=new SaveStore(blocked);
            before=RecurringSmokeState();ShowResult();
            if(before!=RecurringSmokeState()||campaign.Phase!=CampaignPhase.Battle||!battleRewardSavePending||screen.Find("Rewards"))throw new Exception("Failed main victory save advanced rewards, quest progress or after-story.");
            save=validStore;RecurringSmokeClick("Result continue");
            if(battleRewardSavePending||campaign.Phase!=CampaignPhase.AfterStory)throw new Exception("Main victory save did not retry into after-story.");
            RecurringSmokeClick("Result continue");
            if(page!="story"||CampaignRules.IsCleared(progress.Data,CampaignCatalog.Get("c1s02")))throw new Exception("Victory bypassed 1-2 after-story completion.");
            save=new SaveStore(blocked);before=RecurringSmokeState();storyIndex=story.Count;FinishCurrentStory();
            if(before!=RecurringSmokeState()||campaign.Phase!=CampaignPhase.AfterStory||page!="story"||storyDone==null)throw new Exception("Failed after-story save lost its completion checkpoint or retry callback.");
            save=validStore;storyIndex=story.Count;FinishCurrentStory();
            if(!CampaignRules.IsCleared(progress.Data,CampaignCatalog.Get("c1s02")))throw new Exception("After-story save retry did not complete 1-2.");
            ShowWeeklyDungeons();
            if(!recurringSnapshot.DungeonUnlocked||recurringSnapshot.WeeklyDungeonRewardsClaimed!=0)throw new Exception("Actual 1-2 completion did not unlock unspent weekly dungeons.");
            AssertRecurringTextLayout();yield return Capture(directory,"04-three-salvage-difficulties");

            // A real retreat, including its modal control, never consumes a
            // weekly reward and cannot mark a main-story stage complete.
            var cleared=progress.Data.ClearedStages.ToArray();int rewarded=progress.Data.RewardedStages.Count;
            StartWeeklyDungeon("coast",false);ConfirmRetreat();RecurringSmokeClick("Leave",true);
            if(weeklyDungeon.Active!=null||campaign.Active!=null||progress.GetActivities().WeeklyDungeonRewardsClaimed!=0)throw new Exception("Retreat consumed salvage rewards or retained a story session.");
            foreach(string id in new[]{"coast","reef","watch"}){
                StartWeeklyDungeon(id,false);
                if(campaign.Active!=null||battle!=weeklyDungeon.Battle||!battle.State.Allies.Select(value=>value.Id).SequenceEqual(RecruitmentCatalog.StarterParty))throw new Exception("Salvage mixed with campaign or ignored the saved four-person starter party.");
                if(id=="reef")yield return Capture(directory,"05-three-enemy-weekly-battle");
                if(id=="watch")yield return Capture(directory,"06-weekly-boss-intents");
                FinishRecurringSmokeBattle();int previousClaims=progress.GetActivities().WeeklyDungeonRewardsClaimed;
                before=RecurringSmokeState();ShowResult();
                if(before!=RecurringSmokeState()||page!="result")throw new Exception("Opening a salvage result granted rewards before manual collection.");
                if(id=="coast"){
                    save=new SaveStore(blocked);var sameSession=weeklyDungeon.Battle;
                    RecurringSmokeClick("Dungeon result continue");
                    if(before!=RecurringSmokeState()||weeklyDungeon.Battle!=sameSession||page!="result"||!toastText.Contains("취소"))throw new Exception("Failed salvage collection changed progress, ended the battle or hid retry.");
                    yield return Capture(directory,"07-reward-save-failure-retry");save=validStore;
                }
                var oldCollect=screen.GetComponentsInChildren<Button>().First(value=>value.name=="Dungeon result continue");
                oldCollect.onClick.Invoke();string collected=RecurringSmokeState();oldCollect.onClick.Invoke();
                if(weeklyDungeon.Active!=null||page!="weeklyDungeon"||progress.GetActivities().WeeklyDungeonRewardsClaimed!=previousClaims+1||collected!=RecurringSmokeState())throw new Exception("Salvage reward was not once-only, saved, or returned to route selection.");
                if(!cleared.SequenceEqual(progress.Data.ClearedStages)||progress.Data.RewardedStages.Count!=rewarded)throw new Exception("Weekly salvage changed the main-story completion/reward collections.");
            }
            ShowRecurringBoard(true);var weeklyReward=recurringSnapshot.WeeklyQuests.First(value=>value.Id=="weekly_dungeon");
            if(!weeklyReward.CanClaim||weeklyReward.Current!=3)throw new Exception("Shared three-reward target did not become collectible.");
            RecurringSmokeClick("Claim quest weekly_dungeon");AssertRecurringTextLayout();yield return Capture(directory,"08-weekly-quest-reward-claimed");
            ShowWeeklyDungeons();
            if(screen.GetComponentsInChildren<Button>().Where(value=>value.name.StartsWith("Enter dungeon ")).Any(value=>!value.GetComponent<OrnateButton>().Caption.text.Contains("연습")))throw new Exception("Exhausted weekly rewards still advertised reward sorties.");
            RecurringSmokeClick("Enter dungeon coast");
            if(!weeklyDungeon.Practice)throw new Exception("The explicitly labelled practice action entered a reward battle.");
            FinishRecurringSmokeBattle();ShowResult();yield return Capture(directory,"09-practice-result");
            int coins=progress.Data.Coins,dust=progress.Data.Stardust,lantern=progress.Data.LanternFragments,memory=progress.Data.MemoryFragments,shards=progress.Data.CharacterShards;
            string activityBefore=JsonUtility.ToJson(progress.Data.Activities);RecurringSmokeClick("Dungeon result continue");
            if(coins!=progress.Data.Coins||dust!=progress.Data.Stardust||lantern!=progress.Data.LanternFragments||memory!=progress.Data.MemoryFragments||shards!=progress.Data.CharacterShards||activityBefore!=JsonUtility.ToJson(progress.Data.Activities))throw new Exception("Practice increased resources or quest metrics.");

            var old=progress.GetActivities();currentUtc=old.NextDailyResetUtc.AddSeconds(1);ShowRecurringBoard();
            if(recurringSnapshot.DailyKey==old.DailyKey||recurringSnapshot.WeeklyKey!=old.WeeklyKey||recurringSnapshot.WeeklyDungeonRewardsClaimed!=3||!recurringSnapshot.DailyQuests.First(value=>value.Id=="daily_checkin").CanClaim)throw new Exception("Daily KST reset changed weekly progress or failed to offer the new check-in.");
            currentUtc=recurringSnapshot.NextWeeklyResetUtc.AddSeconds(1);ShowRecurringBoard(true);
            if(recurringSnapshot.WeeklyKey==old.WeeklyKey||recurringSnapshot.WeeklyDungeonRewardsClaimed!=0||recurringSnapshot.WeeklyQuests.Any(value=>value.Claimed))throw new Exception("Monday KST reset did not restore the shared weekly allowance and clear old claims.");
            yield return Capture(directory,"10-next-week-reset");
            string futureState=RecurringSmokeState();currentUtc=new DateTime(2026,10,16,0,0,0,DateTimeKind.Utc);ShowRecurringBoard(true);
            if(futureState!=RecurringSmokeState())throw new Exception("A backwards device clock reopened an already consumed period.");
            var loaded=new ProgressService(validStore.Load(),utcNow:()=>currentUtc);
            if(JsonUtility.ToJson(loaded.Data)!=RecurringSmokeState())throw new Exception("Reload did not preserve the final recurring state.");
            string[] shots={"01-hub-supply-entry","02-locked-until-main-story","03-daily-claimed-and-progress","04-three-salvage-difficulties","05-three-enemy-weekly-battle","06-weekly-boss-intents","07-reward-save-failure-retry","08-weekly-quest-reward-claimed","09-practice-result","10-next-week-reset"};
            if(shots.Any(shot=>!FreshSmokeScreenshot(Path.Combine(directory,shot+".png"))))throw new Exception("Missing fresh native recurring screenshots.");
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: ten fresh native captures; isolated save and injected UTC clock only. Hub entry, four daily/four weekly quest rows, check-in claim and repeated old-button rejection; 1-2 locked dungeon controls and direct-call guard; real 1-2 victory/after-story gates with two-phase save failure rollback and retry; all three actual weekly dungeons won with the unchanged acquired starter party and no forced combat outcome. Explicit retreat consumed no reward. Salvage results granted nothing until collection; failed save restored all resources/metrics/claim counts while retaining the winning session and retry; successful collection saved exactly once and closed the session. All difficulties shared the three-reward limit; weekly reward quest became collectible and was claimed. Exhausted routes showed practice; real practice victory added no resource or quest progress. Daily 05:00 KST and Monday 05:00 KST resets, backward-clock monotonic keys, reload, and main-story collection isolation passed. Player saves/settings were untouched.\n");
            Debug.Log("REQUIEM_LANTERN_RECURRING_SMOKE_PASS");Application.Quit(0);
        }
    }
}
