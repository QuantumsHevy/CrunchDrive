using System.Collections.Generic;
using UnityEngine;

namespace CrunchDrives
{
    // The blinding white tube around the craft while a crunch field is up: a spindle along the ring's axis, widest at the
    // ring and closing to a point past each end of the craft, with soft layers around it that blur its edge. It is solid
    // white over the whole craft and starts to fade out exactly at the craft's outermost points (no padding past them),
    // easing to nothing at the tips instead of cutting off. It grows from the ring to its full length as it fades in, and
    // shrinks back as it fades out. The flash, the tube and its light are one effect: all on, or all off. While the field is
    // up the tube's length pulses with the flicker (it only ever stretches past its measured length, never shrinks below it,
    // so the craft stays covered), and however small the craft, each end of the tube is at least one ring diameter long.
    //
    // A brief, overwhelming flash belongs to it: a camera-facing glow (a flat quad that always turns toward the camera) at
    // the ring, every time the craft enters or leaves the crunch field.
    //
    // It is parented to the drive part, so it follows the part's position, rotation and the floating origin for free.
    // Its size is measured from the craft's meshes at the start of each spool. Everything is built in code.
    //
    // Assumes the ring's axis is the part's local Y axis and the part's origin is the centre of the ring along it.
    public class CrunchTube
    {
        const int CornerCount = 8;
        const int MaxMeshVertices = 60000;   // A mesh holds 65535 vertices with the default 16-bit indices; the halo is thinned to fit.

        // One corner of one mesh's bounding box, measured from the ring: how far along the axis, and how far from it.
        struct ScanCorner
        {
            public float along;
            public float radial;
            public int part;
        }

        GameObject root;
        MeshFilter forwardFilter;
        MeshFilter backwardFilter;
        Renderer forwardRenderer;
        Renderer backwardRenderer;
        Transform forwardTransform;
        Transform backwardTransform;
        Light lamp;
        Material material;
        Material glowMaterial;
        Glow flashGlow;
        string colorProperty;
        float colorBase = 1f;

        // The craft, as last scanned.
        readonly List<ScanCorner> scan = new List<ScanCorner>();
        readonly List<string> scanParts = new List<string>();
        float scanRingRadius, scanMaxAlong, scanMinAlong, scanBoundRadius;
        int scanRenderers;
        bool scanned;

        float radius = 1f;       // Tube radius, meters.
        float forward = 2f;      // Tube length on the +Y side of the ring, meters.
        float backward = 2f;     // Tube length on the -Y side of the ring, meters.
        float flashRadius = 1f;  // Radius of a sphere around the ring's centre that holds the whole craft, meters.
        float level;             // 0..1: how far the tube has faded in and grown out.
        float clock;             // Seconds, for the flicker.
        float flashClock = -1f;  // Seconds since the flash began; negative when no flash is running.
        float flashAge;          // 0..1: how far through its time the running flash is.
        float[] forwardAlpha, backwardAlpha;      // The meshes' own opacities, kept so they can be faded by hand when the shader has no colour property.
        Color[] forwardWork, backwardWork;
        bool shown;              // The tube is switched on.
        bool flashShown;         // The flash is switched on.
        bool wasOn;              // The crunch field was up at the last Tick.
        bool built;              // The meshes have been built from a measurement.

        public bool IsBuilt { get { return built; } }   // A measurement has been made, so the tube can be shown.

        static Shader shader;
        static Texture2D glowTexture;
        static readonly Vector3[] corners = new Vector3[CornerCount];
        static readonly string[] ShaderNames =
        {
            "KSP/Particles/Alpha Blended",
            "Legacy Shaders/Particles/Alpha Blended",
            "KSP/Particles/Additive",
            "Legacy Shaders/Particles/Additive"
        };

        // ---- Setup ----

