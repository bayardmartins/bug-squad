using System.Collections.Generic;
using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Lightweight pooled audio source utility.
    /// Replaces AudioSource.PlayClipAtPoint to avoid per-call GameObject allocation.
    /// Usage: AudioPool.Play(clip, position) or AudioPool.Play(clip, position, volume)
    /// </summary>
    public class AudioPool : MonoBehaviour
    {
        private static AudioPool instance;

        [SerializeField] private int poolSize = 16;

        private readonly Queue<AudioSource> available = new Queue<AudioSource>();
        private readonly List<AudioSource> active = new List<AudioSource>();

        /// <summary>
        /// Ensures a singleton instance exists.
        /// Auto-creates on first use if not placed in scene.
        /// </summary>
        private static AudioPool Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[AudioPool]");
                    instance = go.AddComponent<AudioPool>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            // Pre-warm pool
            for (int i = 0; i < poolSize; i++)
                available.Enqueue(CreateSource());
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            // Return finished sources to pool
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (!active[i].isPlaying)
                {
                    var src = active[i];
                    src.clip = null;
                    active.RemoveAt(i);
                    available.Enqueue(src);
                }
            }
        }

        /// <summary>
        /// Plays a clip at the specified world position using a pooled AudioSource.
        /// </summary>
        public static void Play(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            Instance.PlayInternal(clip, position, volume);
        }

        private void PlayInternal(AudioClip clip, Vector3 position, float volume)
        {
            AudioSource src = available.Count > 0 ? available.Dequeue() : CreateSource();

            src.transform.position = position;
            src.clip = clip;
            src.volume = volume;
            src.Play();
            active.Add(src);
        }

        private AudioSource CreateSource()
        {
            var child = new GameObject("PooledAudio");
            child.transform.SetParent(transform);
            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f; // 3D sound
            return src;
        }
    }
}
