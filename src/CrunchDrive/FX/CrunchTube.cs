using System.Collections.Generic;
using UnityEngine;

namespace CrunchDrive
{
    // The blinding white tube around the craft while a crunch field is up: a spindle along the ring's axis, widest at the
    // ring and closing to a point past each end of the craft, with soft layers around it that blur its edge. It is solid
    // white over the whole craft and fades out toward the
    // tips instead of cutting off. It grows from the ring to its full length as it fades in, and shrinks back as it fades
    // out. The flash, the tube and its light are one effect: all on, or all off.
    //
    // It is parented to the drive part, so it follows the part's position, rotation and the floating origin for free.
    // Its size is measured from the craft's meshes at the start of each spool. Everything is built in code.
    //
    // Assumes the ring's axis is the part's local Y axis and the part's origin is the centre of the ring along it.
    public class CrunchTube
    {
        const int CornerCount = 8;

        GameObject root;
        MeshFilter forwardFilter;
        MeshFilter backwardFilter;
        Renderer forwardRenderer;
        Renderer backwardRenderer;
        Transform forwardTransform;
        Transform backwardTransform;
        Light lamp;
        Material material;
        string colorProperty;
        float colorBase = 1f;

        float radius = 1f;       // Tube radius, meters.
        float forward = 2f;      // Tube length on the +Y side of the ring, meters.
        float backward = 2f;     // Tube length on the -Y side of the ring, meters.
        float level;             // 0..1: how far the tube has faded in and grown out.
        float clock;             // Seconds, for the flicker.
        float[] forwardAlpha, backwardAlpha;      // The meshes' own opacities, kept so they can be faded by hand when the shader has no colour property.
        Color[] forwardWork, backwardWork;
        bool shown;
        bool built;              // The meshes have been built from a measurement.

        public bool IsBuilt { get { return built; } }   // A measurement has been made, so the tube can be shown.

        static Shader shader;
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

            root = new GameObject("CrunchDriveTube");
            root.layer = part.gameObject.layer;
            root.transform.SetParent(part.transform, false);

            forwardRenderer = MakeHalf("Forward", out forwardFilter);
            backwardRenderer = MakeHalf("Backward", out backwardFilter);
            forwardTransform = forwardFilter.transform;
            backwardTransform = backwardFilter.transform;

            lamp = root.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = Color.white;
            lamp.shadows = LightShadows.None;

            shown = true;   // Forces the first SetShown to actually switch everything off.
            built = false;
            level = 0f;
            SetShown(false);
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
            if (root != null) Object.Destroy(root);
            if (material != null) Object.Destroy(material);
            root = null;
            material = null;
            forwardFilter = null;
            backwardFilter = null;
            forwardTransform = null;
            backwardTransform = null;
            shown = false;
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

            Transform pt = drive.transform;
            Vector3 center = pt.position;
            Vector3 axis = pt.up;

            float ringRadius = 0f;
            float maxAlong = 0f;
            float minAlong = 0f;
            float needForward = 0f;    // Length the tube needs for the craft to fit inside its curve, each side.
            float needBackward = 0f;
            int poking = 0;            // Corners the length limit leaves outside the tube.
            int rendererCount = 0;     // Mesh renderers that counted towards the craft's size.
            string forwardPart = "", backwardPart = "";   // The parts that set each end's length, for the log.
            float forwardAlong = 0f, forwardRadial = 0f, backwardAlong = 0f, backwardRadial = 0f;

            // The ring: measured in the part's own space, where the ring's axis is Y.
            foreach (Renderer r in MeshRenderers(drive))
            {
                if (!Usable(r)) continue;
                int n = ObbCorners(r);
                for (int i = 0; i < n; i++)
                {
                    Vector3 local = pt.InverseTransformPoint(corners[i]);
                    ringRadius = Mathf.Max(ringRadius, Mathf.Abs(local.x), Mathf.Abs(local.z));
                }
            }

            // The craft: every part, projected onto the ring's axis. The curve of the tube pulls in toward its tips, so a
            // wide part far from the ring needs a longer tube to stay inside it (the ring's radius is known by now).
            float ringR = Mathf.Max(0.5f, ringRadius * (float)CrunchTuning.TubeRadiusFactor);
            float shape = Mathf.Max(1f, (float)CrunchTuning.TubeShapeExponent);   // Below 1 would curve the tube inward, so 1 (a straight cone) is the lowest.
            float fitMax = Mathf.Max(1.1f, (float)CrunchTuning.TubeFitMaxFactor);
            for (int p = 0; p < vessel.parts.Count; p++)
            {
                foreach (Renderer r in MeshRenderers(vessel.parts[p]))
                {
                    if (!Usable(r)) continue;
                    rendererCount++;
                    int n = ObbCorners(r);
                    for (int i = 0; i < n; i++)
                    {
                        float along = Vector3.Dot(corners[i] - center, axis);
                        if (along > maxAlong) maxAlong = along;
                        if (along < minAlong) minAlong = along;
                        Vector3 offset = corners[i] - center;
                        float radial = Mathf.Sqrt(Mathf.Max(0f, offset.sqrMagnitude - along * along));
                        bool capped;
                        if (along > 0f)
                        {
                            float need = RequiredLength(along, radial, ringR, shape, fitMax, out capped);
                            if (need > needForward) { needForward = need; forwardPart = vessel.parts[p].name; forwardAlong = along; forwardRadial = radial; }
                        }
                        else if (along < 0f)
                        {
                            float need = RequiredLength(-along, radial, ringR, shape, fitMax, out capped);
                            if (need > needBackward) { needBackward = need; backwardPart = vessel.parts[p].name; backwardAlong = along; backwardRadial = radial; }
                        }
                        else capped = false;
                        if (capped) poking++;
                    }
                }
            }

