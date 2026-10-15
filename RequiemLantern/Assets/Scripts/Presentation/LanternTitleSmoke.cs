using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RequiemLantern.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public partial class LanternApp
    {
        PointerEventData TitleSmokePointer(GameObject target,Vector2 point)
        {
            return new PointerEventData(EventSystem.current){pointerId=-1,button=PointerEventData.InputButton.Left,
                position=RectTransformUtility.WorldToScreenPoint(null,screen.TransformPoint(ToMesh(point))),
                pointerCurrentRaycast=new RaycastResult{gameObject=target}};
        }
        IEnumerator SmokeTitleSteps()
        {
            var args=Environment.GetCommandLineArgs();int captureIndex=Array.IndexOf(args,"--capture-dir");
            string directory=captureIndex>=0&&captureIndex+1<args.Length?args[captureIndex+1]:Path.Combine(Application.persistentDataPath,"QA");
            Directory.CreateDirectory(directory);smokeDirectory=directory;smokePhase="click-to-start title input";smokeDeadline=Time.realtimeSinceStartup+45;
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"RUNNING: title-only click/keyboard, save routing and settings isolation.\n");
            assisted=true;autoMode=false;reducedMotion=false;toastText="";toastUntil=0;campaign.Exit();
            save=new SaveStore(Path.Combine(directory,"title-new-progress.xml"));progress=new ProgressService(new ProgressData());ShowTitle();
            var surface=screen.Find("Title start surface").GetComponent<TitleStartSurface>();var point=new Vector2(300,370);
            var press=TitleSmokePointer(surface.gameObject,point);surface.OnPointerDown(press);surface.OnPointerUp(press);
            if(page!="title"||campaign.Active!=null)throw new Exception("Title accepted a carried-over press before its input release gate.");
            yield return Capture(directory,"01-title-click-to-start");
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(press,hits);
            if(hits.Count==0||hits[0].gameObject!=surface.gameObject)throw new Exception("The original logo/art region did not act as the title start surface.");
            var settings=screen.Find("Title settings").GetComponent<Button>();var settingsPress=TitleSmokePointer(settings.gameObject,new Vector2(1800,60));
            hits.Clear();EventSystem.current.RaycastAll(settingsPress,hits);
            if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=settings)throw new Exception("Full-screen start input intercepted the settings control.");
            surface.OnPointerDown(press);surface.OnPointerUp(press);
            if(page!="story"||campaign.Active!=CampaignCatalog.Chapters[0].Stages[0]||storyIndex!=0)throw new Exception("New-player click did not enter the first existing story line.");
            var firstStage=campaign.Active;surface.OnPointerUp(press);ProcessTitleInput(false,true,false);
            if(page!="story"||campaign.Active!=firstStage||storyIndex!=0)throw new Exception("Duplicate title input restarted or advanced the opening story.");
            if(progress.Data.PrologueCompleted)throw new Exception("Starting a new title falsely marked its story complete.");
            revealed=999;yield return Capture(directory,"02-new-player-story");
            campaign.Exit();string returningPath=Path.Combine(directory,"title-returning-progress.xml");
            save=new SaveStore(returningPath);save.Save(new ProgressData{PrologueCompleted=true,Coins=987,LanternFragments=23});
            string originalSave=File.ReadAllText(returningPath);progress=new ProgressService(save.Load());ShowTitle();yield return new WaitForSecondsRealtime(.15f);
            settings=screen.Find("Title settings").GetComponent<Button>();EventSystem.current.SetSelectedGameObject(settings.gameObject);
            ProcessTitleInput(false,true,true);
            if(page!="title")throw new Exception("Keyboard start ignored focus on title settings.");
            settings.OnSubmit(new BaseEventData(EventSystem.current));
            if(page!="settings"||settingsReturn!="title"||campaign.Active!=null)throw new Exception("Submitting settings started the game or lost its title return destination.");
            yield return Capture(directory,"03-settings-separated");
            screen.Find("Back").GetComponent<Button>().onClick.Invoke();
            var returnedSurface=screen.Find("Title start surface").GetComponent<TitleStartSurface>();
            returnedSurface.OnPointerUp(TitleSmokePointer(returnedSurface.gameObject,point));ProcessTitleInput(false,true,false);
            if(page!="title"||campaign.Active!=null)throw new Exception("Settings-close release or held start key leaked into title start.");
            reducedMotion=true;ShowTitle();yield return Capture(directory,"04-title-reduced-motion");
            var breathing=screen.Find("Title start prompt").GetComponent<TitlePromptBreath>();var alpha=breathing.GetComponent<CanvasGroup>().alpha;
            if(!breathing.Reduced||alpha!=1)throw new Exception("Reduced motion retained title breathing.");
            yield return new WaitForSecondsRealtime(.2f);
            if(breathing.GetComponent<CanvasGroup>().alpha!=1)throw new Exception("Reduced-motion prompt opacity changed over time.");
            reducedMotion=false;ShowTitle();yield return new WaitForSecondsRealtime(.15f);ProcessTitleInput(false,true,false);
            if(page!="hub"||campaign.Active!=null||progress.Data.Coins!=987||progress.Data.LanternFragments!=23||!progress.Data.PrologueCompleted)
                throw new Exception("Returning-player shared Enter/Space input lost its hub/save routing.");
            ProcessTitleInput(false,true,false);
            if(page!="hub"||File.ReadAllText(returningPath)!=originalSave)throw new Exception("Repeated start input changed or replaced existing saved progress.");
            yield return Capture(directory,"05-returning-player-hub");
            foreach(string shot in new[]{"01-title-click-to-start","02-new-player-story","03-settings-separated","04-title-reduced-motion","05-returning-player-hub"})
                if(!FreshSmokeScreenshot(Path.Combine(directory,shot+".png")))throw new Exception("A title-only screenshot was missing or stale: "+shot);
            File.WriteAllText(Path.Combine(directory,"smoke-result.txt"),"PASS: five fresh title-only native screenshots. The unchanged original title art, logo and world text now use a frameless lower-center click-to-start prompt with a small English caption. The actual UI raycaster resolved ordinary title artwork to the start surface and settings to its own separate button. A primary press/release began the existing first-stage opening for a fresh player; duplicate pointer-up and start-key dispatch did not restart or advance its first story line. The shared Enter/Space handler respected settings focus; submitting settings opened settings and its Back action returned to the title. A release without a title-owned press and a held key on that return frame could not start the game. Reduced motion kept prompt opacity at one. Loading an existing saved prologue completion routed fresh keyboard start to the hub and preserved the exact save bytes, currencies and story completion. QA used isolated save paths and did not write player settings.\n");
            Debug.Log("REQUIEM_LANTERN_TITLE_SMOKE_PASS");Application.Quit(0);
        }
    }
}
