using System;
using System.IO;
using UnityEngine;

namespace SoccerFight.EditorTools
{
    /// <summary>Freigestellte Figur auf einem weichen Gelenkgitter und ein unabhängig geführter Ball.</summary>
    public sealed class DribbleLoopScene : IDisposable
    {
        readonly Texture2D original, background, figure, pattern, shade, highlight;
        readonly Material layer, composite;
        readonly RenderTexture scene;
        readonly Mesh body, ball;
        readonly Vector3[] rest, vertices;
        readonly bool hoops;
        readonly int index;
        readonly Vector2 hip, hand, freeHand, boot, knee, ballRest, ballHigh;
        readonly float radius;
        readonly Rect bounds;

        static float Smooth(float lo,float hi,float v) => Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(lo,hi,v));
        static float Weight(Vector2 p,Vector2 center,Vector2 scale)
        {
            var q=p-center; q.x/=scale.x; q.y/=scale.y;
            return Mathf.Exp(-q.sqrMagnitude*1.6f);
        }
        static Vector2 Rotate(Vector2 p,float degrees)
        {
            float a=degrees*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);
            return new Vector2(p.x*c-p.y*s,p.x*s+p.y*c);
        }

        public DribbleLoopScene(string name,int character,Texture2D source,string sourceRoot,int width,int height)
        {
            original=source; index=character; hoops=index>=3;
            background=Load(sourceRoot,name+"-background");
            figure=Load(sourceRoot,name+"-figure");
            bounds=index==0?Rect.MinMaxRect(258,84,729,880):index==1?Rect.MinMaxRect(256,116,731,881):
                index==2?Rect.MinMaxRect(272,78,760,881):index==3?Rect.MinMaxRect(259,73,733,891):
                index==4?Rect.MinMaxRect(197,72,735,887):Rect.MinMaxRect(209,134,790,862);
            hip=index==4?new Vector2(465,490):index==5?new Vector2(510,545):new Vector2(index==2?500:480,498);
            ballRest=index<2?new Vector2(657,834):index==2?new Vector2(684,834):index==3?new Vector2(366,475):
                index==4?new Vector2(635,507):new Vector2(290,550);
            radius=hoops?(index==3?64:index==4?72:76):76;
            ballHigh=index==3?new Vector2(200,605):index==4?new Vector2(820,610):new Vector2(160,600);
            hand=index==3?new Vector2(334,512):index==4?new Vector2(670,539):new Vector2(267,496);
            freeHand=index==4?new Vector2(315,466):new Vector2(index==5?723:658,index==5?623:548);
            boot=new Vector2(index==2?690:660,745);
            knee=new Vector2(index==2?588:565,573);
            layer=new Material(Resources.Load<Shader>("Menu/Animation/DribbleLayer"));
            composite=new Material(Resources.Load<Shader>("Menu/Animation/DribbleComposite"));
            composite.SetTexture("_OriginalTex",original);
            scene=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            scene.Create();
            _=Palette.Skin;
            pattern=(hoops?Art.HoopPatternCanvas(384):Art.BallPatternCanvas(384)).ToTexture("Loop-Ball",true,false,premultiply:false);
            shade=Art.BallShadeCanvas(384).ToTexture("Loop-Balllicht",true,false,premultiply:false);
            highlight=Art.BallHighlightCanvas(384).ToTexture("Loop-Ballglanz",true,false,premultiply:false);
            body=BuildBody(figure,bounds,out rest,out vertices);
            ball=new Mesh { name="Ball-Viereck" };
            ball.vertices=new Vector3[4];
            ball.uv=new[] {new Vector2(0,1),new Vector2(1,1),new Vector2(1,0),new Vector2(0,0)};
            ball.triangles=new[] {0,1,2,0,2,3};
        }

        static Texture2D Load(string folder,string name)
        {
            var t=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if (!t.LoadImage(File.ReadAllBytes(Path.Combine(folder,name+".png")))) throw new Exception("Animationsvorlage fehlt: "+name);
            t.wrapMode=TextureWrapMode.Clamp;
            return t;
        }

        static Mesh BuildBody(Texture2D tex,Rect target,out Vector3[] rest,out Vector3[] vertices)
        {
            // Die Freistellung wird auf die exakte Größe und Position der Originalfigur eingepasst.
            var pixels=tex.GetPixels32(); int x0=tex.width,y0=tex.height,x1=0,y1=0;
            for (int y=0;y<tex.height;y++) for (int x=0;x<tex.width;x++)
                if (pixels[y*tex.width+x].a>80)
                { x0=Mathf.Min(x0,x);x1=Mathf.Max(x1,x);y0=Mathf.Min(y0,y);y1=Mathf.Max(y1,y); }
            if (x1<=x0||y1<=y0) throw new Exception("Figur ohne transparente Freistellung");
            const int cols=58,rows=94;
            rest=new Vector3[(cols+1)*(rows+1)]; vertices=new Vector3[rest.Length];
            var uv=new Vector2[rest.Length];
            for (int y=0;y<=rows;y++) for (int x=0;x<=cols;x++)
            {
                int n=y*(cols+1)+x; float u=x/(float)cols,v=y/(float)rows;
                rest[n]=new Vector3(target.xMin+target.width*u,target.yMin+target.height*v,0);
                uv[n]=new Vector2(Mathf.Lerp(x0,x1,u)/tex.width,Mathf.Lerp(y1,y0,v)/tex.height);
            }
            var triangles=new int[cols*rows*6]; int at=0;
            for (int y=0;y<rows;y++) for (int x=0;x<cols;x++)
            { int n=y*(cols+1)+x;triangles[at++]=n;triangles[at++]=n+1;triangles[at++]=n+cols+2;
              triangles[at++]=n;triangles[at++]=n+cols+2;triangles[at++]=n+cols+1; }
            var mesh=new Mesh {name="Weiches Figurengitter"};
            mesh.vertices=rest;mesh.uv=uv;mesh.triangles=triangles;mesh.MarkDynamic();
            return mesh;
        }

        public void Render(float time,RenderTexture destination)
        {
            float active=Smooth(0.8f,1.6f,time)*(1f-Smooth(5.3f,6.8f,time));
            float cycles=Mathf.Clamp((time-1.6f)/0.8f,0f,3f);
            float u=Mathf.Repeat(0.5f+cycles,1f),air=4f*u*(1f-u);
            float roll=Mathf.Sin(cycles*Mathf.PI*2f);
            float pose=Smooth(4f,4.7f,time)*(1f-Smooth(5.3f,6.8f,time));
            Vector2 ballPos;
            if (hoops)
            {
                var high=ballHigh+new Vector2(index==5?18f*roll:0f,0f);
                float floor=bounds.yMax+6f-radius;
                ballPos=Vector2.Lerp(ballRest,new Vector2(high.x,floor-(floor-high.y)*air),active);
            }
            else ballPos=ballRest+active*new Vector2(160f+46f*roll,0f);
            float lean=active*(hoops?3.2f:2.4f)-pose*5.5f;
            for (int i=0;i<rest.Length;i++)
            {
                Vector2 p=rest[i],q=p;
                float upper=1f-Smooth(520f,800f,p.y);
                q+=((Rotate(p-hip,lean)+hip)-p)*upper;
                q+=new Vector2(active*(hoops?9f:5f),active*(hoops?16f:7f))*(1f-Smooth(730f,bounds.yMax-15f,p.y));
                if (hoops)
                {
                    Vector2 highPalm=ballHigh-Vector2.up*radius;
                    Vector2 delta=(highPalm-hand)+new Vector2(index==5?18f*roll:0f,(1f-air)*22f);
                    float w=Weight(p,hand,new Vector2(index==4?240f:190f,150));
                    q+=delta*active*w;
                    q+=new Vector2(index==4?-24f:14f,-48f)*pose*Weight(p,freeHand,new Vector2(95,155));
                    // Das Knie beugt sich leicht, die Sohlen bleiben auf dem Boden.
                    q+=new Vector2(index==4?-7f:8f,7f)*active*Weight(p,new Vector2(hip.x+55,670),new Vector2(145,120));
                }
                else
                {
                    float a=Weight(p,boot,new Vector2(155,175)),b=Weight(p,knee,new Vector2(110,125));
                    float total=Mathf.Max(1f,a+b);
                    var footDelta=new Vector2(34f+29f*roll,67f-22f*Mathf.Max(0f,roll));
                    var kneeDelta=new Vector2(12f,34f);
                    q+=(footDelta*a+kneeDelta*b)*(active/total);
                    q+=new Vector2(16f,-42f)*pose*Weight(p,new Vector2(650,530),new Vector2(90,150));
                }
                vertices[i]=q;
            }
            body.vertices=vertices;body.RecalculateBounds();
            Graphics.Blit(background,scene);
            RenderTexture.active=scene;
            GL.PushMatrix();GL.LoadPixelMatrix(0f,1672f,941f,0f);
            DrawShadow(new Vector2(bounds.xMin+70f,bounds.yMax-1f),88f,13f,0.23f);
            if (hoops) DrawShadow(new Vector2(bounds.xMax-72f,bounds.yMax-1f),88f,13f,0.23f);
            float lift=hoops?Mathf.Clamp01((bounds.yMax+6f-radius-ballPos.y)/220f):0f;
            DrawShadow(new Vector2(ballPos.x,hoops?bounds.yMax+6f:ballRest.y+radius+1f),radius*Mathf.Lerp(1.15f,0.63f,lift),14f,Mathf.Lerp(0.28f,0.14f,lift));
            DrawBall(pattern,ballPos,radius,hoops?-cycles*85f:-(ballPos.x-ballRest.x)/radius*Mathf.Rad2Deg);
            DrawBall(shade,ballPos,radius,0f);DrawBall(highlight,ballPos,radius,0f);
            layer.mainTexture=figure;layer.SetFloat("_SoftCutout",1f);layer.SetPass(0);
            Graphics.DrawMeshNow(body,Matrix4x4.identity);
            GL.PopMatrix();RenderTexture.active=null;
            // Anfang und Ende zeigen wirklich das unveränderte Original, einschließlich des Originalballs.
            float restore=1f-Smooth(0.8f,1.15f,time)*(1f-Smooth(6.45f,6.8f,time));
            composite.SetFloat("_Restore",restore);
            Graphics.Blit(scene,destination,composite);
        }

        void DrawShadow(Vector2 center,float rx,float ry,float strength)
        {
            ball.vertices=new[] {new Vector3(center.x-rx,center.y-ry),new Vector3(center.x+rx,center.y-ry),
                new Vector3(center.x+rx,center.y+ry),new Vector3(center.x-rx,center.y+ry)};
            ball.RecalculateBounds();
            layer.SetFloat("_Shadow",strength);layer.SetPass(0);
            Graphics.DrawMeshNow(ball,Matrix4x4.identity);
        }

        void DrawBall(Texture2D texture,Vector2 center,float r,float angle)
        {
            // Canvas enthält einen kleinen transparenten Sicherheitsrand.
            r*=Art.BallExt/Art.BallRadius;
            var corners=new[] {new Vector2(-r,-r),new Vector2(r,-r),new Vector2(r,r),new Vector2(-r,r)};
            var positions=new Vector3[4];
            for (int i=0;i<4;i++) positions[i]=center+Rotate(corners[i],angle);
            ball.vertices=positions;ball.RecalculateBounds();
            layer.mainTexture=texture;layer.SetFloat("_Shadow",0f);layer.SetFloat("_SoftCutout",0f);layer.SetPass(0);
            Graphics.DrawMeshNow(ball,Matrix4x4.identity);
        }

        public void Dispose()
        {
            scene.Release();
            foreach (UnityEngine.Object o in new UnityEngine.Object[] {background,figure,pattern,shade,highlight,body,ball,layer,composite,scene})
                UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