        public void Attach(Part part)
        {
            Dispose();
            if (part == null) return;

            if (shader == null)
            {
                for (int i = 0; i < ShaderNames.Length && shader == null; i++) shader = Shader.Find(ShaderNames[i]);
                if (shader == null) { Debug.Log(CrunchText.LogNoTubeShader); return; }
                if (shader.name.Contains("Additive")) Debug.Log(CrunchText.LogTubeAdditive);
            }

            material = new Material(shader);
            material.mainTexture = Texture2D.whiteTexture;
            if (material.HasProperty("_TintColor")) { colorProperty = "_TintColor"; colorBase = 0.5f; }   // These shaders double the tint.
            else if (material.HasProperty("_Color")) { colorProperty = "_Color"; colorBase = 1f; }
            else colorProperty = null;

            // The flash fades through its vertex colours, so its material's tint stays at full.
            glowMaterial = new Material(shader);
            glowMaterial.mainTexture = GlowTexture();
            if (colorProperty != null) glowMaterial.SetColor(colorProperty, new Color(colorBase, colorBase, colorBase, colorBase));

            root = new GameObject("CrunchDriveTube");
            root.layer = part.gameObject.layer;
            root.transform.SetParent(part.transform, false);

            forwardRenderer = MakeHalf("Forward", out forwardFilter);
            backwardRenderer = MakeHalf("Backward", out backwardFilter);
            forwardTransform = forwardFilter.transform;
            backwardTransform = backwardFilter.transform;

            flashGlow = new Glow("Flash", root.transform, glowMaterial, root.layer);

            lamp = root.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = Color.white;
            lamp.shadows = LightShadows.None;

            shown = true;        // Forces the first SetShown to actually switch everything off.
            flashShown = true;
            built = false;
            scanned = false;
            wasOn = false;
            flashClock = -1f;
            level = 0f;
            SetShown(false, false);
        }

        Renderer MakeHalf(string name, out MeshFilter filter)
        {
            GameObject go = new GameObject(name);
            go.layer = root.layer;
            go.transform.SetParent(root.transform, false);
            filter = go.AddComponent<MeshFilter>();   // No collider is ever added: the tube must not touch anything.
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        public void Dispose()
        {
            DestroyMesh(forwardFilter);
            DestroyMesh(backwardFilter);
            if (flashGlow != null) flashGlow.Dispose();
            if (root != null) Object.Destroy(root);
            if (material != null) Object.Destroy(material);
            if (glowMaterial != null) Object.Destroy(glowMaterial);
            root = null;
            material = null;
            glowMaterial = null;
            flashGlow = null;
            forwardFilter = null;
            backwardFilter = null;
            forwardTransform = null;
            backwardTransform = null;
            scan.Clear();
            scanParts.Clear();
            scanned = false;
            shown = false;
            flashShown = false;
            wasOn = false;
            flashClock = -1f;
            built = false;
        }

        static void DestroyMesh(MeshFilter f)
        {
            if (f != null && f.sharedMesh != null) Object.Destroy(f.sharedMesh);
        }

        // ---- Measuring ----

        // Works out the tube's size from the meshes on the craft and builds its meshes. The radius comes from the drive
        // part's own meshes (its widest point, which is the ring), the lengths from every part's meshes along the ring's axis.
        public void Measure(Part drive, Vessel vessel)
        {
            if (root == null || drive == null || vessel == null) return;
            Scan(drive, vessel);
            Rebuild();
        }

        // Looks at every mesh on the craft once and keeps where the corners of their bounding boxes are, measured from the ring.
        // Rebuild works from that.
        void Scan(Part drive, Vessel vessel)
        {
            Transform pt = drive.transform;
            Vector3 center = pt.position;
            Vector3 axis = pt.up;

            scan.Clear();
            scanParts.Clear();
            scanRingRadius = 0f;
            scanMaxAlong = 0f;
            scanMinAlong = 0f;
            scanBoundRadius = 0f;
            scanRenderers = 0;

            // The ring: measured in the part's own space, where the ring's axis is Y.
            foreach (Renderer r in MeshRenderers(drive))
            {
                if (!Usable(r)) continue;
                int n = ObbCorners(r);
                for (int i = 0; i < n; i++)
                {
                    Vector3 local = pt.InverseTransformPoint(corners[i]);
                    scanRingRadius = Mathf.Max(scanRingRadius, Mathf.Abs(local.x), Mathf.Abs(local.z));
                }
            }

            // The craft: every part, projected onto the ring's axis.
            for (int p = 0; p < vessel.parts.Count; p++)
            {
                scanParts.Add(vessel.parts[p].name);
                foreach (Renderer r in MeshRenderers(vessel.parts[p]))
                {
                    if (!Usable(r)) continue;
                    scanRenderers++;
                    int n = ObbCorners(r);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 offset = corners[i] - center;
                        float along = Vector3.Dot(offset, axis);
                        float radial = Mathf.Sqrt(Mathf.Max(0f, offset.sqrMagnitude - along * along));
                        if (along > scanMaxAlong) scanMaxAlong = along;
                        if (along < scanMinAlong) scanMinAlong = along;
                        scanBoundRadius = Mathf.Max(scanBoundRadius, offset.magnitude);
                        scan.Add(new ScanCorner { along = along, radial = radial, part = p });
                    }
                }
            }
            scanned = true;
        }

