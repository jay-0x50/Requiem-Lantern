using System;
using System.Collections.Generic;
using UnityEngine;

namespace RequiemLantern.Presentation
{
    // Shared PCM foley assets; this random-free variation never touches battle RNG.
    public sealed class CombatAudioBank : IDisposable
    {
        public static readonly string[] Families={"clash","hit_cut","hit_blunt","hit_heavy","swing","brace","ignite","break"};
        sealed class Voice { public AudioSource Source; public float Gain, Started; }
        readonly Dictionary<string,AudioClip[]> clips=new Dictionary<string,AudioClip[]>();
        readonly Dictionary<string,int> next=new Dictionary<string,int>();
        readonly Voice[] voices=new Voice[8];
        readonly GameObject root;
        public int PlayedCount { get; private set; }
        public int ContactCount { get; private set; }
        public int MissingCount { get; private set; }
        public string LastFamily { get; private set; }
        public int ActiveCount { get { int count=0;foreach(var voice in voices)if(voice.Source&&voice.Source.isPlaying)count++;return count; } }

        public CombatAudioBank(Transform owner)
        {
            root=new GameObject("Combat foley");root.transform.SetParent(owner,false);
            for(int i=0;i<voices.Length;i++){
                var go=new GameObject("Foley voice "+i);go.transform.SetParent(root.transform,false);
                var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;
                source.spatialBlend=0;source.pitch=1;source.priority=128;
                voices[i]=new Voice{Source=source};
            }
            foreach(string family in Families){
                var choices=new AudioClip[3];
                for(int i=0;i<3;i++){
                    choices[i]=Resources.Load<AudioClip>("Audio/Combat/"+family+"_"+(i+1).ToString("00"));
                    if(!choices[i]){MissingCount++;continue;}
                    if(choices[i].loadState==AudioDataLoadState.Unloaded)choices[i].LoadAudioData();
                }
                clips[family]=choices;next[family]=0;
            }
        }

        public bool Play(string family,bool enabled,bool contact=false,float level=1)
        {
            if(!enabled){Stop();return false;}
            if(!clips.TryGetValue(family,out var choices))return false;
            int index=next[family];AudioClip clip=null;
            for(int i=0;i<3;i++){
                int candidate=(index+i)%3;
                if(choices[candidate]&&choices[candidate].loadState==AudioDataLoadState.Loaded){clip=choices[candidate];index=candidate;break;}
            }
            // A late-loading clip is skipped at its cue, never replayed after contact.
            if(!clip)return false;next[family]=(index+1)%3;
            Voice selected=null;
            foreach(var voice in voices)if(!voice.Source.isPlaying){selected=voice;break;}
            if(selected==null){
                selected=voices[0];foreach(var voice in voices)if(voice.Started<selected.Started)selected=voice;
                selected.Source.Stop();
            }
            selected.Gain=Gain(family)*Mathf.Clamp01(level);selected.Started=Time.unscaledTime;
            selected.Source.clip=clip;selected.Source.volume=0;selected.Source.Play();
            Rebalance();PlayedCount++;if(contact)ContactCount++;LastFamily=family;return true;
        }

        static float Gain(string family)
        {
            switch(family){
                case "clash":return .50f;case "hit_cut":return .45f;case "hit_blunt":return .52f;
                case "hit_heavy":return .58f;case "swing":return .18f;case "brace":return .34f;
                case "ignite":return .20f;case "break":return .48f;default:return .35f;
            }
        }
        public void Update(bool enabled)
        {if(!enabled)Stop();else Rebalance();}
        void Rebalance()
        {
            int active=Math.Max(1,ActiveCount);
            // Sum of gains remains <= .58 even if every voice peaks together.
            foreach(var voice in voices)if(voice.Source&&voice.Source.isPlaying)voice.Source.volume=voice.Gain/active;
        }
        public void Stop()
        {foreach(var voice in voices)if(voice.Source)voice.Source.Stop();}
        public void Dispose()
        {Stop();if(root)UnityEngine.Object.Destroy(root);}
    }
}
