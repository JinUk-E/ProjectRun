using UnityEngine;
using MoraeGames.Core;
using MoraeGames.Game;
using MoraeGames.Network;

namespace MoraeGames.Ability
{
    /// <summary>Pooled, cosmetic Kenney particles. Gameplay ranges and timers remain owned by abilities.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class SkillVfx : MonoBehaviour
    {
        public enum Kind { Sprint, Smoke, Disguise, Decoy, Trap }
        private Kind _kind;
        private string _poolKey;
        private ParticleSystem _particles;
        private LineRenderer _boundary;
        private Transform _follow;
        private NetworkCharacter _network;
        private float _radius;
        public Kind Style => _kind;
        public int ParticleCount => _particles != null ? _particles.particleCount : 0;

        public static SkillVfx Play(AbilityHolder holder)
        {
            Kind kind;
            if(holder.abilityData is SpeedBurstData) kind=Kind.Sprint;
            else if(holder.abilityData is SmokeBombData) kind=Kind.Smoke;
            else if(holder.abilityData is DisguiseData) kind=Kind.Disguise;
            else if(holder.abilityData is DecoyCloneData) kind=Kind.Decoy;
            else if(holder.abilityData is EscapeTrapData) kind=Kind.Trap;
            else return null;
            string key="KenneySkill_"+kind;
            var obj=EffectPool.Get(key,()=>Create(kind,key));
            var effect=obj.GetComponent<SkillVfx>();
            effect._follow=kind==Kind.Sprint || kind==Kind.Disguise ? holder.transform : null;
            effect._network=holder.GetComponent<NetworkCharacter>();
            obj.transform.SetPositionAndRotation(holder.transform.position,Quaternion.identity);
            obj.transform.localScale=Vector3.one;
            effect._radius=holder.abilityData is SmokeBombData smoke?smoke.smokeRadius:holder.abilityData is EscapeTrapData trap?trap.radius:1;
            var shape=effect._particles.shape;
            shape.radius=kind==Kind.Smoke?effect._radius*.62f:kind==Kind.Trap?0:kind==Kind.Disguise?.8f:.45f;
            if(kind==Kind.Trap) { var main=effect._particles.main; main.startSize=effect._radius*1.9f; }
            effect._particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            effect._particles.Play(true);
            // Populate deployment immediately instead of waiting for the first emission tick.
            if(kind==Kind.Smoke) effect._particles.Emit(22);
            else if(kind==Kind.Decoy) effect._particles.Emit(26);
            else if(kind==Kind.Trap) effect._particles.Emit(1);
            else effect._particles.Emit(8);
            if(effect._boundary!=null) AbilityFeedback.DrawRing(effect._boundary,obj.transform.position+Vector3.up*.1f,effect._radius);
            return effect;
        }

        private static GameObject Create(Kind kind,string key)
        {
            var root=new GameObject(key);
            var effect=root.AddComponent<SkillVfx>(); effect._kind=kind; effect._poolKey=key;
            var child=new GameObject("KenneyParticles"); child.transform.SetParent(root.transform,false);
            child.transform.localPosition=Vector3.up*(kind==Kind.Trap?.2f:kind==Kind.Smoke?.5f:.6f);
            child.transform.localRotation=Quaternion.Euler(-90,0,0);
            var ps=child.AddComponent<ParticleSystem>(); effect._particles=ps;
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;
            main.playOnAwake=false; main.loop=kind!=Kind.Decoy; main.duration=1;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.scalingMode=ParticleSystemScalingMode.Shape;
            main.maxParticles=kind==Kind.Smoke?64:48;
            main.startLifetime=kind==Kind.Smoke?1.8f:kind==Kind.Decoy?.7f:kind==Kind.Trap?1.2f:.65f;
            main.startSpeed=kind==Kind.Decoy?3.2f:kind==Kind.Smoke?.12f:kind==Kind.Trap?0:.8f;
            main.startSize=kind==Kind.Smoke?new ParticleSystem.MinMaxCurve(3.5f,4.8f):new ParticleSystem.MinMaxCurve(.9f,kind==Kind.Sprint?2.2f:1.6f);
            main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            Color color=kind==Kind.Sprint?new Color(.5f,.95f,1,1):kind==Kind.Smoke?new Color(.75f,.85f,.95f,.65f):kind==Kind.Disguise?new Color(.85f,.5f,1,.85f):kind==Kind.Decoy?new Color(.4f,1,1,1):new Color(1,.7f,.15f,1);
            main.startColor=color;
            var emission=ps.emission; emission.rateOverTime=kind==Kind.Decoy?0:kind==Kind.Smoke?22:kind==Kind.Disguise?12:kind==Kind.Trap?2:24;
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Circle; shape.radius=.5f; shape.radiusThickness=1;
            var fade=ps.colorOverLifetime; fade.enabled=true;
            var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.6f,.65f),new GradientAlphaKey(0,1)});
            fade.color=gradient;
            var size=ps.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.5f,1,1.4f));
            var renderer=child.GetComponent<ParticleSystemRenderer>();
            if(kind==Kind.Trap) renderer.renderMode=ParticleSystemRenderMode.HorizontalBillboard;
            string texture=kind==Kind.Smoke?"smoke_01":kind==Kind.Sprint?"spark_06":kind==Kind.Disguise?"twirl_01":kind==Kind.Decoy?"star_04":"magic_02";
            renderer.sharedMaterial=Resources.Load<Material>("SkillVfx/"+texture);
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            if(kind==Kind.Smoke)
            {
                var edge=new GameObject("ConcealmentBoundary"); edge.transform.SetParent(root.transform,false);
                effect._boundary=AbilityFeedback.CreateRing(edge,new Color(.55f,.8f,1,.6f));
                effect._boundary.widthMultiplier=.07f;
            }
            // Register after renderers exist: particles obey the same local visibility as actors.
            root.AddComponent<LocalVisionSubject>();
            return root;
        }

        private void LateUpdate()
        {
            if(_follow!=null)
                transform.position=_network!=null && _network.IsSpawned?_network.PresentedPosition:_follow.position;
        }
        public void Release()
        {
            _follow=null; _network=null;
            _particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            EffectPool.Release(_poolKey,gameObject);
        }
    }
}
