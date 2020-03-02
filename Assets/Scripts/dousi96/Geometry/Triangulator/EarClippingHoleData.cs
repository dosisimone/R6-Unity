using System;
using Unity.Burst;

namespace dousi96.Geometry.Triangulator
{
    [BurstCompile]
    internal struct EarClippingHoleData : IComparable<EarClippingHoleData>
    {
        public readonly int HoleIndex;
        public readonly int IndexMaxX;
        private readonly float MaxX;

        public EarClippingHoleData(PolygonJobData polygon, int holeIndex, int indexMaxX)
        {
            HoleIndex = holeIndex;
            IndexMaxX = indexMaxX;
            MaxX = polygon.GetHolePoint(holeIndex, indexMaxX).x;
        }

        public EarClippingHoleData(int holeIndex, int indexMaxX, float maxX)
        {
            HoleIndex = holeIndex;
            IndexMaxX = indexMaxX;
            MaxX = maxX;
        }

        public int CompareTo(EarClippingHoleData other)
        {
            if (MaxX > other.MaxX)
            {
                return -1;
            }
            else if (MaxX < other.MaxX)
            {
                return 1;
            }
            return 0;
        }
    }
}