            float craftLength = maxAlong - minAlong;
            float extra = Mathf.Max(1f, craftLength * (float)CrunchTuning.TubeExtraLengthFraction);   // Beyond each end of the craft. This is where the tube fades out.
            float pad = extra * 0.1f;                                                                  // The tube stays fully opaque a little past the craft's ends.

            radius = ringR;
            forward = Mathf.Max(2f, maxAlong + extra, needForward);
            backward = Mathf.Max(2f, -minAlong + extra, needBackward);

            // Both halves get the larger of the two lengths, so the tube looks the same whichever way the craft faces.
            float length = Mathf.Max(forward, backward);
            forward = length;
            backward = length;

            int segments = Mathf.Max(8, CrunchTuning.TubeSegments);
            DestroyMesh(forwardFilter);
            DestroyMesh(backwardFilter);
            forwardFilter.sharedMesh = BuildTube(radius, forward, Mathf.Min(maxAlong + pad, forward * 0.95f), segments, 1f, shape);
            backwardFilter.sharedMesh = BuildTube(radius, backward, Mathf.Min(-minAlong + pad, backward * 0.95f), segments, -1f, shape);
            CacheColors(forwardFilter.sharedMesh, out forwardAlpha, out forwardWork);
            CacheColors(backwardFilter.sharedMesh, out backwardAlpha, out backwardWork);
            built = true;

            lamp.range = Mathf.Max(radius, craftLength) * (float)CrunchTuning.TubeLightRangeFactor;
            Debug.Log(CrunchText.LogTubeMeasured(radius, forward, backward, craftLength));
            if (poking > 0) Debug.Log(CrunchText.LogTubePoking(poking));
            Debug.Log(CrunchText.LogTubeSources(rendererCount, vessel.parts.Count, forwardPart, forwardAlong, forwardRadial, backwardPart, backwardAlong, backwardRadial));
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

        // on is true for as long as the crunch field is up, and only then. Call every frame; it allocates nothing.
        // The tube grows from the ring to its full length as it fades in, and shrinks back to the ring as it fades out (the whole
        // tube's opacity follows the same 0..1 level as its length).
        public void Tick(float dt, bool on)
        {
            if (root == null) return;

            if (!built) on = false;   // Nothing to show until a spool has measured the craft.

            float fadeSeconds = on ? (float)CrunchTuning.TubeFadeInSeconds : (float)CrunchTuning.TubeFadeOutSeconds;
            float step = fadeSeconds > 0f ? dt / fadeSeconds : 1f;
            level = Mathf.MoveTowards(level, on ? 1f : 0f, step);

            float fade = level * level;   // Opacity drops faster than the length, so no bright stub is left as the tube shrinks.
            bool visible = fade > 0.0005f;
            SetShown(visible);
            if (!visible) return;

            // Flicker: the whole tube and its light waver while the field is up. It dies away with the fade.
            clock += dt;
            float amount = Mathf.Clamp01((float)CrunchTuning.TubeFlickerAmount);
            float noise = Mathf.PerlinNoise(clock * (float)CrunchTuning.TubeFlickerHz, 0.37f) * 0.6f
                        + Mathf.PerlinNoise(clock * (float)CrunchTuning.TubeFlickerHz * 2.3f, 7.1f) * 0.4f;
            float flicker = 1f - amount * (1f - noise);

            float length = Mathf.Max(0.0001f, Mathf.SmoothStep(0f, 1f, level));
            Vector3 scale = new Vector3(1f, length, 1f);
            forwardTransform.localScale = scale;
            backwardTransform.localScale = scale;

            float shade = fade * flicker;
            if (colorProperty != null)
            {
                material.SetColor(colorProperty, new Color(colorBase, colorBase, colorBase, colorBase * shade));
            }
            else
            {
                FadeColors(forwardFilter.sharedMesh, forwardAlpha, forwardWork, shade);   // The shader has no colour to fade, so fade the vertices.
                FadeColors(backwardFilter.sharedMesh, backwardAlpha, backwardWork, shade);
            }

            lamp.intensity = (float)CrunchTuning.TubeLightIntensity * shade;
        }

        void SetShown(bool on)
        {
            if (shown == on && root != null) return;
            shown = on;
            if (forwardRenderer != null) forwardRenderer.enabled = on;
            if (backwardRenderer != null) backwardRenderer.enabled = on;
            if (lamp != null) lamp.enabled = on;
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

            int shells = Mathf.Max(0, CrunchTuning.TubeHaloShells);
            float spread = (float)CrunchTuning.TubeHaloSpread;
            float strength = Mathf.Clamp01((float)CrunchTuning.TubeHaloStrength);
            int ringVerts = segments + 1;

            List<Vector3> verts = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int s = 0; s <= shells; s++)
            {
                float widen = 1f + spread * s / Mathf.Max(1, shells);
                float inner = 1f - (float)s / (shells + 1);
                float fade = s == 0 ? 1f : strength * inner * inner;
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