        // Builds the tube's meshes (and the flash's size) from the last scan.
        void Rebuild()
        {
            if (root == null || !scanned) return;

            // The curve of the tube pulls in toward its tips, so a wide part far from the ring needs a longer tube to stay inside it.
            float ringR = Mathf.Max(0.5f, scanRingRadius * (float)CrunchTuning.TubeRadiusFactor);
            float shape = Mathf.Max(1f, (float)CrunchTuning.TubeShapeExponent);   // Below 1 would curve the tube inward, so 1 (a straight cone) is the lowest.
            float fitMax = Mathf.Max(1.1f, (float)CrunchTuning.TubeFitMaxFactor);

            float needForward = 0f;    // Length the tube needs for the craft to fit inside its curve, each side.
            float needBackward = 0f;
            int poking = 0;            // Corners the length limit leaves outside the tube.
            string forwardPart = "", backwardPart = "";   // The parts that set each end's length, for the log.
            float forwardAlong = 0f, forwardRadial = 0f, backwardAlong = 0f, backwardRadial = 0f;
            for (int i = 0; i < scan.Count; i++)
            {
                ScanCorner c = scan[i];
                bool capped = false;
                if (c.along > 0f)
                {
                    float need = RequiredLength(c.along, c.radial, ringR, shape, fitMax, out capped);
                    if (need > needForward) { needForward = need; forwardPart = scanParts[c.part]; forwardAlong = c.along; forwardRadial = c.radial; }
                }
                else if (c.along < 0f)
                {
                    float need = RequiredLength(-c.along, c.radial, ringR, shape, fitMax, out capped);
                    if (need > needBackward) { needBackward = need; backwardPart = scanParts[c.part]; backwardAlong = c.along; backwardRadial = c.radial; }
                }
                if (capped) poking++;
            }

            float maxAlong = scanMaxAlong;
            float minAlong = scanMinAlong;
            float craftLength = maxAlong - minAlong;
            float extra = Mathf.Max(1f, craftLength * (float)CrunchTuning.TubeExtraLengthFraction);   // Beyond each end of the craft. The tube fades out over this stretch, starting exactly at the craft's outermost point.

            radius = ringR;
            float minHalf = Mathf.Max(2f, scanRingRadius * 2f * (float)CrunchTuning.TubeMinHalfLengthRingDiameters);   // Each end is at least this long (a ring diameter), however small the craft, so the whole tube is at least twice that.
            forward = Mathf.Max(minHalf, maxAlong + extra, needForward);
            backward = Mathf.Max(minHalf, -minAlong + extra, needBackward);

            // Both halves get the larger of the two lengths, so the tube looks the same whichever way the craft faces.
            float length = Mathf.Max(forward, backward);
            forward = length;
            backward = length;

            // The tube is fully opaque up to the craft's outermost point and fades from there. Both ends fade from the same distance
            // (the farther of the two craft ends), so the whole craft stays covered whichever way it faces.
            float solid = Mathf.Min(Mathf.Max(maxAlong, -minAlong), length * 0.95f);

            int segments = Mathf.Max(8, CrunchTuning.TubeSegments);
            DestroyMesh(forwardFilter);
            DestroyMesh(backwardFilter);
            forwardFilter.sharedMesh = BuildTube(radius, forward, solid, segments, 1f, shape);
            backwardFilter.sharedMesh = BuildTube(radius, backward, solid, segments, -1f, shape);
            CacheColors(forwardFilter.sharedMesh, out forwardAlpha, out forwardWork);
            CacheColors(backwardFilter.sharedMesh, out backwardAlpha, out backwardWork);

            flashRadius = Mathf.Max(scanBoundRadius, radius);
            built = true;

            lamp.range = Mathf.Max(radius, craftLength) * (float)CrunchTuning.TubeLightRangeFactor;

            Debug.Log(CrunchText.LogTubeMeasured(radius, forward, backward, craftLength));
            if (poking > 0) Debug.Log(CrunchText.LogTubePoking(poking));
            Debug.Log(CrunchText.LogTubeSources(scanRenderers, scanParts.Count, forwardPart, forwardAlong, forwardRadial, backwardPart, backwardAlong, backwardRadial));
        }

