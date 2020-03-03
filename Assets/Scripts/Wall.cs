using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using dousi96.Geometry;
using dousi96.Geometry.Triangulator;
using dousi96.Geometry.Extruder;


[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class Wall : MonoBehaviour
{
    [SerializeField] 
    private Vector3 size = Vector3.one;

    MeshFilter meshFilter;
    //BoxCollider boxCollider;
    MeshCollider meshCollider;

    private List<Vector2[]> clips;
    private MultiPolygonData subjects;
    private MultiPolygonData jobClips;
    private MultiPolygonData clippingOutput;
    private NativeList<Vector3> OutVertices;
    private NativeList<int> OutTriangles;
    private JobHandle lastJobHandle;
    private Coroutine waitForJobsCoroutine;

    private void OnEnable()
    {
        Vector3 halfSize = size / 2f;

        meshFilter = GetComponent<MeshFilter>();
        meshFilter.sharedMesh = new Mesh();

        meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.sharedMesh;

        clips = new List<Vector2[]>();
        
        Vector2[] contourn = {
            new Vector2(-halfSize.x, 0f),
            new Vector2(+halfSize.x, 0f),
            new Vector2(+halfSize.x, +size.y),
            new Vector2(-halfSize.x, +size.y)
        };

        subjects = new MultiPolygonData(Allocator.Persistent);
        subjects.AddPolygon(contourn);

        ScheduleJobs();
    }

    private void LateUpdate()
    {
        if (clips.Count > 0 && waitForJobsCoroutine == null)
        {
            ScheduleJobs();
        }
    }

    private void OnDisable()
    {
        //avoid memory leaks
        if (jobClips.IsCreated)
        {
            jobClips.Dispose();
        }        
        if (OutVertices.IsCreated) 
        {
            OutVertices.Dispose();
        }
        if (OutTriangles.IsCreated) 
        {
            OutTriangles.Dispose();
        }
        if (clippingOutput.IsCreated)
        {
            clippingOutput.Dispose();
        }
        if (subjects.IsCreated)
        {
            subjects.Dispose();
        }
    }

    public void AddBulletHole(Ray rayOnWall, float destructionLvWithDropoff)
    {
        Vector3 planeNormal = transform.forward;
        Vector3 planeCenter = transform.position;
        Vector3 diff = planeCenter - rayOnWall.origin;
        float denominator = Vector3.Dot(rayOnWall.direction, planeNormal);
        float t = Vector3.Dot(diff, planeNormal) / denominator;
        Vector3 hitpoint3 = rayOnWall.origin + rayOnWall.direction * t;

        int nPoints = (int)(destructionLvWithDropoff * 50f);
        if (nPoints < 3)
        {
            return;
        }

        Vector2[] clip = new Vector2[nPoints];
        float radiansInterval = Mathf.PI * 2 / nPoints;
        float randomStartRadians = UnityEngine.Random.Range(0f, Mathf.PI);

        Vector2 hitpointLocal2 = transform.InverseTransformPoint(hitpoint3);
        for (int i = 0; i < nPoints; ++i)
        {
            clip[i] = hitpointLocal2 + GetUnitOnCircle(randomStartRadians + radiansInterval * i, destructionLvWithDropoff);
        }
        clips.Add(clip);
    }

    private void ScheduleJobs()
    {
        Vector2 halfSize = size / 2f;

        jobClips = new MultiPolygonData(Allocator.TempJob);
        OutVertices = new NativeList<Vector3>(Allocator.TempJob);
        OutTriangles = new NativeList<int>(Allocator.TempJob);
        clippingOutput = new MultiPolygonData(Allocator.Persistent);

        foreach (Vector2[] clip in clips)
        {
            jobClips.AddPolygon(clip);
        }
        clips.Clear();

        WallClipperJob clipperJob = new WallClipperJob()
        {
            subjects = subjects,
            clips = jobClips,
            output = clippingOutput,
            MinWallAnglePoint = new float2(-halfSize.x, 0f),
            MaxWallAnglePoint = new float2(+halfSize.x, +size.y)
        };

        PrepareTrianglesListJob prepareTrianglesListJob = new PrepareTrianglesListJob()
        {
            Polygons = clippingOutput,
            OutTriangles = OutTriangles
        };

        ECTriangulatorParallelJob triangulatorParallelJob = new ECTriangulatorParallelJob()
        {
            Polygons = clippingOutput,
            OutTriangles = OutTriangles.AsDeferredJobArray()
        };

        ExtruderJob extruderJob = new ExtruderJob()
        {
            Polygons = clippingOutput,
            ExtrudeDirection = new Vector3(0f, 0f, size.z),
            OutTriangles = OutTriangles,
            OutVertices = OutVertices,
        };

        JobHandle clippingHandler = clipperJob.Schedule();
        JobHandle prepareTrianglesListHandler = prepareTrianglesListJob.Schedule(clippingHandler);
        JobHandle triangulatorHandler = triangulatorParallelJob.Schedule(clippingOutput.PolygonSupportList, 1, prepareTrianglesListHandler);
        lastJobHandle = extruderJob.Schedule(triangulatorHandler);

        waitForJobsCoroutine = StartCoroutine(WaitForJobsCompleted());
    }

    private IEnumerator WaitForJobsCompleted()
    {
        while (!lastJobHandle.IsCompleted)
        {
            yield return new WaitForEndOfFrame();
        }
        lastJobHandle.Complete();

        meshFilter.sharedMesh.Clear();
        meshFilter.sharedMesh.vertices = OutVertices.ToArray();
        meshFilter.sharedMesh.triangles = OutTriangles.ToArray();
        meshFilter.sharedMesh.RecalculateNormals();
        meshFilter.sharedMesh.RecalculateTangents();
        meshFilter.sharedMesh.RecalculateBounds();

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = meshFilter.sharedMesh;
        meshCollider.convex = false;

        MultiPolygonData temp = subjects;
        subjects = clippingOutput;
        clippingOutput = temp;

        jobClips.Dispose();
        OutVertices.Dispose();
        OutTriangles.Dispose();
        clippingOutput.Dispose();

        waitForJobsCoroutine = null;
    }

    private Vector2 GetUnitOnCircle(float radians, float radius)
    {
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.grey;
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.matrix = rotationMatrix;
        Gizmos.DrawCube(new Vector3(0.0f, size.y / 2f, 0f), size);
    }
#endif
}
