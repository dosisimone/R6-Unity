using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace dousi96.Geometry.Triangulator
{
    [BurstCompile]
    public struct PrepareTrianglesListJob : IJob
    {
        [ReadOnly]
        public MultiPolygonJobData Polygons;
        public NativeList<int> OutTriangles;

        public void Execute()
        {
            int ntris = 0;
            for (int pi = 0; pi < Polygons.PolygonsCount; ++pi)
            {
                ntris += Polygons.GetPolygonHolesNum(pi) * 2 + Polygons.GetPolygonNumVertices(pi);
            }
            ntris -= 2 * Polygons.PolygonsCount;
            ntris *= 3;
            OutTriangles.ResizeUninitialized(ntris);
        }
    }
}