        static void CacheColors(Mesh mesh, out float[] alphas, out Color[] work)
        {
            work = mesh.colors;
            alphas = new float[work.Length];
            for (int i = 0; i < work.Length; i++) alphas[i] = work[i].a;
        }

        static void FadeColors(Mesh mesh, float[] alphas, Color[] work, float fade)
        {
            for (int i = 0; i < work.Length; i++) work[i].a = alphas[i] * fade;
            mesh.colors = work;
        }

        static IEnumerable<Renderer> MeshRenderers(Part part)
        {
            List<MeshRenderer> meshes = part.FindModelComponents<MeshRenderer>();
            for (int i = 0; i < meshes.Count; i++) yield return meshes[i];
            List<SkinnedMeshRenderer> skinned = part.FindModelComponents<SkinnedMeshRenderer>();
            for (int i = 0; i < skinned.Count; i++) yield return skinned[i];
        }

        // Hidden meshes (a ring variant that is switched off, say) must not count.
        bool Usable(Renderer r)
        {
            return r != null && r.enabled && r.gameObject.activeInHierarchy && !r.transform.IsChildOf(root.transform);
        }

        // The corners of the mesh's own bounding box in world space. Tighter than the renderer's world-aligned box,
        // which would overstate a rotated mesh.
        static int ObbCorners(Renderer r)
        {
            Bounds b;
            SkinnedMeshRenderer skinned = r as SkinnedMeshRenderer;
            if (skinned != null)
            {
                b = skinned.localBounds;
            }
            else
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return 0;
                b = mf.sharedMesh.bounds;
            }

            Transform t = skinned != null && skinned.rootBone != null ? skinned.rootBone : r.transform;
            Vector3 c = b.center, e = b.extents;
            int k = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        corners[k++] = t.TransformPoint(c + new Vector3(e.x * x, e.y * y, e.z * z));
            return CornerCount;
        }

        // ---- Per-frame ----

        // on is true for as long as the crunch field is up, and only then. Call every frame (LateUpdate suits it best: the flash
        // turns to face the camera, which has finished moving by then); it allocates nothing.
        // The tube grows from the ring to its full length as it fades in, and shrinks back to the ring as it fades out (the whole
        // tube's opacity follows the same 0..1 level as its length). Each time on changes, the flash goes off.
        public void Tick(float dt, bool on)
        {
            if (root == null) return;
            if (!built) on = false;   // Nothing to show until a spool has measured the craft.

            if (on != wasOn)          // The craft has just entered or left the crunch field.
            {
                wasOn = on;
                flashClock = 0f;
            }

            float fadeSeconds = on ? (float)CrunchTuning.TubeFadeInSeconds : (float)CrunchTuning.TubeFadeOutSeconds;
            float step = fadeSeconds > 0f ? dt / fadeSeconds : 1f;
            level = Mathf.MoveTowards(level, on ? 1f : 0f, step);

            float fade = level * level;   // Opacity drops faster than the length, so no bright stub is left as the tube shrinks.
            bool visible = fade > 0.0005f;
            float flash = TickFlash(dt);
            SetShown(visible, flash > 0f);
            if (!visible && flash <= 0f) return;

            Camera cam = ViewCamera();
            Quaternion faceCamera = cam != null ? cam.transform.rotation : root.transform.rotation;

            float shade = 0f;
            if (visible)
            {
                // Flicker: the whole tube and its light waver while the field is up. It dies away with the fade.
                clock += dt;
                float hz = (float)CrunchTuning.TubeFlickerHz;
                float amount = Mathf.Clamp01((float)CrunchTuning.TubeFlickerAmount);
                float noise = Mathf.PerlinNoise(clock * hz, 0.37f) * 0.6f
                            + Mathf.PerlinNoise(clock * hz * 2.3f, 7.1f) * 0.4f;
                float flicker = 1f - amount * (1f - noise);

                // Squash and stretch: the tube's length pulses with the flicker, longest when brightest. It only ever stretches past its
                // measured length, never shrinks below it, so the craft stays covered.
                float lengthScale = Mathf.Max(0.0001f, Mathf.SmoothStep(0f, 1f, level)) * StretchFactor(noise, (float)CrunchTuning.TubeStretchAmount);
                Vector3 scale = new Vector3(1f, lengthScale, 1f);
                forwardTransform.localScale = scale;
                backwardTransform.localScale = scale;

                shade = fade * flicker;
                if (colorProperty != null)
                {
                    material.SetColor(colorProperty, new Color(colorBase, colorBase, colorBase, colorBase * shade));
                }
                else
                {
                    FadeColors(forwardFilter.sharedMesh, forwardAlpha, forwardWork, shade);   // The shader has no colour to fade, so fade the vertices.
                    FadeColors(backwardFilter.sharedMesh, backwardAlpha, backwardWork, shade);
                }
            }

            PlaceFlash(flash, cam, faceCamera);

            lamp.intensity = (float)CrunchTuning.TubeLightIntensity * shade + (float)CrunchTuning.FlashLightIntensity * flash;
        }

