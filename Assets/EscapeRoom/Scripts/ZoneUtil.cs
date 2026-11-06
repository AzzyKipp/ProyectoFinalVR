using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Utilidades para comprobar si un objeto está dentro de una zona (caja orientada),
    /// sin depender de eventos de trigger de física (más robusto con objetos kinemáticos).
    /// </summary>
    public static class ZoneUtil
    {
        /// <summary>¿El punto está dentro de la caja orientada definida por zone (escala ignorada) y size?</summary>
        public static bool Contains(Transform zone, Vector3 size, Vector3 worldPoint)
        {
            Vector3 local = Quaternion.Inverse(zone.rotation) * (worldPoint - zone.position);
            return Mathf.Abs(local.x) <= size.x * 0.5f
                && Mathf.Abs(local.y) <= size.y * 0.5f
                && Mathf.Abs(local.z) <= size.z * 0.5f;
        }

        /// <summary>¿Los bounds tocan la zona? (centro o punto más cercano dentro de la zona)</summary>
        public static bool Touches(Transform zone, Vector3 size, Bounds bounds)
        {
            if (Contains(zone, size, bounds.center)) return true;
            return Contains(zone, size, bounds.ClosestPoint(zone.position));
        }

        /// <summary>Bounds combinados de los renderers (o colliders si no hay renderers) de un objeto.</summary>
        public static Bounds GetBounds(GameObject go)
        {
            bool has = false;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            if (has) return b;
            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                if (!has) { b = c.bounds; has = true; }
                else b.Encapsulate(c.bounds);
            }
            return b;
        }

        public static Vector3 FlatForward(Transform t)
        {
            Vector3 f = t.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f)
            {
                f = -t.up;
                f.y = 0f;
            }
            return f.sqrMagnitude < 1e-4f ? Vector3.forward : f.normalized;
        }

        public static void DrawZoneGizmo(Transform zone, Vector3 size, Color color)
        {
            if (zone == null) return;
            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.TRS(zone.position, zone.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
