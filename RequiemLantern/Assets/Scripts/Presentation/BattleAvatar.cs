using System;
using UnityEngine;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    // Battle-only art and planted-foot geometry; menu/dialogue portraits stay in Art/.
    public sealed class BattleAvatarMarker : MonoBehaviour
    {
        public string ArtId, AtlasKey;
        public CombatPoseLayout Layout;
        public float BodyHeight;
        public bool IsSD;
    }

    public static class BattleAvatar
    {
        public const float AllyBodyHeight=220, EnemyBodyHeight=310, GroundY=700;
        public static readonly string[] AllyArtIds={"Lian","Sera","Minato","Noa","Heidel"};

        public static Vector2 EnemyFoot(int index,int count)
        {return new Vector2(count<=1?1595:count==2?1310+350*index:1130+320*index,702);}
        public static float EnemyHeight(int count)
        {return count<=1?EnemyBodyHeight:count==2?250:225;}
        public static Vector2 EnemyAnchor(int index,int count)
        {return EnemyFoot(index,count)-new Vector2(0,EnemyHeight(count)*.48f);}

        public static Texture2D LoadAtlas(string artId,bool preferSD,out string key,out CombatPoseLayout layout)
        {
            key="Combat/"+artId+(preferSD?"SDActions":"Actions");
            var texture=Resources.Load<Texture2D>(key);
            layout=null;
            if(!texture)return null;
            var metadata=Resources.Load<TextAsset>(key+"Layout");
            if(metadata)try{
                var parsed=JsonUtility.FromJson<CombatPoseLayout>(metadata.text);
                if(parsed!=null&&parsed.IsValid(texture))layout=parsed;
                else Debug.LogWarning("Invalid battle avatar layout: "+key);
            }catch(Exception error){Debug.LogWarning("Cannot read battle avatar layout: "+key+" / "+error.Message);}
            return texture;
        }

        public static RawImage Create(Transform parent,string artId,Texture2D fallback,bool enemy,Vector2 foot,float bodyHeight)
        {
            var texture=LoadAtlas(artId,!enemy,out var key,out var layout);
            var node=new GameObject(artId+" battle avatar",typeof(RectTransform),typeof(RawImage),typeof(BattleAvatarMarker));
            var rect=node.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(.5f,0);
            var image=node.GetComponent<RawImage>();image.texture=texture?texture:fallback;image.raycastTarget=false;
            image.color=image.texture?Color.white:Color.clear;
            var marker=node.GetComponent<BattleAvatarMarker>();marker.ArtId=artId;marker.BodyHeight=bodyHeight;
            marker.AtlasKey=texture?key:null;marker.Layout=layout;marker.IsSD=texture&&!enemy;
            if(texture&&layout!=null)ApplyFrame(image,texture,layout,CombatPosePlayer.Idle,bodyHeight);
            else if(texture){
                float h=bodyHeight*512f/360f;
                rect.sizeDelta=new Vector2(h*(texture.width/3f)/(texture.height/2f),h);
                rect.pivot=new Vector2(.5f,1-470f/512f);
                image.uvRect=new Rect(.5f/texture.width,.5f+.5f/texture.height,1f/3-1f/texture.width,.5f-1f/texture.height);
            }
            else{
                float aspect=fallback?(float)fallback.width/fallback.height:.65f;
                rect.sizeDelta=new Vector2(bodyHeight*aspect,bodyHeight);
            }
            rect.anchoredPosition=new Vector2(foot.x,-foot.y);
            return image;
        }

        public static void ApplyFrame(RawImage image,Texture2D atlas,CombatPoseLayout layout,int pose,float bodyHeight)
        {
            var frame=layout.frames[Mathf.Clamp(pose,0,5)];float scale=bodyHeight/layout.bodyHeight;
            float insetX=.5f/atlas.width,insetY=.5f/atlas.height;
            image.uvRect=new Rect(frame.x/atlas.width+insetX,1-(frame.y+frame.height)/atlas.height+insetY,
                frame.width/atlas.width-2*insetX,frame.height/atlas.height-2*insetY);
            var rect=image.rectTransform;
            rect.pivot=new Vector2((frame.footX-frame.x)/frame.width,1-(frame.footY-frame.y)/frame.height);
            rect.sizeDelta=new Vector2(frame.width*scale,frame.height*scale);
        }
    }
}