        // Advances the flash and returns its brightness, 0..1 (0 when none is running).
        float TickFlash(float dt)
        {
            if (flashClock < 0f) return 0f;
            float rise = Mathf.Max(0f, (float)CrunchTuning.FlashRiseSeconds);
            float fall = Mathf.Max(0.01f, (float)CrunchTuning.FlashFadeSeconds);
            flashClock += dt;
            if (flashClock >= rise + fall) { flashClock = -1f; return 0f; }
            flashAge = flashClock / (rise + fall);
            return FlashEnvelope(flashClock, rise, fall);
        }

        // The flash is a disc that faces the camera, centred on the ring but pushed out toward the camera until it is in front
        // of the whole craft, so no part of the craft cuts into it.
        void PlaceFlash(float flash, Camera cam, Quaternion faceCamera)
        {
            if (flash <= 0f) return;

            Vector3 position = Vector3.zero;
            if (cam != null)
            {
                Vector3 toCamera = root.transform.InverseTransformPoint(cam.transform.position);
                float distance = toCamera.magnitude;
                if (distance > 0.001f)
                {
                    float standoff = Mathf.Min(flashRadius * 1.05f, distance * 0.5f);   // Never farther than half way to the camera.
                    position = toCamera * (standoff / distance);
                }
            }

            float size = 2f * flashRadius * (float)CrunchTuning.FlashSizeFactor * (1f + (float)CrunchTuning.FlashGrowth * flashAge);
            flashGlow.Place(position, faceCamera, size, Mathf.Clamp01(flash * (float)CrunchTuning.FlashStrength));
        }

        void SetShown(bool tubeOn, bool flashOn)
        {
            if (root != null && shown == tubeOn && flashShown == flashOn) return;
            shown = tubeOn;
            flashShown = flashOn;
            if (forwardRenderer != null) forwardRenderer.enabled = tubeOn;
            if (backwardRenderer != null) backwardRenderer.enabled = tubeOn;
            if (flashGlow != null) flashGlow.Visible = flashOn;
            if (lamp != null) lamp.enabled = tubeOn || flashOn;
        }

        // The flight camera, which the flash faces. UNVERIFIED: FlightCamera.fetch.mainCamera is from memory; Camera.main is the fallback.
        static Camera ViewCamera()
        {
            Camera c = FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : null;
            return c != null ? c : Camera.main;
        }

        // ---- Glow quads ----

        // A flat square, textured with the soft round glow, that is turned to face the camera every frame. Its opacity is carried in
        // its vertex colours. The quad is drawn from both sides.
        sealed class Glow
        {
            readonly GameObject go;
            readonly Transform tf;
            readonly MeshRenderer renderer;
            readonly Mesh mesh;
            readonly Color[] colors = new Color[4];
            float lastAlpha = -1f;

            public Glow(string name, Transform parent, Material material, int layer)
            {
                go = new GameObject(name);
                go.layer = layer;
                tf = go.transform;
                tf.SetParent(parent, false);
                MeshFilter filter = go.AddComponent<MeshFilter>();   // No collider is ever added.
                renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                mesh = BuildQuad();
                filter.sharedMesh = mesh;
                renderer.enabled = false;
            }

