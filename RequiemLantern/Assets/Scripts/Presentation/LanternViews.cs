using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using RequiemLantern.Core;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        string lastStoryPortrait="";
        void ShowTitle()
        {
            Clear("title");Box(screen,"Title veil",0,0,1920,1080,new Color(.02f,.035f,.065f,.35f));
            PrepareTitleInput();
            Label(screen,"Overline","T H E   L A S T   L I G H T",160,185,1200,40,19,Gold);
            var logo=Label(screen,"Logo","REQUIEM\nLANTERN",145,225,1400,270,118,Ivory);
            logo.lineSpacing=.78f;
            Box(screen,"Gold rule",162,514,100,3,Gold);
            Label(screen,"Korean title","레퀴엠 랜턴",160,546,1000,64,38);
            Label(screen,"Promise","태양이 꺼진 지 13년.\n우리가 잃어버린 것들은, 아직 빛나고 있다.",163,640,1000,96,26,Muted);
            var prompt=MakeRect(screen,"Title start prompt",510,823,900,118);
            var promptGroup=prompt.gameObject.AddComponent<CanvasGroup>();promptGroup.blocksRaycasts=false;
            Label(prompt,"Start invitation","화면을 클릭해 시작",0,0,900,66,38,Gold,TextAnchor.MiddleCenter);
            Label(prompt,"Start invitation English","CLICK TO START",0,73,900,29,18,Muted,TextAnchor.MiddleCenter);
            prompt.gameObject.AddComponent<TitlePromptBreath>().Configure(promptGroup,reducedMotion);
            titleSettingsButton=Button(screen,"Title settings","설정",1735,34,135,52,ShowSettings).gameObject;
            Label(screen,"Small inscription","기억이 사라져도\n등불은 남는다.",1400,764,350,120,26,Muted,TextAnchor.MiddleRight);
            Footer("Enter / Space로도 시작할 수 있습니다");
        }
        void ShowHub()
        {
            campaign.Exit();ExitWeeklyDungeon();RefreshRecurringActivities();Clear("hub");Header("BEACON KEEP  /  영야의 해안","등대 요새");
            Box(screen,"Portrait plate",66,160,850,800,new Color(.025f,.04f,.06f,.2f));
            Character(screen,"Lian",190,143,625,811);
            Label(screen,"Location number","01",80,170,150,100,72,new Color(.9f,.8f,.66f,.6f));
            Label(screen,"Keep status",$"등대 복구  {progress.Data.KeepLevel}단계",94,800,600,46,25,Gold);
            Label(screen,"Lian quote","“…살아 있으면, 그걸로 됐어.”",94,862,800,64,30);
            Label(screen,"Lian caption","리안  /  등대의 파수꾼",96,927,700,38,19,Muted);
            Button(screen,"Supply board","보급 게시판",95,969,350,47,()=>ShowRecurringBoard());
            Button(screen,"Weekly dungeon","잔향 회수",464,969,350,47,ShowWeeklyDungeons);
            var next=NextCampaignStage();
            var nextChapter=CampaignCatalog.Chapters[next.Chapter-1];
            Label(screen,"Main quest label","MAIN STORY / "+CampaignCharacterName(nextChapter.FocusCharacterId)+"의 이야기",1010,177,760,40,18,Gold);
            Label(screen,"Main quest title",next.Chapter+"장 · "+CampaignCatalog.Chapters[next.Chapter-1].Title,1007,222,800,80,38);
            Label(screen,"Main quest body",next.Number+"  "+next.Title+"\n"+next.Summary,1013,318,775,95,25,Muted);
            HubCard("Sortie","01",next.Number+"  "+next.Title,"이야기 이어보기  ·  귀항호 항해도",1008,462,()=>{selectedChapter=next.Chapter;selectedStage=next.Id;ShowStages();},true);
            HubCard("Roster","02","잔영 기록","별의 파편을 품은 동료들",1008,594,()=>ShowRoster(0));
            HubCard("FirstMeal","03","지나온 이야기","스테이지의 대화와 기억을 다시 펼치기",1008,726,ShowArchive);
            Button(screen,"Recruitment","잔영 소환",1010,890,245,65,()=>ShowRecruitment(),true);
            Button(screen,"Workshop","등대 복구",1275,890,245,65,()=>{
                if(progress.RepairKeep(out string error)){bool saved=Persist();ShowHub();if(saved)Notify("등대의 불빛이 조금 더 따뜻해졌습니다.");}else Notify(error);
            });
            Button(screen,"Settings","설정",1540,890,245,65,ShowSettings);
            if(progress.Data.KeepLevel>1)Box(screen,"Recovered warmth",0,116,1920,908,new Color(1,.53f,.12f,.035f*(progress.Data.KeepLevel-1)));
            Footer("등불지기  카이  ·  작은 온기에서 시작되는 이야기");
        }
        void HubCard(string id,string index,string title,string subtitle,float x,float y,Action action,bool primary=false)
        {
            var b=Button(screen,id,"",x,y,780,111,action,primary);
            Label(b.transform,"Number",index,32,23,60,62,25,Gold);
            Label(b.transform,"Title",title,104,16,520,43,32,Ivory);
            Label(b.transform,"Subtitle",subtitle,105,64,565,27,19,primary?Ivory:Muted);
            Label(b.transform,"Arrow","→",680,25,60,57,33,Gold);
        }
        void StartStory(List<StoryLine> lines,Action done)
        {
            story=lines;storyIndex=0;storyDone=done;storyLog.Clear();storyAuto=false;storyHold=0;lastStoryPortrait="";DrawStory();
        }
        void DrawStory()
        {
            if(storyIndex>=story.Count){FinishCurrentStory();return;}
            var l=story[storyIndex];Clear("story");
            if(l.Background=="keep")screen.Find("The eternal night").GetComponent<RawImage>().texture=Texture("Keep");
            if(l.Background=="black")Box(screen,"Blackout",0,0,1920,1080,new Color(.01f,.02f,.035f,.99f));
            DrawStoryPortraits(l);
            var sideVeil=MakeRect(screen,"Cinematic side shadows",0,0,1920,1080).gameObject.AddComponent<CinematicVeil>();sideVeil.Horizontal=true;sideVeil.color=new Color(0,.005f,.012f,.67f);sideVeil.raycastTarget=false;
            var plaque=MakeRect(screen,"Location plaque",52,34,535,92).gameObject.AddComponent<OrnateButtonGraphic>();plaque.raycastTarget=false;
            Label(plaque.transform,"Location",!string.IsNullOrEmpty(l.Location)?l.Location:l.Background=="keep"?"등대 요새":l.Background=="black"?"기억의 잔향":"얼어붙은 폐허",32,4,467,66,31,Ivory,TextAnchor.MiddleCenter);
            Label(plaque.transform,"Chapter",!string.IsNullOrEmpty(l.Caption)?l.Caption:"CHAPTER 01  ·  꺼진 등대",35,61,460,23,13,Gold,TextAnchor.MiddleCenter);
            Button(screen,"Dialogue log","기록",1327,38,154,58,ShowDialogueLog);
            Button autoButton=null;
            autoButton=Button(screen,"Auto dialogue",storyAuto?"자동 켜짐":"자동",1495,38,164,58,()=>{storyAuto=!storyAuto;storyHold=0;autoButton.GetComponent<OrnateButton>().Caption.text=storyAuto?"자동 켜짐":"자동";});
            Button(screen,"Skip dialogue","장면 넘기기",1673,38,190,58,()=>{storyIndex=story.Count;FinishCurrentStory();});
            var bottomVeil=MakeRect(screen,"Cinematic dialogue fade",0,670,1920,410).gameObject.AddComponent<CinematicVeil>();bottomVeil.color=new Color(.006f,.012f,.024f,.96f);bottomVeil.raycastTarget=false;
            var panel=Box(screen,"Dialogue panel",122,799,1676,248,new Color(.016f,.024f,.039f,.43f));panel.raycastTarget=true;
            var b=panel.gameObject.AddComponent<Button>();b.transition=UnityEngine.UI.Selectable.Transition.None;b.onClick.AddListener(NextStory);
            for(int scan=0;scan<42;scan++)Box(panel.transform,"Glass grain "+scan,0,scan*6,1676,1,new Color(.74f,.66f,.45f,.018f));
            Box(panel.transform,"Left engraved edge",0,0,386,1,new Color(.77f,.63f,.38f,.55f));
            Box(panel.transform,"Right engraved edge",505,0,1171,1,new Color(.77f,.63f,.38f,.30f));
            Label(panel.transform,"Speaker",string.IsNullOrEmpty(l.Speaker)?"기억의 잔향":l.Speaker,44,15,1500,48,27,Gold);
            lineText=l.Text;revealed=0;storyHold=0;
            dialogue=Label(panel.transform,"Dialogue","",44,77,1550,128,32,Ivory,string.IsNullOrEmpty(l.Speaker)?TextAnchor.MiddleCenter:TextAnchor.UpperLeft);
            Label(panel.transform,"Continue",StoryAdvanceLabel(),1246,199,388,38,18,Gold,TextAnchor.MiddleRight);
            Label(screen,"Story progress",$"{storyIndex+1:00} / {story.Count:00}  ·  Space / 클릭",1380,1040,440,25,14,Muted,TextAnchor.MiddleRight);
        }
        System.Collections.IEnumerator RevealPortrait(RawImage portrait,int version)
        {var target=portrait.color;for(float t=0;t<.32f;t+=Time.unscaledDeltaTime){if(version!=screenVersion||!portrait)yield break;portrait.color=new Color(target.r,target.g,target.b,target.a*Mathf.SmoothStep(0,1,t/.32f));yield return null;}if(portrait)portrait.color=target;}
        void FinishCurrentStory(){var done=storyDone;storyDone=null;done?.Invoke();}
        void NextStory(){if(page!="story"||overlay)return;if(revealed<lineText.Length){revealed=lineText.Length;storyHold=0;dialogue.text=lineText;return;}storyIndex++;DrawStory();}
        void ShowDialogueLog()
        {
            if(overlay)return;
            overlay=MakeRect(stage,"Dialogue log",0,0,1920,1080);Box(overlay,"Scrim",0,0,1920,1080,new Color(0,0,0,.8f)).raycastTarget=true;
            Box(overlay,"Paper",220,120,1480,840,PanelColor);Label(overlay,"Heading","지나온 대화",270,150,1100,65,38,Gold);
            Label(overlay,"Log scope","이 장면에서 읽은 대화만 표시됩니다 · 휠로 스크롤",273,222,1300,32,20,Muted);
            var viewport=MakeRect(overlay,"Transcript viewport",273,276,1360,570);
            var viewportImage=viewport.gameObject.AddComponent<Image>();viewportImage.color=new Color(.03f,.045f,.065f,1);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            var content=MakeRect(viewport,"Transcript content",0,0,1318,570);
            string text=VisibleStoryTranscript();
            var transcript=Label(content,"Transcript",text,16,10,1286,550,27);transcript.supportRichText=false;
            float height=Mathf.Max(570,transcript.preferredHeight+34);content.sizeDelta=new Vector2(1318,height);
            transcript.rectTransform.sizeDelta=new Vector2(1286,height-24);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0;
            Label(overlay,"Log scroll hint","↑ 앞 대화    ·    ↓ 최근 대화",273,874,1100,32,19,Muted);
            Button(overlay,"Close log","닫기",1460,154,180,62,CloseStoryLog);
        }
        void ShowRoster(int selected)
        {
            Clear("roster");Header("AFTERGLOW ARCHIVE  /  인물 기록","잔영과 등불지기",ShowHub);
            var descriptions=new[]{"은백색 머리의 파수꾼.\n소등의 날 이후, 13년간 등대의 불을 지켜 왔다.\n파편의 대가로 어린 시절의 기억 대부분을 잃었다.","붉은 머리의 낙천가.\n사라진 고아원 아이들을 찾아 요새에 합류했다.\n큰 웃음 뒤에 남겨진 마음을 감추고 있다.","청록색 머리의 다정한 의사 지망생.\n치료하지 못한 환자들의 이름을 모두 기억한다.","고글과 개조 석궁을 쓰는 실용주의 기술자.\n등대 요새의 공방을 맡는다.","철종 교단에서 도망친 수수께끼의 잔영.\n그림자와 대화할 수 있다.","얼어붙은 폐허에서 기억을 잃고 깨어났다.\n잔영들의 빛을 증폭하고 조율한다.","영야의 연안에서 구조와 인양을 맡아 온 잔영.\n귀항호의 줄과 닻을 점검하며 돌아올 자리를 남긴다.\n놓친 구조줄의 감촉을 아직 손에서 지우지 못했다.","철종 교단의 수장.\n빛이 죽은 세계에 ‘안식’을 제안한다."};
            Character(screen,CharacterIds[selected],70,132,710,840);
            Label(screen,"Character index",$"CHARACTER   {selected+1:00} / {CharacterIds.Length:00}",827,165,990,36,19,Gold);
            Label(screen,"Character name",CharacterNames[selected],820,225,1010,110,76);
            Label(screen,"Role",CharacterRoles[selected],829,361,1010,45,27,Gold);
            Box(screen,"Description rule",832,442,940,1,new Color(.4f,.45f,.5f,.6f));
            Label(screen,"Biography",descriptions[selected],830,484,935,172,28,Muted);
            Label(screen,"Character quote","“"+CharacterQuotes[selected]+"”",830,650,920,57,27);
            if(selected<7){
                string id=CharacterIds[selected].ToLowerInvariant();bool owned=progress.IsOwned(id);var growth=progress.GetCharacter(id);
                var catalog=RecruitmentCatalog.Characters.First(value=>value.Id==id);
                string stars=new string('★',catalog.Rarity);
                Label(screen,"Character growth",owned?$"{stars} · 보유  /  레벨 {growth.Level} · 동기화 {growth.SyncLevel}":catalog.Recruitable?stars+" · 미보유":"스토리 주인공 · 신규 소환 대상 아님",831,404,920,34,21,owned?Gold:Muted);
                var chapter=CampaignCatalog.Chapters.Find(c=>c.FocusCharacterId==id);
                if(chapter!=null){
                    bool available=CampaignRules.IsUnlocked(progress.Data,chapter.Stages[0])||CampaignRules.IsCleared(progress.Data,chapter.Stages[0]);
                    var mainStory=Button(screen,"Main chapter "+id,$"{chapter.Number}장 · {CharacterNames[selected]}의 본편 이야기"+(available?" →":" · 앞 장 완료 필요"),832,710,596,43,()=>ShowCharacterChapter(id));
                    mainStory.interactable=available;
                }
                var equip=Button(screen,"Equip character "+id,owned?"파티 편성":"미보유 · 편성 불가",1444,710,326,43,()=>ShowPartyAssignment(id,()=>ShowRoster(selected)),owned);equip.interactable=owned;
                var sync=Button(screen,"Synchronize "+id,!owned?catalog.Recruitable?"미보유 · 잔영 소환으로 획득하면 동기화할 수 있습니다.":"스토리 주인공 · 기존 보유 캐릭터의 성장은 유지됩니다.":growth.SyncLevel>0?"동기화 완료 · 3스킬 해금":"동기화 · 등불 조각 10 → 3스킬 해금",832,765,938,41,()=>{
                    var checkpoint=progress.Capture();if(progress.Synchronize(id,out string error)){if(!Persist()){progress.Restore(checkpoint);Notify("저장하지 못해 동기화를 취소했습니다.");return;}ShowRoster(selected);Notify("동기화가 완료되어 3스킬이 열렸습니다.");}else Notify(error);
                },owned&&growth.SyncLevel==0);
                sync.interactable=owned&&growth.SyncLevel==0;
            }
            for(int i=0;i<CharacterIds.Length;i++){int n=i;string id=CharacterIds[i].ToLowerInvariant();var entry=RecruitmentCatalog.Character(id);string state=entry!=null?(progress.IsOwned(id)?" · 보유":entry.Recruitable?" · 미보유":" · 주인공"):" · 기록";Button(screen,"Select "+CharacterIds[i],CharacterNames[i]+state,833+(i%4)*241,817+(i/4)*82,220,65,()=>ShowRoster(n),i==selected);}
            Footer("스토리 출연과 보유 여부는 별개입니다 · 보유한 잔영만 동기화와 전투 편성이 가능합니다");
        }
        void ShowSettings()
        {
            if(page!="settings")settingsReturn=page;Clear("settings");Header("PREFERENCES","환경 설정",ReturnFromSettings);
            Label(screen,"Settings heading","편안한 속도로, 빛을 잇도록.",133,195,1700,110,57);
            SettingsRow("점화 타이밍 보조","자동으로 ‘양호’ 판정. 끄면 Space / 클릭으로 점화합니다.",390,assisted,()=>{assisted=!assisted;SaveSettings();ShowSettings();});
            SettingsRow("움직임 줄이기","캐릭터 이동, 화면 흔들림과 빛 파편을 간소화합니다.",550,reducedMotion,()=>{reducedMotion=!reducedMotion;SaveSettings();ShowSettings();});
            SettingsRow("효과음","무기 충돌, 타격, 불씨와 장비 효과음을 재생합니다.",710,sound,()=>{sound=!sound;SaveSettings();ShowSettings();});
            var coupon=Button(screen,"Test coupons","테스트 쿠폰",133,891,410,65,ShowCouponDialog);coupon.interactable=RecruitmentAllowed()&&settingsReturn!="battle"&&settingsReturn!="resolve";
            Label(screen,"Test coupon help","별가루 · 코인과 성장 재료를 테스트용으로 지급합니다.",581,899,1186,50,23,Muted);
            Footer("설정과 진행 상황은 이 기기에 저장됩니다");
        }
        void ReturnFromSettings(){if(settingsReturn=="title")ShowTitle();else ShowHub();}
        void SettingsRow(string title,string help,int y,bool on,Action change)
        {
            Box(screen,title,132,y,1640,131,new Color(.05f,.075f,.11f,.94f));Label(screen,title+" label",title,165,y+15,1300,51,31);Label(screen,title+" help",help,166,y+76,1330,38,23,Muted);
            Button(screen,title+" toggle",on?"켜짐":"꺼짐",1538,y+29,194,71,change,on);
        }
        void SaveSettings(){PlayerPrefs.SetInt("assisted",assisted?1:0);PlayerPrefs.SetInt("reducedMotion",reducedMotion?1:0);PlayerPrefs.SetInt("sound",sound?1:0);PlayerPrefs.Save();}
        RingGraphic Ring(Transform p,string name,float x,float y,float diameter,Color c,float thickness=3)
        {var g=MakeRect(p,name,x,y,diameter,diameter).gameObject.AddComponent<RingGraphic>();g.color=c;g.Thickness=thickness;g.raycastTarget=false;return g;}
    }
}
