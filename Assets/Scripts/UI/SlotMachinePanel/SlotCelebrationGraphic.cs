using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // One UI mesh for the bursts, ribbons and soft light; the cannon artwork lives in the prefab.
    public sealed class SlotCelebrationGraphic : MaskableGraphic
    {
        [SerializeField] private CanvasGroup _cannons;
        [SerializeField] private RectTransform[] _barrels;
        [SerializeField] private AudioSource _sound;
        [SerializeField] private AudioClip _cannonSound;
        private SlotPrizeTier _tier;
        private float _started;
        private float _duration;
        private int _salvo = -1;

        public bool IsPlaying => _tier != SlotPrizeTier.None;
        public float Elapsed => Time.unscaledTime - _started;

        public void Play(SlotPrizeTier tier)
        {
            Clear();
            _tier = tier;
            _started = Time.unscaledTime;
            _duration = tier == SlotPrizeTier.Small ? 2f : tier == SlotPrizeTier.Medium ? 3.8f : 7f;
            Tick();
        }

        public void Clear()
        {
            _tier = SlotPrizeTier.None;
            _salvo = -1;
            if (_cannons != null) _cannons.alpha = 0f;
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            Clear();
            base.OnDisable();
        }

        private void Update()
        {
            if (IsPlaying) Tick();
        }

        private void Tick()
        {
            float time = Elapsed;
            if (time >= _duration)
            {
                Clear();
                return;
            }
            if (_tier == SlotPrizeTier.Grand)
            {
                int salvo = Mathf.Min(2, Mathf.FloorToInt(time / 0.85f));
                if (salvo > _salvo)
                {
                    _salvo = salvo;
                    if (_cannonSound != null && AudioPreferences.SoundVolume > 0f)
                        _sound.PlayOneShot(_cannonSound, 0.72f);
                }
                _cannons.alpha = Mathf.Clamp01(time / 0.12f) * Mathf.Clamp01((5f - time) / 0.7f);
                float recoil = Mathf.Exp(-(time - _salvo * 0.85f) * 14f);
                foreach (var barrel in _barrels)
                    barrel.localScale = new Vector3(1f, 1f - recoil * 0.16f, 1f);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!IsPlaying) return;
            float time = Elapsed;
            bool grand = _tier == SlotPrizeTier.Grand;
            bool small = _tier == SlotPrizeTier.Small;
            int waves = grand ? 3 : 1;
            int count = small ? 18 : grand ? 84 : 70;
            float fade = Mathf.Clamp01((_duration - time) / 0.65f);
            if (grand) AddRays(mesh, time, fade);
            for (int wave = 0; wave < waves; wave++)
            {
                float age = time - wave * 0.85f;
                if (age < 0f) continue;
                for (int i = 0; i < count; i++)
                {
                    int seed = i + wave * 101;
                    float t = age - Hash(seed + 17) * (small ? 0.18f : 0.22f);
                    if (t < 0f) continue;
                    Vector2 position = Flight(seed, i % 2, t, small, grand);
                    if (!InStage(position)) continue;
                    Color tint = Tint(seed, small, grand);
                    tint.a = fade * Mathf.Clamp01(t / 0.04f);
                    float size = (small ? 5f : 7f) + Hash(seed + 43) * 6f;
                    float flip = 0.2f + 0.8f * Mathf.Abs(Mathf.Cos(t * 9f + seed));
                    AddPaper(mesh, position, size * flip, size * 0.55f,
                        t * (3f + Hash(seed + 5) * 9f) + seed, tint);
                }
                if (!small)
                {
                    int ribbons = grand ? 12 : 6;
                    for (int i = 0; i < ribbons; i++)
                    {
                        int seed = i * 7 + wave * 101;
                        for (int segment = 0; segment < 10; segment++)
                        {
                            float t = age - segment * 0.022f;
                            if (t <= 0.025f) continue;
                            Vector2 a = Flight(seed, i % 2, t, false, grand);
                            Vector2 b = Flight(seed, i % 2, t - 0.025f, false, grand);
                            a.x += Mathf.Sin(t * 13f + i) * 8f;
                            b.x += Mathf.Sin((t - 0.025f) * 13f + i) * 8f;
                            if (!InStage(a) || !InStage(b)) continue;
                            Color tint = Tint(seed, false, grand);
                            tint.a = fade * (1f - segment / 12f);
                            AddRibbon(mesh, a, b, 3f, tint);
                        }
                    }
                }
                if (grand && age < 0.4f)
                {
                    var glow = new Color(1f, 0.78f, 0.24f, (1f - age / 0.4f) * 0.7f);
                    AddGlow(mesh, new Vector2(-740f, -300f), 28f + age * 150f, glow);
                    AddGlow(mesh, new Vector2(280f, -300f), 28f + age * 150f, glow);
                }
            }
        }

        private static float Hash(int seed) => Mathf.Repeat(Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f, 1f);

        private static Vector2 Flight(int seed, int side, float time, bool small, bool grand)
        {
            float direction = side == 0 ? 1f : -1f;
            float x = small ? -230f : grand ? (side == 0 ? -740f : 280f) : (side == 0 ? -630f : 170f);
            float y = small ? -180f : grand ? -300f : -190f;
            float vx = small ? (Hash(seed + 7) - 0.5f) * 520f : direction * (100f + Hash(seed + 7) * 560f);
            float vy = small ? 180f + Hash(seed + 31) * 150f : grand ? 670f + Hash(seed + 31) * 180f : 430f + Hash(seed + 31) * 200f;
            float gravity = small ? 450f : grand ? 520f : 460f;
            return new Vector2(x + vx * (1f - Mathf.Exp(-0.7f * time)) / 0.7f + Mathf.Sin(time * 5f + seed) * time * 6f,
                y + vy * time - gravity * time * time * 0.5f);
        }

        private static bool InStage(Vector2 p) => p.x > -850f && p.x < 480f && p.y > -470f && p.y < 390f;

        private static Color Tint(int seed, bool small, bool grand)
        {
            if (small || seed % 3 == 0) return new Color(1f, 0.79f + Hash(seed) * 0.15f, 0.3f);
            if (grand) return seed % 3 == 1 ? new Color(1f, 0.22f, 0.52f) : new Color(0.86f, 0.52f, 1f);
            return seed % 3 == 1 ? new Color(0.25f, 0.85f, 1f) : new Color(0.85f, 0.95f, 1f);
        }

        private static void AddPaper(VertexHelper mesh, Vector2 p, float w, float h, float angle, Color tint)
        {
            var x = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * w * 0.5f;
            var y = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * h * 0.5f;
            AddQuad(mesh, p - x - y, p - x + y, p + x + y, p + x - y, tint);
        }

        private static void AddRibbon(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 delta = (b - a).normalized;
            var normal = new Vector2(-delta.y, delta.x) * width;
            AddQuad(mesh, a - normal, a + normal, b + normal, b - normal, tint);
        }

        private static void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddGlow(VertexHelper mesh, Vector2 p, float radius, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(p, tint, Vector2.zero);
            tint.a = 0f;
            for (int i = 0; i <= 20; i++)
            {
                float angle = i * Mathf.PI * 0.1f;
                mesh.AddVert(p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private static void AddRays(VertexHelper mesh, float time, float fade)
        {
            // Local amber rays, never a full-screen flash. Stay below the navigation area.
            var center = new Vector2(-230f, -50f);
            for (int i = 0; i < 14; i++)
            {
                float angle = i * Mathf.PI / 7f + time * 0.07f;
                var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var width = new Vector2(-axis.y, axis.x) * 36f;
                var tip = center + axis * 430f;
                var tint = new Color(1f, 0.68f, 0.18f, fade * Mathf.Clamp01(time * 3f) * 0.16f);
                int start = mesh.currentVertCount;
                mesh.AddVert(center + axis * 70f, tint, Vector2.zero);
                tint.a = 0f;
                mesh.AddVert(tip - width, tint, Vector2.zero);
                mesh.AddVert(tip + width, tint, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
            }
        }
    }
}