            public bool Visible { set { renderer.enabled = value; } }

            // localPosition is in the tube's own space; size is the side of the square (the glow's diameter).
            public void Place(Vector3 localPosition, Quaternion worldRotation, float size, float alpha)
            {
                tf.localPosition = localPosition;
                tf.rotation = worldRotation;
                tf.localScale = new Vector3(size, size, 1f);
                if (Mathf.Approximately(alpha, lastAlpha)) return;
                lastAlpha = alpha;
                Color c = new Color(1f, 1f, 1f, alpha);
                colors[0] = c; colors[1] = c; colors[2] = c; colors[3] = c;
                mesh.colors = colors;
            }

            public void Dispose()
            {
                if (mesh != null) Object.Destroy(mesh);
                if (go != null) Object.Destroy(go);
            }

            static Mesh BuildQuad()
            {
                Mesh m = new Mesh();
                m.name = "CrunchDriveGlow";
                m.vertices = new Vector3[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                };
                m.uv = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                Color clear = new Color(1f, 1f, 1f, 0f);
                m.colors = new Color[] { clear, clear, clear, clear };
                m.triangles = new int[] { 0, 2, 1, 1, 2, 3, 0, 1, 2, 1, 3, 2 };   // Both sides.
                m.RecalculateBounds();
                return m;
            }
        }

        // The soft round glow texture, for the flash: white, with the opacity in the alpha channel. Built once.
        static Texture2D GlowTexture()
        {
            if (glowTexture != null) return glowTexture;

            const int size = 64;
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    byte a = (byte)Mathf.RoundToInt(255f * GlowAlpha(Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            t.SetPixels32(pixels);
            t.Apply();
            glowTexture = t;
            return t;
        }

        // ---- Mesh ----

        // Opacity along the tube: fully opaque from the ring to solid, then easing to nothing at the far end.
        public static float EndAlpha(float distance, float solid, float length)
        {
            if (distance <= solid) return 1f;
            if (distance >= length) return 0f;
            float t = (distance - solid) / (length - solid);
            return 1f - Mathf.SmoothStep(0f, 1f, t);
        }

        // Opacity of the glow texture at radius r from its centre (0 = centre, 1 = edge): a small bright core, then a smooth fade to nothing at the edge.
        public static float GlowAlpha(float r)
        {
            if (r >= 1f) return 0f;
            float s = Mathf.Clamp01((r - 0.15f) / 0.85f);
            return 1f - Mathf.SmoothStep(0f, 1f, s);
        }

        // Brightness of the flash, 0..1, t seconds after it begins: it rises over 'rise' seconds, then dies away over 'fall' seconds
        // (fast at first, with a long soft tail).
        public static float FlashEnvelope(float t, float rise, float fall)
        {
            if (t <= 0f) return 0f;
            if (t < rise) return Mathf.SmoothStep(0f, 1f, t / rise);
            float s = Mathf.Clamp01((t - rise) / Mathf.Max(0.0001f, fall));
            return (1f - s) * (1f - s);
        }

        // How much longer than its measured length the tube is, given the flicker's noise (0..1): 1 at the lowest, 1 + amount at the
        // highest. Never below 1, so the tube never shrinks inside the craft's bounds.
        public static float StretchFactor(float noise, float amount)
        {
            return 1f + Mathf.Max(0f, amount) * Mathf.Clamp01(noise);
        }

        // Opacity of soft layer s (1 = innermost, up to 'shells' = outermost) of the halo around the tube. Seen from the side, the
        // glow just outside the tube's edge has opacity edgeOpacity and falls off like a bell curve (width 'falloff', as a share of the halo's
        // width) to nothing at the outermost layer: each layer adds the difference between the curve at its inner and outer edge. A
        // layer is crossed twice by a line of sight (front and back wall), so its own opacity is lower than the opacity it adds.
        public static float HaloShellAlpha(int s, int shells, float edgeOpacity, float falloff)
        {
            if (shells < 1 || s < 1 || s > shells) return 0f;
            float k = Mathf.Max(0.05f, falloff);
            float end = Bell(1f, k);
            float inner = (Bell((float)(s - 1) / shells, k) - end) / (1f - end);
            float outer = (Bell((float)s / shells, k) - end) / (1f - end);
            float added = Mathf.Clamp01(edgeOpacity) * Mathf.Max(0f, inner - outer);
            return 1f - Mathf.Sqrt(1f - Mathf.Min(added, 0.999f));
        }

        static float Bell(float d, float k)
        {
            return Mathf.Exp(-(d * d) / (k * k));
        }

        // How long a tube of this shape must be for a point 'along' the axis and 'radial' from it to be inside it.
        // The result is never more than maxFactor x 'along': a part much wider than the ring would otherwise ask for a tube
        // many times the craft's length, so it is left poking out of the tube instead (capped is true then).
        public static float RequiredLength(float along, float radial, float ringRadius, float shape, float maxFactor, out bool capped, float margin = 1.05f)
        {
            capped = false;
            if (along <= 0f) return 0f;
            float ratio = Mathf.Min(radial / ringRadius, 0.98f);
            float fit = Mathf.Pow(1f - Mathf.Pow(ratio, shape), 1f / shape);   // Share of the length at which the curve is as wide as the point.
            float length = along / fit * margin;
            if (length > along * maxFactor) { capped = true; return along * maxFactor; }
            return length;
        }

        // The tube's radius at u (0 at the ring, 1 at the tip) as a share of the ring's radius: widest at the ring, closing to a point.
        public static float RadiusScale(float u, float shape)
        {
            u = Mathf.Clamp01(u);
            return Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(u, shape)), 1f / shape);
        }

