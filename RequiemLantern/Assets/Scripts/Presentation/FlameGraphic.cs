using UnityEngine;
using UnityEngine.UI;

namespace RequiemLantern.Presentation
{
    public sealed class FlameGraphic : MaskableGraphic
    {
        public bool Lit;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            Vector2[] points={new Vector2(.5f,1),new Vector2(.42f,.76f),new Vector2(.21f,.57f),new Vector2(.16f,.33f),new Vector2(.25f,.13f),new Vector2(.5f,.04f),new Vector2(.75f,.13f),new Vector2(.84f,.35f),new Vector2(.69f,.63f),new Vector2(.62f,.46f)};
            Vector2 middle=new Vector2(.5f,.40f);vh.AddVert(new Vector3(r.xMin+middle.x*r.width,r.yMin+middle.y*r.height),color,Vector2.zero);
            for(int i=0;i<points.Length;i++)vh.AddVert(new Vector3(r.xMin+points[i].x*r.width,r.yMin+points[i].y*r.height),color,Vector2.zero);
            for(int i=0;i<points.Length;i++)vh.AddTriangle(0,i+1,(i+1)%points.Length+1);
            if(Lit){Color core=Color.Lerp(color,Color.white,.72f);int k=vh.currentVertCount;vh.AddVert(new Vector3(r.xMin+.5f*r.width,r.yMin+.68f*r.height),core,Vector2.zero);vh.AddVert(new Vector3(r.xMin+.35f*r.width,r.yMin+.19f*r.height),core,Vector2.zero);vh.AddVert(new Vector3(r.xMin+.65f*r.width,r.yMin+.19f*r.height),core,Vector2.zero);vh.AddTriangle(k,k+1,k+2);}
        }
    }
}
