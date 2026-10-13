using System;
using UnityEngine;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    [Serializable]
    public sealed class CombatPoseFrame
    {
        public float x,y,width,height,footX,footY,contactX,contactY;
    }
    [Serializable]
    public sealed class CombatPoseLayout
    {
        public float bodyHeight=360;
        public CombatPoseFrame[] frames;
        public bool IsValid(Texture2D texture)
        {
            if(bodyHeight<=0||frames==null||frames.Length!=6)return false;
            foreach(var frame in frames)if(frame==null||frame.width<=1||frame.height<=1||frame.x<0||frame.y<0
                ||frame.x+frame.width>texture.width||frame.y+frame.height>texture.height
                ||frame.footX<frame.x||frame.footX>frame.x+frame.width||frame.footY<frame.y||frame.footY>frame.y+frame.height)return false;
            return true;
        }
    }

    // Runtime inspection for the smoke tour; no debug labels enter the game view.
    public sealed class CombatPhaseMarker : MonoBehaviour
    {
        public string Phase;
        public RawImage Attacker, Defender;
        public Vector2 ActorFoot, ContactPoint;
        public bool AtlasActive, FrameLayoutActive, Clash;
        public int Pose, SkillNumber;
        public string SkillId;
    }

    public sealed class FlameRevealMarker : MonoBehaviour
    {
        public int Revealed, Total;
        public bool Tense;
        public float Duration;
    }

    // A temporary foreground performer. The planning portrait is restored on every exit.
    public sealed class CombatPosePlayer : IDisposable
    {
        public const int Idle=0, Windup=1, Swing=2, FollowThrough=3, Recovery=4, Hit=5;
        readonly RawImage original;
        readonly bool originalEnabled;
        readonly bool faceRight;
        readonly Texture2D atlas;
        readonly CombatPoseLayout layout;
        readonly RectTransform rect;
        readonly float cellHeight;
        readonly float desiredBodyHeight;
        bool disposed;
        public RawImage Image { get; private set; }
        public Vector2 OriginalFoot { get; private set; }
        public bool HasAtlas { get { return atlas; } }
        public bool HasFrameLayout { get { return layout!=null; } }
        public int Pose { get; private set; }

        public CombatPosePlayer(Transform parent,RawImage source,string artId,bool facesRight,float bodyHeight,string name)
        {
            original=source;originalEnabled=source&&source.enabled;faceRight=facesRight;
            desiredBodyHeight=bodyHeight;
            var sourceRect=source.rectTransform;
            OriginalFoot=new Vector2(sourceRect.anchoredPosition.x+(.5f-sourceRect.pivot.x)*sourceRect.sizeDelta.x,
                -sourceRect.anchoredPosition.y+sourceRect.pivot.y*sourceRect.sizeDelta.y);
            atlas=Resources.Load<Texture2D>("Combat/"+artId+"Actions");
            if(!atlas)atlas=Resources.Load<Texture2D>("Art/Combat/"+artId+"Actions");
            if(atlas){
                var metadata=Resources.Load<TextAsset>("Combat/"+artId+"ActionsLayout");
                if(!metadata)metadata=Resources.Load<TextAsset>("Art/Combat/"+artId+"ActionsLayout");
                if(metadata)try{
                    var parsed=JsonUtility.FromJson<CombatPoseLayout>(metadata.text);
                    if(parsed!=null&&parsed.IsValid(atlas))layout=parsed;
                    else Debug.LogWarning("Invalid combat pose layout: "+artId+"; using the 3 by 2 grid.");
                }catch(Exception error){Debug.LogWarning("Cannot read combat pose layout: "+artId+" / "+error.Message);}
            }
            var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));
            rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);
            // Atlas cells are 512 square: shared foot row 470, body height approximately 360.
            // Keeping the pivot at that foot row prevents pose swaps from sliding on the ground.
            rect.pivot=new Vector2(.5f,atlas?1-470f/512f:0);
            Image=go.GetComponent<RawImage>();Image.raycastTarget=false;
            Image.texture=atlas?atlas:source.texture;Image.color=source.color;
            cellHeight=atlas?bodyHeight*512f/360f:bodyHeight;
            float aspect=atlas?(atlas.width/3f)/(atlas.height/2f):sourceRect.sizeDelta.x/Mathf.Max(1,sourceRect.sizeDelta.y);
            rect.sizeDelta=new Vector2(cellHeight*aspect,cellHeight);
            SetPose(Idle);PlaceFoot(OriginalFoot);
            source.enabled=false;
        }

        public void SetPose(int pose)
        {
            if(!Image)return;
            Pose=Mathf.Clamp(pose,0,5);
            if(atlas){
                // Rows in the source PNG run top to bottom; Unity UV coordinates run bottom to top.
                float insetX=.5f/atlas.width,insetY=.5f/atlas.height;
                if(layout!=null){
                    var frame=layout.frames[Pose];float scale=desiredBodyHeight/layout.bodyHeight;
                    Image.uvRect=new Rect(frame.x/atlas.width+insetX,1-(frame.y+frame.height)/atlas.height+insetY,frame.width/atlas.width-2*insetX,frame.height/atlas.height-2*insetY);
                    rect.pivot=new Vector2((frame.footX-frame.x)/frame.width,1-(frame.footY-frame.y)/frame.height);
                    rect.sizeDelta=new Vector2(frame.width*scale,frame.height*scale);
                }
                else Image.uvRect=new Rect((Pose%3)/3f+insetX,1-(Pose/3+1)/2f+insetY,1f/3-2*insetX,.5f-2*insetY);
                rect.localRotation=Quaternion.identity;
            }
            else{
                Image.uvRect=original.uvRect;
                float angle=Pose==Windup?7:Pose==Swing?-8:Pose==FollowThrough?-5:Pose==Hit?11:Pose==Recovery?3:0;
                rect.localRotation=Quaternion.Euler(0,0,angle*(faceRight?1:-1));
            }
        }

        public void PlaceFoot(Vector2 point)
        {if(rect)rect.anchoredPosition=new Vector2(point.x,-point.y);}

        // Rotate around the planted foot, after SetPose has chosen the source frame.
        public void Lean(float degrees)
        {if(rect)rect.localRotation*=Quaternion.Euler(0,0,degrees);}

        // Top-down canvas offset from the planted foot to the actual blade/fist contact.
        public Vector2 ContactOffset(int pose)
        {
            if(layout!=null){
                var frame=layout.frames[Mathf.Clamp(pose,0,5)];
                if(frame.contactX>0&&frame.contactY>0)
                    return new Vector2(frame.contactX-frame.footX,frame.contactY-frame.footY)*(desiredBodyHeight/layout.bodyHeight);
            }
            return new Vector2(faceRight?170:-170,-desiredBodyHeight*.48f);
        }

        public void Tint(Color color)
        {if(Image)Image.color=color;}

        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(original)original.enabled=originalEnabled;
            if(Image){Image.gameObject.SetActive(false);UnityEngine.Object.Destroy(Image.gameObject);}
        }
    }

    // A short weapon trail around the contact point, never a beam between battlefield anchors.
    public sealed class WeaponArcGraphic : MaskableGraphic
    {
        public Vector2 Center;
        public float Radius=112, Thickness=12, Progress;
        public int Facing=1;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(Progress<=0||color.a<=0)return;
            const int pieces=26;
            float head=Mathf.Clamp01(Progress),tail=Mathf.Max(0,head-.62f);
            for(int i=0;i<pieces;i++){
                float a=Mathf.Lerp(tail,head,i/(float)pieces),b=Mathf.Lerp(tail,head,(i+1)/(float)pieces);
                float aa=Mathf.Lerp(120,-85,a)*Mathf.Deg2Rad,ab=Mathf.Lerp(120,-85,b)*Mathf.Deg2Rad;
                var na=new Vector2(Mathf.Cos(aa)*Facing,Mathf.Sin(aa));var nb=new Vector2(Mathf.Cos(ab)*Facing,Mathf.Sin(ab));
                float width=Thickness*Mathf.Sin((i+.5f)/pieces*Mathf.PI);
                var tint=color;tint.a*=.3f+.7f*(i+1)/pieces;
                int k=vh.currentVertCount;
                vh.AddVert(Center+na*(Radius-width),tint,Vector2.zero);vh.AddVert(Center+na*Radius,tint,Vector2.zero);
                vh.AddVert(Center+nb*Radius,tint,Vector2.zero);vh.AddVert(Center+nb*(Radius-width),tint,Vector2.zero);
                vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
            }
        }
    }
}
