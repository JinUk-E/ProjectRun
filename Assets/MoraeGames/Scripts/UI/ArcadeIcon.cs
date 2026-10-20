using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace MoraeGames.UI
{
    public sealed class ArcadeIcon : MaskableGraphic
    {
        public int symbol;
        public void SetSymbol(int value) { if (symbol == value) return; symbol = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch (symbol)
            {
                case 0: Path(vh, new[]{new Vector2(.58f,.95f),new Vector2(.18f,.43f),new Vector2(.47f,.43f),new Vector2(.38f,.05f),new Vector2(.84f,.6f),new Vector2(.55f,.6f)}, true); break;
                case 1: Ring(vh,.5f,.5f,.4f); Ring(vh,.5f,.5f,.2f); Line(vh,.5f,.5f,.8f,.85f); break;
                case 2: Ring(vh,.5f,.5f,.28f); Line(vh,.5f,.05f,.5f,.3f); Line(vh,.5f,.7f,.5f,.95f); Line(vh,.05f,.5f,.3f,.5f); Line(vh,.7f,.5f,.95f,.5f); break;
                case 3: Ring(vh,.3f,.48f,.22f); Ring(vh,.57f,.64f,.25f); Ring(vh,.75f,.42f,.18f); Line(vh,.23f,.2f,.78f,.2f); break;
                case 4: Path(vh,new[]{new Vector2(.1f,.75f),new Vector2(.9f,.75f),new Vector2(.8f,.3f),new Vector2(.5f,.12f),new Vector2(.2f,.3f)},true); Line(vh,.25f,.57f,.4f,.5f);Line(vh,.6f,.5f,.75f,.57f);break;
                case 5: Ring(vh,.35f,.7f,.14f);Ring(vh,.7f,.65f,.14f);Line(vh,.18f,.18f,.35f,.5f);Line(vh,.35f,.5f,.5f,.18f);Line(vh,.55f,.16f,.7f,.45f);Line(vh,.7f,.45f,.87f,.16f);break;
                case 6: Ring(vh,.5f,.45f,.32f);Line(vh,.2f,.15f,.8f,.8f);Line(vh,.2f,.8f,.8f,.15f);Line(vh,.5f,.1f,.5f,.85f);break;
                case 7: Path(vh,new[]{new Vector2(.2f,.85f),new Vector2(.8f,.85f),new Vector2(.8f,.4f),new Vector2(.5f,.1f),new Vector2(.2f,.4f)},true);break;
                case 8: Ring(vh,.5f,.5f,.38f);Line(vh,.5f,.25f,.5f,.75f);Line(vh,.35f,.65f,.65f,.65f);Line(vh,.35f,.35f,.65f,.35f);break;
                default: Ring(vh,.5f,.65f,.2f);Path(vh,new[]{new Vector2(.18f,.65f),new Vector2(.5f,.08f),new Vector2(.82f,.65f)},false);break;
            }
        }
        private void Ring(VertexHelper vh,float x,float y,float radius)
        { for(int i=0;i<28;i++){float a=i*Mathf.PI*2/28,b=(i+1)*Mathf.PI*2/28;Line(vh,x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius);} }
        private void Path(VertexHelper vh,Vector2[] points,bool closed)
        {for(int i=1;i<points.Length;i++)Line(vh,points[i-1].x,points[i-1].y,points[i].x,points[i].y);if(closed)Line(vh,points[points.Length-1].x,points[points.Length-1].y,points[0].x,points[0].y);}
        private void Line(VertexHelper vh,float x,float y,float xx,float yy)
        {
            var rect=GetPixelAdjustedRect(); float size=Mathf.Min(rect.width,rect.height);
            Vector2 a=rect.center+new Vector2(x-.5f,y-.5f)*size,b=rect.center+new Vector2(xx-.5f,yy-.5f)*size;
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*size*.043f;int index=vh.currentVertCount;
            vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);vh.AddVert(a-n,color,Vector2.zero);
            vh.AddTriangle(index,index+1,index+2);vh.AddTriangle(index,index+2,index+3);
        }
    }


}
