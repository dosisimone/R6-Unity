using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using dousi96.Geometry;
using ClipperLib;

public struct WallClipperJob : IJob
{
    private const long precision = 1000;

    public MultiPolygonData subjects;
    public MultiPolygonData clips;
    public MultiPolygonData output;

    public float2 MinWallAnglePoint;
    public float2 MaxWallAnglePoint;

    public void Execute()
    {
        List<List<IntPoint>> subjsList = new List<List<IntPoint>>();
        for (int pi = 0; pi < subjects.PolygonsCount; ++pi)
        {
            List<IntPoint> contour = new List<IntPoint>();
            foreach (var contourPoint in subjects.GetContourPoints(pi))
            {
                contour.Add(Vector2ToIntPoint(contourPoint.Point));
            }
            subjsList.Add(contour);

            for (int hi = 0; hi < subjects.GetPolygonHolesNum(pi); ++hi)
            {
                List<IntPoint> holeList = new List<IntPoint>();
                var hole = subjects.GetPolygonHole(pi, hi);
                foreach (var holePoint in hole)
                {
                    holeList.Add(Vector2ToIntPoint(holePoint.Point));
                }
                subjsList.Add(holeList);
            }
        }

        List<List<IntPoint>> clipsList = new List<List<IntPoint>>();
        for (int pi = 0; pi < clips.PolygonsCount; ++pi)
        {
            List<IntPoint> contour = new List<IntPoint>();
            foreach (var contourPoint in clips.GetContourPoints(pi))
            {
                contour.Add(Vector2ToIntPoint(contourPoint.Point));
            }
            clipsList.Add(contour);
        }

        //Run Clipper
        Clipper clipper = new Clipper();
        clipper.AddPaths(subjsList, PolyType.ptSubject, true);
        clipper.AddPaths(clipsList, PolyType.ptClip, true);
        PolyTree solution = new PolyTree();
        clipper.Execute(ClipType.ctDifference, solution, PolyFillType.pftPositive);

        //Save the results
        output.ClearPolygons();
        foreach (PolyNode node in solution.Childs)
        {
            bool polygonOk = false;

            Vector2[] contour = new Vector2[node.Contour.Count];
            for (int i = node.Contour.Count - 1; i >= 0; --i)
            {
                contour[i] = IntPointToVector2(node.Contour[i]);

                polygonOk |= (math.abs(contour[i].x - MinWallAnglePoint.x) <= float.Epsilon)
                            || (math.abs(contour[i].x - MaxWallAnglePoint.x) <= float.Epsilon)
                            || (math.abs(contour[i].y - MinWallAnglePoint.y) <= float.Epsilon)
                            || (math.abs(contour[i].y - MaxWallAnglePoint.y) <= float.Epsilon);                
            }

            Vector2[][] holes = new Vector2[node.ChildCount][];
            int holeIndex = 0;
            foreach (PolyNode holeNode in node.Childs)
            {
                holes[holeIndex] = new Vector2[holeNode.Contour.Count];
                for (int j = 0; j < holeNode.Contour.Count; ++j)
                {
                    holes[holeIndex][j] = IntPointToVector2(holeNode.Contour[j]);
                }
                ++holeIndex;
            }

            if (polygonOk)
            {
                output.AddPolygon(contour, holes);
            }
        }
    }

    private static IntPoint Vector2ToIntPoint(float2 v)
    {
        return new IntPoint
        {
            X = (long)(v.x * precision),
            Y = (long)(v.y * precision)
        };
    }

    private static float2 IntPointToVector2(IntPoint p)
    {
        return new float2
        {
            x = (float)p.X / precision,
            y = (float)p.Y / precision
        };
    }
}
