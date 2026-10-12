using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RequiemLantern.Presentation
{
    // Geometry stays crisp at every Canvas scale; no baked screenshot text.
    public sealed class OrnateButtonGraphic : MaskableGraphic
    {
        public bool Primary;
        int state;
        public void SetState(int value) { state=value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;bool disabled=state==4,pressed=state==2,lit=state==1||state==3||Primary;
            Color edge=disabled?new Color(.31f,.34f,.38f,.6f):pressed?new Color(.90f,.51f,.29f):lit?new Color(1,.77f,.40f):new Color(.64f,.54f,.39f,.83f);
            Color fill=disabled?new Color(.09f,.105f,.13f,.85f):pressed?new Color(.30f,.095f,.058f,.98f):lit?new Color(.29f,.19f,.095f,.98f):new Color(.038f,.068f,.105f,.96f);
            float cut=Mathf.Min(18,r.height*.25f);
            Vector2[] p={new Vector2(r.xMin+cut,r.yMin+2),new Vector2(r.xMax-cut,r.yMin+2),new Vector2(r.xMax-2,r.center.y),new Vector2(r.xMax-cut,r.yMax-2),new Vector2(r.xMin+cut,r.yMax-2),new Vector2(r.xMin+2,r.center.y)};
            for(int i=0;i<6;i++){
                int k=vh.currentVertCount;var a=p[i];var b=p[(i+1)%6];
                vh.AddVert(r.center,fill,Vector2.zero);vh.AddVert(a,fill*(a.y>r.center.y?1.15f:1f),Vector2.zero);vh.AddVert(b,fill*(b.y>r.center.y?1.15f:1f),Vector2.zero);vh.AddTriangle(k,k+1,k+2);
                if(lit&&!disabled)Line(vh,a,b,new Color(edge.r,edge.g,edge.b,.10f),7);
                Line(vh,a,b,edge,pressed?2.2f:1.3f);
                Line(vh,Vector2.Lerp(a,r.center,.08f),Vector2.Lerp(b,r.center,.08f),new Color(edge.r,edge.g,edge.b,.27f),.7f);
            }
            float cy=r.center.y;
            Diamond(vh,new Vector2(r.xMin+12,cy),4,edge);Diamond(vh,new Vector2(r.xMax-12,cy),4,edge);
            float wing=Mathf.Min(22,r.width*.09f);
            Line(vh,new Vector2(r.xMin+12,cy),new Vector2(r.xMin+12+wing,cy),edge,.8f);
            Line(vh,new Vector2(r.xMax-12-wing,cy),new Vector2(r.xMax-12,cy),edge,.8f);
            Diamond(vh,new Vector2(r.center.x,r.yMax-3),2.5f,edge);
            Diamond(vh,new Vector2(r.center.x,r.yMin+3),2.5f,edge);
        }
        static void Diamond(VertexHelper vh,Vector2 c,float size,Color color)
        {int k=vh.currentVertCount;vh.AddVert(c+Vector2.up*size,color,Vector2.zero);vh.AddVert(c+Vector2.right*size,color,Vector2.zero);vh.AddVert(c-Vector2.up*size,color,Vector2.zero);vh.AddVert(c-Vector2.right*size,color,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);}
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,Color c,float width)
        {var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int k=vh.currentVertCount;vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);}
    }
    public sealed class OrnateButton : Button
    {
        public OrnateButtonGraphic Frame;
        public Text Caption;
        public void Configure(OrnateButtonGraphic frame,Text caption){Frame=frame;Caption=caption;transition=Transition.None;DoStateTransition(currentSelectionState,true);}
        protected override void DoStateTransition(SelectionState state,bool instant)
        {
            base.DoStateTransition(state,instant);
            if(Frame)Frame.SetState((int)state);
            if(Caption)Caption.color=state==SelectionState.Disabled?new Color(.47f,.48f,.50f):state==SelectionState.Pressed?new Color(1,.83f,.62f):new Color(.91f,.86f,.77f);
        }
    }
    public sealed class CinematicVeil : MaskableGraphic
    {
        public bool Horizontal;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;const int bands=32;
            for(int i=0;i<bands;i++){
                float a=i/(float)bands,b=(i+1)/(float)bands;
                var ca=color;var cb=color;ca.a*=Horizontal?Mathf.Pow(Mathf.Abs(a-.5f)*2,1.6f):Mathf.SmoothStep(1,0,a);cb.a*=Horizontal?Mathf.Pow(Mathf.Abs(b-.5f)*2,1.6f):Mathf.SmoothStep(1,0,b);
                int k=vh.currentVertCount;
                if(Horizontal){float x0=Mathf.Lerp(r.xMin,r.xMax,a),x1=Mathf.Lerp(r.xMin,r.xMax,b);vh.AddVert(new Vector2(x0,r.yMin),ca,Vector2.zero);vh.AddVert(new Vector2(x0,r.yMax),ca,Vector2.zero);vh.AddVert(new Vector2(x1,r.yMax),cb,Vector2.zero);vh.AddVert(new Vector2(x1,r.yMin),cb,Vector2.zero);}
                else{float y0=Mathf.Lerp(r.yMin,r.yMax,a),y1=Mathf.Lerp(r.yMin,r.yMax,b);vh.AddVert(new Vector2(r.xMin,y0),ca,Vector2.zero);vh.AddVert(new Vector2(r.xMax,y0),ca,Vector2.zero);vh.AddVert(new Vector2(r.xMax,y1),cb,Vector2.zero);vh.AddVert(new Vector2(r.xMin,y1),cb,Vector2.zero);}
                vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
            }
        }
    }
    public sealed class LinkTarget : MonoBehaviour { public string Id; }
    public sealed class ThreadGraphic : MaskableGraphic
    {
        public Vector2 From, To;
        public float Width = 3;
        public bool Dashed, Glow, Pulse, ReducedMotion;
        void Update(){if(Pulse&&!ReducedMotion)SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var d = To - From;
            if (d.sqrMagnitude < 1) return;
            var n = new Vector2(-d.y, d.x).normalized * Width * .5f;
            int pieces = Dashed ? 22 : 1;
            for (int i = 0; i < pieces; i++)
            {
                var a = Vector2.Lerp(From, To, i / (float)pieces);
                var b = Vector2.Lerp(From, To, (i + (Dashed ? .57f : 1f)) / pieces);
                int start = vh.currentVertCount;
                vh.AddVert(a-n, color, Vector2.zero); vh.AddVert(a+n, color, Vector2.zero);
                vh.AddVert(b+n, color, Vector2.zero); vh.AddVert(b-n, color, Vector2.zero);
                vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
            }
            if(Glow){AddBeam(vh,From,To,Width*4,new Color(color.r,color.g,color.b,color.a*.09f));AddBeam(vh,From,To,Width*.38f,new Color(1,.93f,.76f,color.a));}
            if(Pulse&&!ReducedMotion){float t=Mathf.Repeat(Time.unscaledTime*.55f,1);AddBeam(vh,Vector2.Lerp(From,To,Mathf.Max(0,t-.09f)),Vector2.Lerp(From,To,t),Width*1.35f,new Color(1,1,.87f,.9f));}
        }
        static void AddBeam(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c){var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int k=vh.currentVertCount;vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);}
    }

    public sealed class RingGraphic : MaskableGraphic
    {
        public float Thickness = 3;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            float radius = Mathf.Min(r.width,r.height)*.5f;
            for(int i=0;i<96;i++)
            {
                float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96;
                Vector2 va=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                Vector2 vb=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                int k=vh.currentVertCount;
                vh.AddVert(r.center+va*radius,color,Vector2.zero);
                vh.AddVert(r.center+va*(radius-Thickness),color,Vector2.zero);
                vh.AddVert(r.center+vb*(radius-Thickness),color,Vector2.zero);
                vh.AddVert(r.center+vb*radius,color,Vector2.zero);
                vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
            }
        }
    }

    public sealed class ImmediatePress : MonoBehaviour, IPointerDownHandler
    {
        public Action<PointerEventData> Pressed;
        bool consumed;
        public void OnPointerDown(PointerEventData e)
        {
            // A timing prompt accepts the first primary mouse/touch press,
            // never the later release or a second simultaneous pointer.
            if(consumed||e.button!=PointerEventData.InputButton.Left)return;
            consumed=true;Pressed?.Invoke(e);
        }
    }
    public sealed class LinkDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action<PointerEventData> Begin, Drag, End;
        public void OnBeginDrag(PointerEventData e) { Begin?.Invoke(e); }
        public void OnDrag(PointerEventData e) { Drag?.Invoke(e); }
        public void OnEndDrag(PointerEventData e) { End?.Invoke(e); }
    }

    public sealed class IdleSway : MonoBehaviour
    {
        Vector3 origin; public float Phase;
        void Start() { origin=transform.localPosition; }
        void Update() { transform.localPosition=origin+new Vector3(0,Mathf.Sin(Time.unscaledTime*.95f+Phase)*3,0); }
    }
}
