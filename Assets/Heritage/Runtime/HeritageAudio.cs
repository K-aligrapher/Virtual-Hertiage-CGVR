using UnityEngine;

namespace VirtualHeritage
{
    public sealed class HeritageAudio : MonoBehaviour
    {
        AudioSource ambience, bell, ui;
        AudioClip windClip, bellClip, clickClip;
        float nextBell;
        public bool Muted { get; private set; }

        public void Initialize()
        {
            windClip = Wind(); bellClip = Bell(); clickClip = Click();
            ambience = gameObject.AddComponent<AudioSource>(); ambience.clip = windClip; ambience.loop = true; ambience.volume = .15f;
            ambience.spatialBlend = .65f; ambience.minDistance = 8; ambience.maxDistance = 55; ambience.Play();
            var source = new GameObject("Spatial temple bell"); source.transform.SetParent(transform,false); source.transform.localPosition = new Vector3(2,4,7);
            bell = source.AddComponent<AudioSource>(); bell.clip = bellClip; bell.spatialBlend = 1; bell.rolloffMode = AudioRolloffMode.Linear;
            bell.minDistance = 2; bell.maxDistance = 45; bell.volume = .25f;
            ui = gameObject.AddComponent<AudioSource>(); ui.clip = clickClip; ui.spatialBlend = 0; ui.volume = .18f;
            nextBell = Time.time + 8;
        }

        void Update() { if (Time.time > nextBell && bell) { bell.Play(); nextBell = Time.time + 18; } }
        public void PlayClick() { if (ui && !Muted) ui.PlayOneShot(clickClip); }
        public void Toggle() { Muted = !Muted; ambience.mute = bell.mute = ui.mute = Muted; }

        static AudioClip Wind()
        {
            const int rate = 22050, count = rate * 12;
            var data = new float[count]; var random = new System.Random(42); float filtered = 0;
            for (int i = 0; i < count; i++)
            {
                filtered = filtered * .975f + ((float)random.NextDouble() * 2 - 1) * .025f;
                float envelope = .65f + .35f * Mathf.Sin(i * Mathf.PI * 2 / count);
                data[i] = filtered * envelope;
            }
            // Crossfade the seam to keep the generated ambience loop click-free.
            for (int i = 0; i < 512; i++) data[count-512+i] = Mathf.Lerp(data[count-512+i],data[i],i/511f);
            var clip = AudioClip.Create("Original synthesized courtyard air",count,1,rate,false); clip.SetData(data,0); return clip;
        }

        static AudioClip Bell()
        {
            const int rate = 22050; var data = new float[rate * 5];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i/(float)rate;
                data[i] = (Mathf.Sin(t*2*Mathf.PI*392)*Mathf.Exp(-t*1.1f) + .35f*Mathf.Sin(t*2*Mathf.PI*1047)*Mathf.Exp(-t*1.9f) + .18f*Mathf.Sin(t*2*Mathf.PI*1712)*Mathf.Exp(-t*2.6f)) * .38f * Mathf.Min(1,t*90);
            }
            var clip = AudioClip.Create("Original synthesized bronze bell",data.Length,1,rate,false); clip.SetData(data,0); return clip;
        }

        static AudioClip Click()
        {
            const int rate = 22050; var data = new float[1800];
            for (int i = 0; i < data.Length; i++) { float t = i/(float)rate; data[i] = Mathf.Sin(t*2*Mathf.PI*780)*Mathf.Exp(-t*65)*Mathf.Min(1,t*500)*.3f; }
            var clip = AudioClip.Create("Original UI chime",data.Length,1,rate,false); clip.SetData(data,0); return clip;
        }

        void OnDestroy() { Destroy(windClip); Destroy(bellClip); Destroy(clickClip); }
    }
}
