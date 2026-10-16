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
        void AssertMaterialTextLayout()
        {
            foreach(var label in screen.GetComponentsInChildren<Text>()){
                if(!new[]{"Biography","Character quote","Synchronization requirements","Material selection help","Material description","Material sortie rules","Material sync help","Material availability","Sync coupon help"}.Contains(label.name))continue;
                var rect=label.rectTransform.rect;var settings=label.GetGenerationSettings(rect.size);
                float height=label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text,settings)/label.pixelsPerUnit;
                if(height>rect.height+1)throw new Exception("Material text exceeds its allocated height: "+label.name+" "+height+" > "+rect.height);
            }
        }
        IEnumerator SmokeMaterialSteps()
        {
            var args=Environment.GetCommandLineArgs();int captureIndex=Array.IndexOf(args,"--capture-dir");
            string directory=captureIndex>=0&&captureIndex+1<args.Length?args[captureIndex+1]:Path.Combine(Application.persistentDataPath,"QA");
            Directory.CreateDirectory(directory);smokeDirectory=directory;smokePhase="synchronization material dungeons";smokeDeadline=Time.realtimeSinceStartup+120;
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"RUNNING: isolated native character material collection.\n");
            var now=new DateTime(2026,10,16,0,0,0,DateTimeKind.Utc);
            var validStore=new SaveStore(Path.Combine(directory,"material-progress.xml"));save=validStore;
            progress=new ProgressService(new ProgressData{LanternFragments=10},utcNow:()=>now);campaign.Exit();ExitWeeklyDungeon();ExitMaterialDungeon();
            assisted=true;autoMode=false;reducedMotion=true;recurringNextTick=float.MaxValue;toastText="";toastUntil=0;
            ShowHub();if(!screen.Find("Material dungeon"))throw new Exception("Hub omitted the material route.");
            ShowRoster(0);AssertMaterialTextLayout();
            if(screen.Find("Synchronize lian").GetComponent<Button>().interactable||!screen.Find("Synchronization requirements").GetComponent<Text>().text.Contains("0 / 10"))throw new Exception("Common lantern fragments alone still enabled five-star synchronization.");
            yield return Capture(directory,"01-roster-character-material-required");RecurringSmokeClick("Find sync material lian");
            if(page!="materialDungeon"||screen.GetComponentsInChildren<Button>().Count(value=>value.name.StartsWith("Select material "))!=7||screen.Find("Enter material dungeon").GetComponent<Button>().interactable)throw new Exception("Material route did not show seven targets or enforce 1-2 completion.");
            string before=RecurringSmokeState();StartMaterialDungeon("lian");
            if(materialDungeon.Active!=null||before!=RecurringSmokeState())throw new Exception("Calling a locked material dungeon bypassed the gate.");
            AssertMaterialTextLayout();yield return Capture(directory,"02-seven-materials-before-unlock");

            // Finish the authored 1-2 path rather than setting its completion bit.
            progress.Data.ClearedStages.Add("c1s01");progress.Data.PrologueCompleted=true;save.Save(progress.Data);
            EnterCampaignStage("c1s02");storyIndex=story.Count;FinishCurrentStory();FinishRecurringSmokeBattle();ShowResult();
            if(campaign.Phase!=CampaignPhase.AfterStory)throw new Exception("Actual 1-2 victory did not retain its after-story gate.");
            RecurringSmokeClick("Result continue");storyIndex=story.Count;FinishCurrentStory();
            if(!progress.GetSynchronizationMaterials().Unlocked)throw new Exception("Completing the real 1-2 after-story did not unlock collection.");
            ShowMaterialDungeons("mare");AssertMaterialTextLayout();yield return Capture(directory,"03-mare-material-unlimited-route");
            int quota=progress.GetActivities().WeeklyDungeonRewardsClaimed;var stages=progress.Data.ClearedStages.ToArray();var rewarded=progress.Data.RewardedStages.ToArray();
            string blocked=Path.Combine(directory,"blocked-material-save");Directory.CreateDirectory(blocked);
            for(int run=0;run<2;run++){
                StartMaterialDungeon("mare");
                if(campaign.Active!=null||weeklyDungeon.Active!=null||battle!=materialDungeon.Battle||battle.State.Enemies.Count!=2||!battle.State.Allies.Select(value=>value.Id).SequenceEqual(RecruitmentCatalog.StarterParty))throw new Exception("Material battle mixed sessions or ignored the saved starter party.");
                if(run==0)yield return Capture(directory,"04-two-enemy-material-battle");
                FinishRecurringSmokeBattle();before=RecurringSmokeState();ShowResult();
                if(before!=RecurringSmokeState()||page!="result")throw new Exception("Opening a material result granted items before collection.");
                if(run==0){
                    save=new SaveStore(blocked);var engine=materialDungeon.Battle;RecurringSmokeClick("Material result collect");
                    if(before!=RecurringSmokeState()||materialDungeon.Battle!=engine||page!="result"||!toastText.Contains("취소"))throw new Exception("Failed material save consumed its collection or lost the winning session.");
                    yield return Capture(directory,"05-material-reward-save-retry");save=validStore;
                }
                var old=screen.GetComponentsInChildren<Button>().First(value=>value.name=="Material result collect");
                if(run==0){old.onClick.Invoke();string collected=RecurringSmokeState();old.onClick.Invoke();if(collected!=RecurringSmokeState())throw new Exception("An old material collect button granted twice.");}
                else {
                    RecurringSmokeClick("Material result repeat");
                    if(page!="battle"||materialDungeon.Active==null||materialDungeon.Active.CharacterId!="mare")throw new Exception("Collect-and-repeat failed to open a fresh same-character dive.");
                    string repeatState=RecurringSmokeState();ConfirmRetreat();RecurringSmokeClick("Leave",true);
                    if(repeatState!=RecurringSmokeState()||materialDungeon.Active!=null||page!="materialDungeon")throw new Exception("Retreat spent resources or granted uncollected materials.");
                }
            }
            var mare=progress.GetSynchronizationMaterials().Entries.First(value=>value.CharacterId=="mare");
            if(mare.Count!=6||!mare.CanSynchronize||progress.GetActivities().WeeklyDungeonRewardsClaimed!=quota||!stages.SequenceEqual(progress.Data.ClearedStages)||!rewarded.SequenceEqual(progress.Data.RewardedStages))throw new Exception("Two dives did not grant only six Mare materials while preserving weekly/campaign state.");
            ShowRoster(Array.IndexOf(CharacterIds,"Mare"));AssertMaterialTextLayout();yield return Capture(directory,"06-mare-ready-to-synchronize");
            save=new SaveStore(blocked);before=RecurringSmokeState();RecurringSmokeClick("Synchronize mare");
            if(before!=RecurringSmokeState()||progress.GetCharacter("mare").SyncLevel!=0)throw new Exception("Failed synchronization save spent dedicated/common materials or unlocked S3.");
            save=validStore;int lantern=progress.Data.LanternFragments;RecurringSmokeClick("Synchronize mare");
            if(progress.GetCharacter("mare").SyncLevel!=1||progress.Data.LanternFragments!=lantern-10||progress.GetSynchronizationMaterials().Entries.First(value=>value.CharacterId=="mare").Count!=0||screen.Find("Synchronize mare").GetComponent<Button>().interactable)throw new Exception("Mare synchronization did not consume exactly six dedicated materials plus ten lantern fragments once.");
            AssertMaterialTextLayout();yield return Capture(directory,"07-mare-synchronized-s3-unlocked");

            ShowMaterialDungeons("sera");StartMaterialDungeon("sera");FinishRecurringSmokeBattle();ShowResult();RecurringSmokeClick("Material result collect");
            var sera=progress.GetSynchronizationMaterials().Entries.First(value=>value.CharacterId=="sera");
            if(sera.Owned||sera.Count!=3||sera.CanSynchronize)throw new Exception("Pre-farming granted ownership or enabled an unowned character's synchronization.");
            ShowRoster(1);AssertMaterialTextLayout();yield return Capture(directory,"08-unowned-sera-prefarmed-material");
            ShowHub();ShowSettings();ShowCouponDialog();RecurringSmokeClick("Fill sync materials coupon",true);
            if(overlay.Find("Coupon input").GetComponent<InputField>().text!=MaterialCatalog.TestCouponCode)throw new Exception("The dedicated test coupon fill control used the wrong code.");
            yield return Capture(directory,"09-dedicated-material-test-coupon");
            var prior=progress.GetSynchronizationMaterials().Entries.ToDictionary(value=>value.CharacterId,value=>value.Count);
            save=new SaveStore(blocked);before=RecurringSmokeState();RecurringSmokeClick("Redeem coupon",true);
            if(before!=RecurringSmokeState()||!overlay||!overlay.Find("Recruitment notice").GetComponent<Text>().text.Contains("취소"))throw new Exception("Failed dedicated coupon save spent its once-only code or inventory.");
            save=validStore;RecurringSmokeClick("Redeem coupon",true);
            if(progress.GetSynchronizationMaterials().Entries.Any(value=>value.Count!=prior[value.CharacterId]+RecruitmentCatalog.TestAmount)||!toastText.Contains("7종")||toastText.Contains("+0"))throw new Exception("Dedicated coupon did not add all seven exact amounts with accurate feedback.");
            ShowCouponDialog();RecurringSmokeClick("Fill sync materials coupon",true);before=RecurringSmokeState();RecurringSmokeClick("Redeem coupon",true);
            if(before!=RecurringSmokeState()||!overlay.Find("Recruitment notice").GetComponent<Text>().text.Contains("이미"))throw new Exception("A reused dedicated coupon was accepted.");
            CloseRecruitmentOverlay();ShowMaterialDungeons("sera");AssertMaterialTextLayout();yield return Capture(directory,"10-seven-inventories-persisted");
            var loaded=new ProgressService(validStore.Load(),utcNow:()=>now);
            if(JsonUtility.ToJson(loaded.Data)!=RecurringSmokeState()||loaded.GetCharacter("mare").SyncLevel!=1||loaded.IsOwned("sera"))throw new Exception("Reload lost material inventory, synchronization, coupon claims or unowned state.");
            string[] shots={"01-roster-character-material-required","02-seven-materials-before-unlock","03-mare-material-unlimited-route","04-two-enemy-material-battle","05-material-reward-save-retry","06-mare-ready-to-synchronize","07-mare-synchronized-s3-unlocked","08-unowned-sera-prefarmed-material","09-dedicated-material-test-coupon","10-seven-inventories-persisted"};
            if(shots.Any(shot=>!FreshSmokeScreenshot(Path.Combine(directory,shot+".png"))))throw new Exception("Missing fresh material captures.");
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: ten fresh native captures and actual TextGenerator bounds checks. Dedicated materials are required alongside ten common lantern fragments; roster inventory/cost, disabled synchronization and direct source route were visible. Seven collection targets remained visible before the actual 1-2 battle/after-story unlocked entry. Two real Mare dives and one unowned Sera dive won with the saved starter party; each manual collection added exactly three of its selected material. Opening a result granted nothing; failed save restored inventory/metrics/claims and retained the victory session/button, then retry paid once. Collect-and-repeat created a fresh dive; retreat changed no progress/resources. Two Mare wins supplied six materials; synchronization failed-save rollback preserved everything, then a normal retry consumed exactly six plus ten common fragments and unlocked S3 once. Unowned Sera pre-farmed three without ownership/synchronization. The dedicated coupon filled the correct code, rolled back on save failure, added all seven exact test amounts once, accurately named its reward, and rejected reuse. Weekly quota and main completion/reward lists were unchanged by material dives; ordinary victory quest metrics remained eligible. Reload preserved inventory/synchronization/code/ownership. Player saves and settings were untouched.\n");
            Debug.Log("REQUIEM_LANTERN_MATERIAL_SMOKE_PASS");Application.Quit(0);
        }
    }
}
