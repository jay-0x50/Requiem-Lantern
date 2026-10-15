using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RequiemLantern.Core;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp : MonoBehaviour
    {
        internal static readonly Color Ink=Hex("0D1117"), PanelColor=Hex("151C27"), Gold=Hex("F2A541"), Ivory=Hex("E7DCC9"), Muted=Hex("9CA7B5"), Violet=Hex("B09BD8"), Teal=Hex("75C8C4");
        RectTransform stage,screen,overlay;
        Font font; AudioSource audioSource; Dictionary<string,AudioClip> tones=new Dictionary<string,AudioClip>();
        CombatAudioBank combatAudio;
        Dictionary<string,Texture2D> art=new Dictionary<string,Texture2D>();
        BattleEngine battle; ProgressService progress; SaveStore save;
        string page="",toastText="",settingsReturn="title"; float toastUntil; Text toast;
        int screenVersion;
        bool assisted=true,reducedMotion=false,sound=true,autoMode=false;
        List<StoryLine> story; int storyIndex; Action storyDone; Text dialogue; string lineText; float revealed; bool storyAuto;
        List<string> storyLog=new List<string>();
        static readonly string[] CharacterIds={"Lian","Sera","Minato","Heidel","Noa","Kai","Orphe"};
        static readonly string[] CharacterNames={"리안","세라","미나토","하이델","노아","카이","오르페"};
        static readonly string[] CharacterRoles={"광휘 · 수호자","화염 · 돌격수","물결 · 치유사","서리 · 사수","그림자 · 조율사","등불지기","철종 교단 · 종지기"};
        static readonly string[] CharacterQuotes={"…살아 있으면, 그걸로 됐어.","괜찮아 괜찮아! 어둠 따위, 내가 태워버릴 테니까!","잃어버린 이름을, 잊지 않으려고.","꺼진 장치에도 쓸 만한 부품은 남아 있어.","그림자도 가끔은 이야기를 해.","손에 남은 것은, 꺼져가는 등불 하나.","온화한 미소로 ‘안식’을 설파하는 자."};

        void Awake()
        {
            Application.targetFrameRate=60;
            font=Resources.Load<Font>("Fonts/NotoSansKRRegular");
            if(!font)font=Resources.Load<Font>("Fonts/NotoSansKR");
            if(!font) font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},26);
            save=new SaveStore(Path.Combine(Application.persistentDataPath,"progress.xml"));
            progress=new ProgressService(save.Load());
            assisted=PlayerPrefs.GetInt("assisted",1)==1; reducedMotion=PlayerPrefs.GetInt("reducedMotion",0)==1; sound=PlayerPrefs.GetInt("sound",1)==1;
            var canvasGo=new GameObject("Requiem Lantern Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasGo.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            stage=MakeRect(canvasGo.transform,"Stage",0,0,1920,1080);
            stage.anchorMin=stage.anchorMax=stage.pivot=new Vector2(.5f,.5f);stage.anchoredPosition=Vector2.zero;
            if(!FindAnyObjectByType<EventSystem>())new GameObject("Event System",typeof(EventSystem),typeof(StandaloneInputModule));
            audioSource=gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake=false;
            combatAudio=new CombatAudioBank(transform);
            ShowTitle();
            if(!string.IsNullOrEmpty(save.LastWarning))Notify(save.LastWarning);
            if(Environment.GetCommandLineArgs().Any(argument=>argument=="--smoke"||argument=="--ux-smoke"||argument=="--intent-smoke"||argument=="--title-smoke"||argument=="--recruitment-smoke"||argument=="--coupon-smoke"))StartCoroutine(SmokeTour());
        }
        void Update()
        {
            combatAudio?.Update(sound);
            if(toast)toast.gameObject.SetActive(Time.unscaledTime<toastUntil);
            if(page=="title"){TickTitleInput();return;}
            if(dialogue&&page=="story"&&!overlay)
            {
                revealed+=Time.unscaledDeltaTime*36;
                dialogue.text=lineText.Substring(0,Mathf.Min(lineText.Length,(int)revealed));
                if(storyAuto&&revealed>lineText.Length+90)NextStory();
                if(Input.GetKeyDown(KeyCode.Space))NextStory();
            }
            if(Input.GetKeyDown(KeyCode.Escape)&&page!="title"&&page!="resolve"){
                if(overlay){if(page=="recruitment")DismissRecruitmentOverlay();else CloseRecruitmentOverlay();}else if(page=="battle")ConfirmRetreat();else if(page=="settings")ReturnFromSettings();else ShowHub();
            }
        }
        bool Persist() { try { save.Save(progress.Data); return true; } catch(Exception e) { Notify("저장하지 못했습니다. 저장 공간을 확인해 주세요."); Debug.LogException(e); return false; } }
        static Color Hex(string hex){ColorUtility.TryParseHtmlString("#"+hex,out Color c);return c;}
        RectTransform MakeRect(Transform parent,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=go.GetComponent<RectTransform>();rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        Image Box(Transform p,string name,float x,float y,float w,float h,Color c)
        {var rt=MakeRect(p,name,x,y,w,h);var im=rt.gameObject.AddComponent<Image>();im.color=c;im.raycastTarget=false;return im;}
        Text Label(Transform p,string name,string content,float x,float y,float w,float h,int size=26,Color? c=null,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var t=MakeRect(p,name,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.text=content;t.fontSize=size;t.color=c??Ivory;t.alignment=align;t.raycastTarget=false;
            // Noto's Korean line metrics exceed several compact label rectangles.
            // Preserve the complete glyphs instead of dropping the whole first line.
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;t.supportRichText=true;return t;
        }
        Button Button(Transform p,string name,string content,float x,float y,float w,float h,Action action,bool primary=false)
        {
            var im=Box(p,name,x,y,w,h,Color.clear);im.raycastTarget=true;
            var frame=MakeRect(im.transform,"Engraved frame",0,0,w,h).gameObject.AddComponent<OrnateButtonGraphic>();frame.Primary=primary;frame.raycastTarget=false;
            float inset=w<100?12:25;
            var caption=Label(im.transform,"Label",content,inset,0,w-inset*2,h,Mathf.Min(24,(int)(h*.46f)),Ivory,TextAnchor.MiddleCenter);
            var b=im.gameObject.AddComponent<OrnateButton>();b.Configure(frame,caption);
            b.onClick.AddListener(()=>{Tone("click",660,.055f,.055f);action?.Invoke();});return b;
        }
        Texture2D Texture(string name)
        {
            if(!art.TryGetValue(name,out var t)){t=Resources.Load<Texture2D>("Art/"+name);art[name]=t;}return t;
        }
        RawImage Picture(Transform p,string name,string key,float x,float y,float w,float h,bool fit=true)
        {
            var t=Texture(key);var r=MakeRect(p,name,x,y,w,h);var im=r.gameObject.AddComponent<RawImage>();im.texture=t;im.color=t?Color.white:Color.clear;im.raycastTarget=false;
            if(t&&fit){float s=Mathf.Min(w/t.width,h/t.height);r.sizeDelta=new Vector2(t.width*s,t.height*s);r.anchoredPosition=new Vector2(x+(w-r.sizeDelta.x)/2,-y-(h-r.sizeDelta.y)/2);}
            return im;
        }
        RawImage Character(Transform p,string id,float x,float y,float w,float h,bool sway=true)
        {
            id=ArtId(id);
            string name=Texture(id)?id:id+"_Reference";var im=Picture(p,id+" portrait",name,x,y,w,h);
            if(sway&&!reducedMotion)im.gameObject.AddComponent<IdleSway>().Phase=x*.01f;
            return im;
        }
        void Clear(string name)
        {
            combatAudio?.Stop();
            HideIntentTooltip();
            screenVersion++;
            resolving=false;battleDockRetracted=false;activeClashActor=null;CancelDragVisuals();
            if(screen){screen.gameObject.SetActive(false);Destroy(screen.gameObject);}if(overlay){overlay.gameObject.SetActive(false);Destroy(overlay.gameObject);}overlay=null;
            screen=MakeRect(stage,name,0,0,1920,1080);page=name;dialogue=null;
            Box(screen,"Background",0,0,1920,1080,Ink);
            Picture(screen,"The eternal night",name=="hub"||name=="archive"||name=="settings"?"Keep":"Background",0,0,1920,1080,false);
            Box(screen,"Atmosphere",0,0,1920,1080,new Color(.025f,.04f,.075f,.35f));
            toast=Label(screen,"Notification",toastText,440,16,1040,46,22,Gold,TextAnchor.MiddleCenter);
        }
        void Header(string eyebrow,string title,Action back=null)
        {
            Box(screen,"Header backing",0,0,1920,116,new Color(.035f,.05f,.075f,.93f));
            if(back!=null)Button(screen,"Back","〈",40,34,62,56,back);
            Label(screen,"Section",eyebrow,back==null?62:130,24,950,26,16,Gold);
            Label(screen,"Title",title,back==null?62:130,54,1050,48,33);
            Label(screen,"Currency",$"등불 조각  {progress.Data.LanternFragments}     코인  {progress.Data.Coins:N0}",1330,43,525,45,23,Muted,TextAnchor.MiddleRight);
        }
        void Footer(string note)
        {
            Box(screen,"Footer backing",0,1024,1920,56,new Color(.035f,.05f,.075f,.95f));
            Label(screen,"Build mark","REQUIEM LANTERN   /   DEVELOPMENT 0.1",48,1034,750,30,15,Muted);
            Label(screen,"Hint",note,800,1034,1072,30,17,Muted,TextAnchor.MiddleRight);
        }
        void Notify(string message){toastText=message;toastUntil=Time.unscaledTime+4;if(toast){toast.text=message;toast.transform.SetAsLastSibling();}}
        void Tone(string key,float frequency,float duration,float volume)
        {
            if(!sound)return;
            if(!tones.TryGetValue(key,out var clip)){
                int n=(int)(44100*duration);float[] a=new float[n];
                for(int i=0;i<n;i++){float t=i/44100f;float e=Mathf.Sin(Mathf.PI*i/n)*Mathf.Exp(-4*t/duration);a[i]=(Mathf.Sin(2*Mathf.PI*frequency*t)+.22f*Mathf.Sin(2*Mathf.PI*frequency*2.76f*t))*e;}
                clip=AudioClip.Create(key,n,1,44100,false);clip.SetData(a,0);tones[key]=clip;
            }audioSource.PlayOneShot(clip,volume);
        }
        void OnDestroy(){combatAudio?.Dispose();foreach(var c in tones.Values)Destroy(c);}
    }
}
