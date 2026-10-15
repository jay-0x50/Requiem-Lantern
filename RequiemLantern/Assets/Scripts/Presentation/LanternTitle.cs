using System;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public sealed class TitleStartSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Func<bool> Ready;
        public Action Start;
        bool ownsPress;
        int pointerId;
        public void OnPointerDown(PointerEventData e)
        {
            ownsPress=e.button==PointerEventData.InputButton.Left&&Ready!=null&&Ready();pointerId=e.pointerId;
        }
        public void OnPointerUp(PointerEventData e)
        {
            bool start=ownsPress&&e.pointerId==pointerId&&e.button==PointerEventData.InputButton.Left
                &&e.pointerCurrentRaycast.gameObject==gameObject&&Ready!=null&&Ready();
            ownsPress=false;if(start)Start?.Invoke();
        }
        void OnDisable(){ownsPress=false;}
    }
    public sealed class TitlePromptBreath : MonoBehaviour
    {
        CanvasGroup prompt;
        public bool Reduced {get;private set;}
        public void Configure(CanvasGroup group,bool reduced){prompt=group;Reduced=reduced;if(prompt)prompt.alpha=1;}
        void Update(){if(prompt)prompt.alpha=Reduced?1:Mathf.Lerp(.65f,1,(Mathf.Sin(Time.unscaledTime*1.25f)+1)*.5f);}
    }
    public partial class LanternApp
    {
        bool titleArmed,titleStarting;
        int titleOpenedFrame;
        GameObject titleSettingsButton;

        void PrepareTitleInput()
        {
            titleArmed=false;titleStarting=false;titleOpenedFrame=Time.frameCount;
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
            var surface=Box(screen,"Title start surface",0,0,1920,1080,Color.clear);surface.raycastTarget=true;
            int version=screenVersion;var input=surface.gameObject.AddComponent<TitleStartSurface>();
            input.Ready=()=>version==screenVersion&&TitleReady();input.Start=StartFromTitle;
        }
        bool TitleReady(){return page=="title"&&titleArmed&&!titleStarting&&!overlay;}
        void TickTitleInput()
        {
            bool released=!Input.GetMouseButton(0)&&!Input.GetKey(KeyCode.Return)&&!Input.GetKey(KeyCode.KeypadEnter)&&!Input.GetKey(KeyCode.Space);
            var focused=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            bool settingsFocused=titleSettingsButton&&focused&&(focused==titleSettingsButton||focused.transform.IsChildOf(titleSettingsButton.transform));
            ProcessTitleInput(released,Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)||Input.GetKeyDown(KeyCode.Space),settingsFocused);
        }
        void ProcessTitleInput(bool released,bool startPressed,bool settingsFocused)
        {
            if(page!="title"||titleStarting||overlay)return;
            if(!titleArmed){if(Time.frameCount>titleOpenedFrame&&released)titleArmed=true;return;}
            if(startPressed&&!settingsFocused)StartFromTitle();
        }
        void StartFromTitle()
        {
            if(!TitleReady())return;titleStarting=true;Tone("click",660,.055f,.055f);
            if(progress.Data.PrologueCompleted)ShowHub();else EnterCampaignStage(CampaignCatalog.Chapters[0].Stages[0].Id);
            if(page=="title"){titleStarting=false;titleArmed=false;titleOpenedFrame=Time.frameCount;}
        }
    }
}
