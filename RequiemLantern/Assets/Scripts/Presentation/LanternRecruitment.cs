using System;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        string selectedRecruitBanner="lian";
        bool recruitmentBusy;
        bool RecruitmentAllowed(){return page!="battle"&&page!="resolve"&&campaign.Phase!=CampaignPhase.Battle;}
        bool PersistRecruitment()
        {
            try{save.Save(progress.Data);return true;}
            catch(Exception error){Debug.LogWarning("Recruitment save could not complete: "+error.Message);return false;}
        }
        void CloseRecruitmentOverlay(){if(overlay){overlay.gameObject.SetActive(false);Destroy(overlay.gameObject);overlay=null;}}
        void DismissRecruitmentOverlay(){CloseRecruitmentOverlay();recruitmentBusy=false;ShowRecruitment(selectedRecruitBanner);}
        void RecruitmentNotice(string message)
        {
            if(!overlay){Notify(message);return;}
            var existing=overlay.Find("Recruitment notice");var label=existing?existing.GetComponent<Text>():Label(overlay,"Recruitment notice","",260,967,1400,59,23,Gold,TextAnchor.MiddleCenter);label.text=message;
        }
        RectTransform RecruitmentModal(string name,string title,float x=140,float y=135,float width=1640,float height=815)
        {
            CloseRecruitmentOverlay();overlay=MakeRect(stage,name,0,0,1920,1080);
            Box(overlay,"Scrim",0,0,1920,1080,new Color(.005f,.009f,.018f,.89f)).raycastTarget=true;
            Box(overlay,"Paper",x,y,width,height,new Color(.035f,.055f,.083f,.99f));
            Box(overlay,"Gold edge",x,y,width,2,new Color(Gold.r,Gold.g,Gold.b,.64f));
            Label(overlay,"Heading",title,x+42,y+25,width-84,66,37,Gold);
            return overlay;
        }
        void ShowRecruitment(string bannerId=null)
        {
            if(!RecruitmentAllowed()){Notify("전투를 마친 뒤 잔영을 소환해 주세요.");return;}
            if(bannerId!=null)selectedRecruitBanner=bannerId;
            var banner=RecruitmentCatalog.Banners.FirstOrDefault(value=>value.Id==selectedRecruitBanner)??RecruitmentCatalog.Banners[0];selectedRecruitBanner=banner.Id;
            var featured=RecruitmentCatalog.Characters.First(value=>value.Id==banner.FeaturedCharacterId);
            recruitmentBusy=false;campaign.Exit();Clear("recruitment");Header("ECHOES OF THE LANTERN / PICKUP SUMMON","잔영 소환",ShowHub);
            for(int i=0;i<RecruitmentCatalog.Banners.Count;i++){
                var option=RecruitmentCatalog.Banners[i];var character=RecruitmentCatalog.Characters.First(value=>value.Id==option.FeaturedCharacterId);
                Button(screen,"Recruit banner "+option.Id,character.Name+" · ★★★★★ 픽업",100+i*580,148,560,68,()=>ShowRecruitment(option.Id),option.Id==banner.Id);
            }
            Box(screen,"Banner portrait plate",82,244,790,729,new Color(.018f,.032f,.054f,.53f));
            Character(screen,featured.Portrait,116,248,710,714);
            Label(screen,"Pickup banner eyebrow","PICKUP  /  ★★★★★",944,259,830,34,21,Gold);
            Label(screen,"Pickup character name",featured.Name,938,304,840,109,74,Ivory);
            Label(screen,"Pickup character role",featured.Role,948,421,830,45,28,Gold);
            Label(screen,"Pickup character ownership",progress.IsOwned(featured.Id)?"보유한 잔영 · 중복 획득 시 공용 조각 100개":"아직 만나지 못한 잔영 · 첫 획득 시 편성 가능",949,481,820,48,23,Muted);
            Box(screen,"Recruit status plate",945,550,827,162,new Color(.035f,.055f,.081f,.96f));
            Label(screen,"Recruit pity",$"5성 확정까지  {RecruitmentCatalog.HardPity-progress.Data.RecruitPity}회",971,566,770,48,29,Gold);
            Label(screen,"Recruit guarantee",progress.Data.PickupGuaranteed?"다음 5성은 선택한 픽업 잔영 확정":"5성 등장 시 선택한 픽업 잔영 확률 50%",974,621,770,39,23,Ivory);
            Label(screen,"Recruit shared guarantee","천장과 다음 픽업 확정은 모든 배너가 공유합니다.",975,671,770,29,18,Muted);
            Label(screen,"Recruit stardust",$"별가루  {progress.Data.Stardust:N0}",948,738,820,53,34,Gold);
            int version=screenVersion;
            var single=Button(screen,"Recruit single","1회 소환 · 별가루 160",946,813,399,82,()=>RecruitFromBanner(banner.Id,1,version),true);
            var ten=Button(screen,"Recruit ten","10회 소환 · 별가루 1,600",1361,813,410,82,()=>RecruitFromBanner(banner.Id,10,version),true);
            single.interactable=progress.Data.Stardust>=RecruitmentCatalog.SingleCost;ten.interactable=progress.Data.Stardust>=RecruitmentCatalog.TenCost;
            Button(screen,"Recruit rates","확률 및 소환 안내",948,920,399,54,ShowRecruitmentRates);
            Button(screen,"Recruit roster","보유 잔영 · 파티 편성",1363,920,408,54,()=>ShowRoster(0));
            Footer("5성 0.8% · 80회 이내 5성 확정 · 10회는 1회 소환을 10번 진행합니다");
        }
        void RecruitFromBanner(string bannerId,int count,int version)
        {
            if(page!="recruitment"||version!=screenVersion||overlay||recruitmentBusy||!RecruitmentAllowed())return;
            recruitmentBusy=true;var checkpoint=progress.Capture();
            if(!progress.Recruit(bannerId,count,out var result,out string error)){recruitmentBusy=false;Notify(error);return;}
            if(!PersistRecruitment()){progress.Restore(checkpoint);recruitmentBusy=false;ShowRecruitment(bannerId);Notify("저장하지 못해 소환을 취소했습니다. 별가루와 획득 내역은 바뀌지 않았습니다.");return;}
            ShowRecruitmentResults(result);
        }
        void ShowRecruitmentResults(RecruitmentResult result)
        {
            if(page!="recruitment")return;
            var modal=RecruitmentModal("Recruitment results","등불에 닿은 잔영");
            Label(modal,"Result cost",$"별가루 {result.Cost:N0} 사용  ·  현재 {progress.Data.Stardust:N0}  ·  공용 조각 {progress.Data.CharacterShards:N0}",184,235,1550,42,23,Muted);
            for(int i=0;i<result.Pulls.Count;i++){
                var pull=result.Pulls[i];float x=result.Pulls.Count==1?815:175+(i%5)*315,y=result.Pulls.Count==1?365:303+(i/5)*287;
                var card=MakeRect(modal,"Recruit result "+i,x,y,290,266);
                Box(card,"Card backing",0,0,290,266,new Color(.06f,.087f,.125f,1));Box(card,"Rarity edge",0,0,290,3,pull.Rarity==5?Gold:Violet);
                var character=RecruitmentCatalog.Characters.First(value=>value.Id==pull.CharacterId);
                Character(card,character.Portrait,9,21,111,177,false);
                Label(card,"Name",pull.Name,128,21,154,46,29);
                Label(card,"Rarity",new string('★',pull.Rarity),128,78,152,32,19,pull.Rarity==5?Gold:Violet);
                Label(card,"Acquisition",pull.IsNew?"NEW":"중복",128,119,152,36,25,pull.IsNew?Gold:Muted);
                Label(card,"Shards",pull.IsNew?"편성할 수 있습니다.":"공용 조각\n+"+pull.DuplicateShards,128,159,152,48,18,Muted);
                string id=pull.CharacterId;Button(card,"Equip result "+i,"파티 편성",23,209,244,44,()=>ShowPartyAssignment(id,()=>ShowRecruitmentResults(result)));
            }
            Button(modal,"Close recruitment results","확인",1433,873,287,61,DismissRecruitmentOverlay,true);
            Label(modal,"Result save status","획득 결과가 저장되었습니다. 새 잔영은 편성한 뒤 출격에 사용할 수 있습니다.",185,885,1170,38,21,Muted);
        }
        void ShowRecruitmentRates()
        {
            if(page!="recruitment"||overlay)return;
            var modal=RecruitmentModal("Recruitment rates","잔영 소환 확률과 보장",290,193,1340,680);
            string fourStars=string.Join(" · ",RecruitmentCatalog.Characters.Where(character=>character.Recruitable&&character.Rarity==4).Select(character=>character.Name));
            Label(modal,"Rate details","기본 확률  ·  5성 0.8% / 4성 99.2%\n5성 등장 시  ·  선택한 픽업 50% / 나머지 5성 각 25%\n4성 등장 시  ·  "+fourStars+" 중 균등 추첨\n\n80회 이내 5성 확정  ·  5성이 나오면 천장 횟수가 초기화됩니다.\n픽업 외 5성 획득 후, 다음 5성은 선택한 배너의 픽업 잔영입니다.\n배너를 바꾸어도 천장과 다음 픽업 확정은 유지됩니다.\n\n중복 보상  ·  4성 공용 조각 20개 / 5성 공용 조각 100개\n10회 소환은 같은 규칙의 개별 추첨을 10번 진행합니다.",338,300,1240,432,25,Ivory);
            Button(modal,"Close recruitment rates","닫기",1275,770,290,62,CloseRecruitmentOverlay);
        }
        void ShowPartyAssignment(string id,Action after)
        {
            if(!RecruitmentAllowed()||!progress.IsOwned(id)){Notify("보유한 잔영만 편성할 수 있습니다.");return;}
            string name=CampaignCharacterName(id);var modal=RecruitmentModal("Party assignment",name+" · 교체할 편성 칸을 선택하세요",190,230,1540,590);
            Label(modal,"Party swap help","이미 편성한 잔영을 선택하면 두 칸의 위치를 서로 바꿉니다. 출격 파티는 4명입니다.",231,334,1460,52,24,Muted);
            var party=progress.Data.ActiveParty.ToArray();
            for(int i=0;i<party.Length;i++){
                int slot=i;string current=party[i];float x=235+i*368;
                Box(modal,"Party slot plate "+i,x,409,346,253,new Color(.055f,.08f,.116f,1));
                Character(modal,ArtId(current),x+12,419,121,164,false);
                Label(modal,"Party slot "+i,(i+1)+"번 칸",x+151,432,182,40,23,Gold);
                Label(modal,"Party member "+i,CampaignCharacterName(current),x+151,481,182,48,30);
                Button(modal,"Assign party slot "+i,"이 칸에 편성",x+18,589,310,55,()=>{
                    if(overlay!=modal||!RecruitmentAllowed())return;
                    var checkpoint=progress.Capture();if(!progress.EquipCharacter(slot,id,out string error)){RecruitmentNotice(error);return;}
                    if(!PersistRecruitment()){progress.Restore(checkpoint);RecruitmentNotice("저장하지 못해 편성 변경을 취소했습니다.");return;}
                    CloseRecruitmentOverlay();after?.Invoke();RecruitmentNotice(name+"의 편성을 저장했습니다.");
                },current==id);
            }
            Button(modal,"Close party assignment","돌아가기",1375,713,290,62,()=>{CloseRecruitmentOverlay();after?.Invoke();});
        }
        void ShowCouponDialog()
        {
            if(page!="settings"||overlay||!RecruitmentAllowed()||settingsReturn=="battle"||settingsReturn=="resolve"){Notify("전투를 마친 뒤 쿠폰을 사용해 주세요.");return;}
            var modal=RecruitmentModal("Test coupon","테스트 쿠폰",340,240,1240,596);
            Label(modal,"Coupon description","개발 테스트용 쿠폰입니다. 각 코드는 저장 파일마다 한 번 사용할 수 있습니다.",385,341,1145,60,24,Muted);
            var fieldRect=MakeRect(modal,"Coupon input",388,430,1138,81);var backing=fieldRect.gameObject.AddComponent<Image>();backing.color=new Color(.015f,.027f,.045f,1);
            var text=Label(fieldRect,"Text","",23,12,1085,56,29,Ivory);text.raycastTarget=false;
            var placeholder=Label(fieldRect,"Placeholder","쿠폰 코드를 입력하세요",23,12,1085,56,27,Muted);
            var input=fieldRect.gameObject.AddComponent<InputField>();input.targetGraphic=backing;input.textComponent=text;input.placeholder=placeholder;input.characterLimit=64;
            input.lineType=InputField.LineType.SingleLine;input.contentType=InputField.ContentType.Standard;
            input.onValueChanged.AddListener(value=>{var notice=modal.Find("Recruitment notice");if(notice)notice.GetComponent<Text>().text="";});
            Button(modal,"Fill currency coupon","별가루 · 코인",390,540,367,55,()=>input.text=RecruitmentCatalog.TestCurrencyCode);
            Button(modal,"Fill materials coupon","공통 성장 재료",774,540,367,55,()=>input.text=RecruitmentCatalog.TestMaterialsCode);
            Button(modal,"Fill sync materials coupon","전용 재료 7종",1158,540,367,55,()=>input.text=MaterialCatalog.TestCouponCode);
            Label(modal,"Sync coupon help","전용 재료 쿠폰: 캐릭터 7명의 재료를 각각 +9,999,999개 지급합니다.",391,617,1135,45,21,Muted);
            Button(modal,"Redeem coupon","쿠폰 사용",1161,700,364,65,()=>{
                if(!overlay||page!="settings"||!RecruitmentAllowed())return;
                var checkpoint=progress.Capture();if(!progress.RedeemCode(input.text,out var reward,out string error)){RecruitmentNotice(error);return;}
                if(!PersistRecruitment()){progress.Restore(checkpoint);RecruitmentNotice("저장하지 못해 쿠폰 사용을 취소했습니다. 다시 사용할 수 있습니다.");return;}
                CloseRecruitmentOverlay();ShowSettings();Notify(reward.SyncMaterials!=null?"쿠폰 사용 완료 · 캐릭터별 전용 재료 7종 각 +"+RecruitmentCatalog.TestAmount.ToString("N0"):reward.Stardust>0?"쿠폰 사용 완료 · 별가루 +"+reward.Stardust.ToString("N0")+" / 코인 +"+reward.Coins.ToString("N0"):"쿠폰 사용 완료 · 등불 조각 · 기억의 파편 · 공용 조각 각 +"+reward.LanternFragments.ToString("N0"));
            },true);
            Button(modal,"Close coupon","닫기",390,700,320,65,CloseRecruitmentOverlay);
            EventSystemSelect(input.gameObject);
        }
        void EventSystemSelect(GameObject target){if(UnityEngine.EventSystems.EventSystem.current)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(target);}
    }
}