        // One half of the tube, from the ring (y = 0) out to y = direction * length, drawn from both sides. The opacity is
        // carried in the vertex colours, so it fades out toward the tip. Wider, fainter copies of the tube (the halo)
        // blur its outline.
        static Mesh BuildTube(float radius, float length, float solid, int segments, float direction, float shape)
        {
            int count = Mathf.Max(4, CrunchTuning.TubeRings);
            List<float> us = new List<float>();
            for (int i = 0; i <= count; i++)
            {
                float t = (float)i / count;
                us.Add(1f - (1f - t) * (1f - t));   // Rings are packed closer toward the tip, where the curve is steepest.
            }
            float solidU = Mathf.Clamp01(solid / length);
            us.Add(solidU);                          // A ring exactly where the opaque part ends.
            us.Sort();
            for (int i = us.Count - 1; i > 0; i--) if (us[i] - us[i - 1] < 0.0005f) us.RemoveAt(i);

            int ringVerts = segments + 1;
            int shells = Mathf.Max(0, CrunchTuning.TubeHaloShells);
            shells = Mathf.Min(shells, Mathf.Max(0, MaxMeshVertices / (us.Count * ringVerts) - 1));   // Keep the mesh within its vertex limit.
            float spread = (float)CrunchTuning.TubeHaloSpread;
            float edgeOpacity = Mathf.Clamp01((float)CrunchTuning.TubeHaloStrength);
            float falloff = (float)CrunchTuning.TubeHaloFalloff;

            List<Vector3> verts = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int s = 0; s <= shells; s++)
            {
                float widen = 1f + spread * s / Mathf.Max(1, shells);
                float fade = s == 0 ? 1f : HaloShellAlpha(s, shells, edgeOpacity, falloff);
                int first = verts.Count;

                for (int r = 0; r < us.Count; r++)
                {
                    float y = length * us[r];
                    float rad = radius * widen * RadiusScale(us[r], shape);
                    Color c = new Color(1f, 1f, 1f, EndAlpha(y, solid, length) * fade);
                    for (int i = 0; i < ringVerts; i++)
                    {
                        float a = (float)i / segments * Mathf.PI * 2f;
                        verts.Add(new Vector3(Mathf.Cos(a) * rad, y * direction, Mathf.Sin(a) * rad));
                        colors.Add(c);
                        uvs.Add(new Vector2((float)i / segments, 0.5f));
                    }
                }

                for (int r = 0; r < us.Count - 1; r++)
                {
                    for (int i = 0; i < segments; i++)
                    {
                        int a = first + r * ringVerts + i, b = a + 1, c = a + ringVerts, d = c + 1;
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(b); tris.Add(c); tris.Add(d);
                        tris.Add(a); tris.Add(b); tris.Add(c);   // The same faces again, facing the other way.
                        tris.Add(b); tris.Add(d); tris.Add(c);
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "CrunchDriveTube";
            mesh.vertices = verts.ToArray();
            mesh.colors = colors.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}