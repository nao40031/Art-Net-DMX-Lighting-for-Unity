using System;
using System.Linq;
using UnityEngine;

namespace Mewlist.MewNoiseGen
{
    [Serializable]
    public class Shape
    {
        public enum ShapeType
        {
            Box,
            Sphere,
        }

        public ShapeType Type;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Size;
        [Range(0.01f, 1f)]
        public float FadeDistance = 0.01f;
        [Range(0f, 1f)]
        public float Fade = 0f;

        public virtual float Density(Vector3 position)
        {
            return 1f;
        }

        public virtual Bounds Bounds()
        {
            return new Bounds(Vector3.zero, Vector3.one * 256f);
        }

        public Shape Factory()
        {
            switch (Type)
            {
                case ShapeType.Box: return new Box(this);
                case ShapeType.Sphere: return new Sphere(this);
                default:
                    return this;
            }
        }

        public static Shape Box(float fade, float dist, Vector3 pos, Vector3 size)
        {
            return new Shape()
            {
                Fade = fade,
                FadeDistance = dist,
                Position = pos,
                Rotation = Vector3.zero,
                Size = size,
                Type = ShapeType.Box
            };
        }

        public static Shape Sphere(float fade, float dist, Vector3 pos, float size)
        {
            return new Shape()
            {
                Fade = fade,
                FadeDistance = dist,
                Position = pos,
                Rotation = Vector3.zero,
                Size = Vector3.one * size,
                Type = ShapeType.Sphere
            };
        }
    }

    public class Box : Shape
    {
        private Quaternion quaternion;
        public Box(Shape origin)
        {
            Position = origin.Position;
            Rotation = origin.Rotation;
            Size     = origin.Size;
            Fade     = origin.Fade;
            FadeDistance = origin.FadeDistance;
            quaternion = Quaternion.Euler(Rotation);
        }

        public override float Density(Vector3 position)
        {
            var localPos = Quaternion.Inverse(quaternion) * (position - Position);
            var localBounds = LocalBounds;
            if (!localBounds.Contains(localPos)) return 0f;

            var p = Planes;
            var dist = p.Aggregate(float.MaxValue,
                (a, x) =>
                {
                    var d = x.GetDistanceToPoint(localPos);
                    return Mathf.Min(a, Mathf.Abs(d));
                });

            var t = Mathf.Pow(Mathf.Clamp(dist / FadeDistance, 0f, 1f),  2);
            return Mathf.Pow(t, Fade);
        }

        public override Bounds Bounds()
        {
            var l = Points;
            var tl = l.Select(x => quaternion * x).ToArray();
            var min = tl.Aggregate(Vector3.Min);
            var max = tl.Aggregate(Vector3.Max);
            return new Bounds(Position, max - min);
        }

        private Vector3[] points = null;
        private Vector3[] Points
        {
            get
            {
                if (points != null) return points;
                var bounds = new Bounds(Vector3.zero, Size);
                var min = bounds.min;
                var max = bounds.max;
                var a = new Vector3(min.x, min.y, min.z);
                var b = new Vector3(min.x, min.y, max.z);
                var c = new Vector3(min.x, max.y, min.z);
                var d = new Vector3(min.x, max.y, max.z);
                var e = new Vector3(max.x, min.y, min.z);
                var f = new Vector3(max.x, min.y, max.z);
                var g = new Vector3(max.x, max.y, min.z);
                var h = new Vector3(max.x, max.y, max.z);
                points = new [] {a, b, c, d, e, f, g, h};
                return points;
            }
        }

        private Plane[] planes = null;
        private Plane[] Planes
        {
            get
            {
                if (planes == null)
                {
                    var p = Points;
                    var a = new Plane(p[1] - p[0], p[0]);
                    var b = new Plane(p[2] - p[0], p[0]);
                    var c = new Plane(p[4] - p[0], p[0]);
                    var d = new Plane(p[3] - p[7], p[7]);
                    var e = new Plane(p[5] - p[7], p[7]);
                    var f = new Plane(p[6] - p[7], p[7]);
                    planes = new [] {a, b, c, d, e, f};
                }
                return planes;
            }
        }

        private Bounds LocalBounds
        {
            get
            {
                return new Bounds(Vector3.zero, Size);
            }
        }
    }

    
    public class Sphere : Shape
    {
        public Sphere(Shape origin)
        {
            Position = origin.Position;
            Rotation = origin.Rotation;
            Size     = origin.Size;
            Fade     = origin.Fade;
            FadeDistance = origin.FadeDistance;
        }

        public override float Density(Vector3 position)
        {
            var localPos = position - Position;
            var dist = Size.x / 2f - localPos.magnitude;
            if (dist < 0) return 0f;

            var t = Mathf.Pow(Mathf.Clamp(dist / FadeDistance, 0f, 1f),  2);
            return Mathf.Pow(t, Fade);
        }

        public override Bounds Bounds()
        {
            return new Bounds(Position, Vector3.one * Size.x);
        }
    }
}