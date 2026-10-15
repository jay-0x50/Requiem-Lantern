using System;
using System.Collections;
using System.IO;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    // An isolated acceptance fixture: pity forces a new Sera followed by nine
    // ordinary duplicate four-stars. Production uses the persisted random stream.
    sealed class RecruitmentSmokeRandom : IRecruitRandom
    {
        int sample;
        public double NextDouble(ref uint state){return sample++%3==0?.9:.1;}
    }
    public partial class LanternApp
    {
        string RecruitmentSmokeSnapshot(){return JsonUtility.ToJson(progress.Data);}
        void RecruitmentSmokeClick(string name,bool modal=false)
        {
            var root=modal?overlay:screen;var node=root.GetComponentsInChildren<Button>().FirstOrDefault(button=>button.name==name);
            if(!node||!node.interactable)throw new Exception("Recruitment control unavailable: "+name);node.onClick.Invoke();
        }
        IEnumerator SmokeCouponFeedbackSteps()
        {
            var args=Environment.GetCommandLineArgs();int captureIndex=Array.IndexOf(args,"--capture-dir");
            string directory=captureIndex>=0&&captureIndex+1<args.Length?args[captureIndex+1]:Path.Combine(Application.persistentDataPath,"QA");
            Directory.CreateDirectory(directory);smokeDirectory=directory;smokePhase="coupon feedback only";smokeDeadline=Time.realtimeSinceStartup+30;
            save=new SaveStore(Path.Combine(directory,"coupon-feedback-progress.xml"));progress=new ProgressService(new ProgressData{PrologueCompleted=true});
            campaign.Exit();reducedMotion=true;ShowHub();ShowSettings();ShowCouponDialog();
            overlay.Find("Coupon input").GetComponent<InputField>().text="INVALID";RecruitmentSmokeClick("Redeem coupon",true);
            if(string.IsNullOrEmpty(overlay.Find("Recruitment notice").GetComponent<Text>().text))throw new Exception("Invalid coupon had no visible error.");
            RecruitmentSmokeClick("Fill materials coupon",true);
            if(!string.IsNullOrEmpty(overlay.Find("Recruitment notice").GetComponent<Text>().text))throw new Exception("Editing the coupon retained the previous invalid-code error.");
            yield return Capture(directory,"09-coupon-input-clears-error");RecruitmentSmokeClick("Redeem coupon",true);
            if(!toastText.Contains("등불 조각")||!toastText.Contains("기억의 파편")||!toastText.Contains("공용 조각")||toastText.Contains("+0")
                ||progress.Data.LanternFragments!=RecruitmentCatalog.TestAmount||progress.Data.MemoryFragments!=RecruitmentCatalog.TestAmount||progress.Data.CharacterShards!=RecruitmentCatalog.TestAmount)
                throw new Exception("Material coupon feedback did not describe its actual three rewards.");
            yield return Capture(directory,"10-materials-coupon-feedback");
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: coupon feedback only, two fresh native screenshots. Editing/filling a coupon after an invalid submission cleared the old validation error. A real material-coupon redemption granted and named all three materials with their actual amount, without unrelated +0 currency text. This narrow check preserved the parent recruitment acceptance report and did not repeat its eight scenarios.\n");
            Debug.Log("REQUIEM_LANTERN_COUPON_FEEDBACK_SMOKE_PASS");Application.Quit(0);
        }
        IEnumerator SmokeRecruitmentSteps()
        {
            var args=Environment.GetCommandLineArgs();int captureIndex=Array.IndexOf(args,"--capture-dir");
            string directory=captureIndex>=0&&captureIndex+1<args.Length?args[captureIndex+1]:Path.Combine(Application.persistentDataPath,"QA");
            Directory.CreateDirectory(directory);smokeDirectory=directory;smokePhase="recruitment and party acceptance";smokeDeadline=Time.realtimeSinceStartup+85;
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"RUNNING: isolated coupon, pickup, ownership, party and persistence checks.\n");
            var validStore=new SaveStore(Path.Combine(directory,"recruitment-progress.xml"));save=validStore;
            progress=new ProgressService(new ProgressData{PrologueCompleted=true});campaign.Exit();assisted=true;autoMode=false;reducedMotion=true;toastText="";toastUntil=0;
            if(!progress.Data.ActiveParty.SequenceEqual(RecruitmentCatalog.StarterParty)||progress.Data.OwnedCharacters.Count!=4
                ||progress.Data.ActiveParty.Count(id=>RecruitmentCatalog.Character(id).Rarity==5)!=1)throw new Exception("The starter roster was not one five-star and three four-stars.");
            int growthCount=progress.Data.Characters.Count;for(int index=0;index<6;index++)ShowRoster(index);ShowRoster(1);
            if(progress.IsOwned("sera")||progress.IsOwned("noa")||progress.Data.Characters.Count!=growthCount
                ||screen.Find("Synchronize sera").GetComponent<Button>().interactable||screen.Find("Equip character sera").GetComponent<Button>().interactable)
                throw new Exception("Merely viewing an unowned character granted ownership/growth or enabled synchronization/equip.");
            yield return Capture(directory,"01-unowned-roster");
            ShowHub();RecruitmentSmokeClick("Recruitment");
            if(screen.Find("Recruit single").GetComponent<Button>().interactable||screen.Find("Recruit ten").GetComponent<Button>().interactable)
                throw new Exception("Insufficient stardust still enabled a summon.");
            ShowHub();RecruitmentSmokeClick("Settings");RecruitmentSmokeClick("Test coupons");
            var input=overlay.Find("Coupon input").GetComponent<InputField>();input.text="NOT-A-COUPON";string before=RecruitmentSmokeSnapshot();RecruitmentSmokeClick("Redeem coupon",true);
            if(before!=RecruitmentSmokeSnapshot()||!overlay.Find("Recruitment notice"))throw new Exception("An invalid coupon changed progress or hid its error.");
            RecruitmentSmokeClick("Fill currency coupon",true);yield return Capture(directory,"02-test-coupon-input");
            var oldRedeem=overlay.GetComponentsInChildren<Button>().First(button=>button.name=="Redeem coupon");oldRedeem.onClick.Invoke();oldRedeem.onClick.Invoke();
            if(progress.Data.Stardust!=RecruitmentCatalog.TestAmount||progress.Data.Coins!=RecruitmentCatalog.TestAmount||progress.Data.RedeemedCodes.Count!=1||overlay)
                throw new Exception("Currency coupon or duplicate-submit protection failed.");
            RecruitmentSmokeClick("Test coupons");RecruitmentSmokeClick("Fill currency coupon",true);before=RecruitmentSmokeSnapshot();RecruitmentSmokeClick("Redeem coupon",true);
            if(before!=RecruitmentSmokeSnapshot()||!overlay.Find("Recruitment notice").GetComponent<Text>().text.Contains("이미"))throw new Exception("An already redeemed coupon was accepted again.");
            RecruitmentSmokeClick("Fill materials coupon",true);RecruitmentSmokeClick("Redeem coupon",true);
            if(progress.Data.LanternFragments!=RecruitmentCatalog.TestAmount||progress.Data.MemoryFragments!=RecruitmentCatalog.TestAmount||progress.Data.CharacterShards!=RecruitmentCatalog.TestAmount)
                throw new Exception("The material coupon did not grant all growth materials.");
            progress=new ProgressService(progress.Capture(),new RecruitmentSmokeRandom());progress.Data.RecruitPity=79;progress.Data.PickupGuaranteed=true;
            ShowRecruitment("sera");yield return Capture(directory,"03-sera-pickup-and-guarantee");
            RecruitmentSmokeClick("Recruit rates");if(!overlay.Find("Rate details").GetComponent<Text>().text.Contains("0.8%"))throw new Exception("The rates details were not reachable.");
            RecruitmentSmokeClick("Close recruitment rates",true);
            int dust=progress.Data.Stardust,shards=progress.Data.CharacterShards;
            var oldTen=screen.Find("Recruit ten").GetComponent<Button>();oldTen.onClick.Invoke();oldTen.onClick.Invoke();
            if(!overlay||progress.Data.Stardust!=dust-RecruitmentCatalog.TenCost||!progress.IsOwned("sera")||progress.Data.CharacterShards!=shards+9*RecruitmentCatalog.DuplicateFourStarShards
                ||overlay.GetComponentsInChildren<RectTransform>().Count(node=>node.name.StartsWith("Recruit result "))!=10)
                throw new Exception("Ten-pull result, NEW acquisition, duplicate shards or repeat-submit guard failed.");
            if(!overlay.Find("Recruit result 0/Acquisition").GetComponent<Text>().text.Contains("NEW")
                ||!overlay.Find("Recruit result 1/Shards").GetComponent<Text>().text.Contains("20"))throw new Exception("The result did not distinguish NEW from duplicate rewards.");
            yield return Capture(directory,"04-ten-pull-new-and-duplicates");RecruitmentSmokeClick("Equip result 0",true);yield return Capture(directory,"05-four-slot-party-assignment");
            var oldAssign=overlay.GetComponentsInChildren<Button>().First(button=>button.name=="Assign party slot 1");oldAssign.onClick.Invoke();oldAssign.onClick.Invoke();
            if(progress.Data.ActiveParty[1]!="sera"||progress.Data.ActiveParty.Distinct().Count()!=4)throw new Exception("Acquired Sera did not replace the selected party slot exactly once.");
            RecruitmentSmokeClick("Close recruitment results",true);ShowRoster(1);
            if(!screen.Find("Synchronize sera").GetComponent<Button>().interactable)throw new Exception("An acquired character remained unavailable for growth.");
            ShowPartyAssignment("lian",()=>ShowRoster(0));RecruitmentSmokeClick("Assign party slot 3",true);
            if(progress.Data.ActiveParty[3]!="lian"||progress.Data.ActiveParty[0]!="kai")throw new Exception("Equipping an existing party member did not exchange the two slots.");
            var loaded=new ProgressService(validStore.Load());
            if(!loaded.IsOwned("sera")||!loaded.Data.ActiveParty.SequenceEqual(progress.Data.ActiveParty)||loaded.Data.RedeemedCodes.Count!=2||loaded.Data.Stardust!=progress.Data.Stardust)
                throw new Exception("Reload lost recruitment, coupons or ordered party state.");
            // A directory deliberately occupies the isolated save filename. All
            // three successful service mutations must roll back if saving fails.
            var original=progress.Capture();string blocked=Path.Combine(directory,"blocked-save");Directory.CreateDirectory(blocked);save=new SaveStore(blocked);
            ShowRecruitment("noa");before=RecruitmentSmokeSnapshot();RecruitmentSmokeClick("Recruit single");
            if(before!=RecruitmentSmokeSnapshot()||overlay||!toastText.Contains("취소"))throw new Exception("Failed summon persistence retained currency/RNG/pity/ownership changes or opened results.");
            progress.Restore(original);progress.Data.RedeemedCodes.Remove(RecruitmentCatalog.TestCurrencyCode);ShowHub();ShowSettings();ShowCouponDialog();RecruitmentSmokeClick("Fill currency coupon",true);
            before=RecruitmentSmokeSnapshot();RecruitmentSmokeClick("Redeem coupon",true);
            if(before!=RecruitmentSmokeSnapshot()||!overlay.Find("Recruitment notice").GetComponent<Text>().text.Contains("취소"))throw new Exception("Failed coupon persistence consumed its code or currency transaction.");
            CloseRecruitmentOverlay();progress.Restore(original);ShowRoster(1);ShowPartyAssignment("sera",()=>ShowRoster(1));before=RecruitmentSmokeSnapshot();RecruitmentSmokeClick("Assign party slot 3",true);
            if(before!=RecruitmentSmokeSnapshot()||!overlay.Find("Recruitment notice").GetComponent<Text>().text.Contains("취소"))throw new Exception("Failed party persistence retained a replacement or hid the error.");
            yield return Capture(directory,"06-save-failure-rolls-back");CloseRecruitmentOverlay();save=validStore;progress.Restore(original);
            var stageData=CampaignCatalog.Get("c2s04");
            foreach(var previous in CampaignCatalog.Chapters.SelectMany(chapter=>chapter.Stages).TakeWhile(stageDataBefore=>stageDataBefore.Id!=stageData.Id))
                if(!progress.Data.ClearedStages.Contains(previous.Id))progress.Data.ClearedStages.Add(previous.Id);
            selectedChapter=2;selectedStage=stageData.Id;ShowStages();
            if(!screen.Find("Participants").GetComponent<Text>().text.Contains("세라")||!screen.Find("Participants").GetComponent<Text>().text.Contains("출격 편성"))throw new Exception("Landing details showed scripted cast instead of the saved battle party.");
            yield return Capture(directory,"07-saved-sortie-party");EnterCampaignStage(stageData.Id);
            while(page=="story"){storyIndex=story.Count;FinishCurrentStory();}
            if(page!="battle"||!battle.State.Allies.Select(unit=>unit.Id).SequenceEqual(progress.Data.ActiveParty))throw new Exception("Actual campaign battle did not field the saved acquired party.");
            AssertSmallBattleAvatars();var sera=battle.Unit("sera");var offered=sera.SkillHand[0];SelectOfferedCard("sera",offered.Id,false);Target(battle.State.Enemies[0].Id);
            if(!battle.State.Links.Any(link=>link.ActorId=="sera"))throw new Exception("The acquired character's offered skill could not be used in planning.");
            ShowPartyAssignment("minato",null);if(overlay)throw new Exception("Party replacement opened during a battle.");
            yield return Capture(directory,"08-acquired-party-in-battle");
            foreach(string shot in new[]{"01-unowned-roster","02-test-coupon-input","03-sera-pickup-and-guarantee","04-ten-pull-new-and-duplicates","05-four-slot-party-assignment","06-save-failure-rolls-back","07-saved-sortie-party","08-acquired-party-in-battle"})
                if(!FreshSmokeScreenshot(Path.Combine(directory,shot+".png")))throw new Exception("Missing fresh recruitment capture: "+shot);
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: eight fresh native recruitment-only screenshots. Starter party was one five-star Lian plus four-star Minato, Heidel and Kai. Passive roster browsing granted nothing and unowned synchronization/equip stayed disabled. Actual settings coupon input rejected invalid/reused codes without mutation and granted each test reward once, including repeated button submission. Banner selection/rate details/pity and shared pickup-guarantee state were visible. A deterministic acceptance fixture forced the guaranteed new Sera followed by nine duplicate four-stars in one real ten-pull service call; results displayed NEW and exact duplicate shards and repeated activation consumed only one batch. Sera replaced the chosen party slot; selecting an already equipped member exchanged slots without duplicates. Reload preserved ownership, ordered party, codes and stardust. Deliberately blocked isolated save paths rolled back summon currency/RNG/pity/ownership, coupon use and party changes, with visible errors and no false result. Landing details used saved party; a real campaign battle fielded that party including acquired Sera and dedicated Kai SD art, and Sera's offered skill created a normal combat link. In-battle party editing was blocked. Player save/settings were untouched.\n");
            Debug.Log("REQUIEM_LANTERN_RECRUITMENT_SMOKE_PASS");Application.Quit(0);
        }
    }
}
