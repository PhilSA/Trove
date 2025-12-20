using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
[CustomEditor(typeof(BVHDebuggerBehaviour))]
public class BVHDebuggerBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        BVHDebuggerBehaviour authoring = (target as BVHDebuggerBehaviour);
        if (GUILayout.Button("+ Bounding Box Level"))
        {
            authoring.BoundingBoxDebugLevel++;
        }
        if (GUILayout.Button("- Bounding Box Level"))
        {
            authoring.BoundingBoxDebugLevel--;
        }
    }
}
#endif

public struct BVHDebugger : IComponentData
{
    public bool QueryEnabled;
    public float3 QueryPosition;
    public float3 QueryDirection;
    public float QueryLength;
    public float3 QueryExtents;
    
    public bool DebugMortonCurve;
    public int MortonCurveDebugIterations;
    
    public bool DebugBoundingBoxes;
    public int BoundingBoxDebugLevel;
    
    public bool DebugNearestNeighbours;
    public int NearestNeighboursDebugLevel;
}

class BVHDebuggerBehaviour : MonoBehaviour
{
    public bool QueryEnabled;
    public float3 QueryExtents;
    public float QueryLength;
    
    public bool DebugMortonCurve;
    public int MortonCurveDebugIterations = int.MaxValue;
    
    public bool DebugBoundingBoxes;
    public int BoundingBoxDebugLevel;
    
    public bool DebugNearestNeighbours;
    public int NearestNeighboursDebugLevel;

    private World World;
    private Entity Entity;
    
    void Start()
    {
        World = World.DefaultGameObjectInjectionWorld;
        Entity = World.EntityManager.CreateEntity();
        World.EntityManager.AddComponentData(Entity, new BVHDebugger());
    }

    void Update()
    {
        World.EntityManager.SetComponentData(Entity, new BVHDebugger
        {
            QueryEnabled = QueryEnabled,
            QueryPosition = transform.position,
            QueryDirection = transform.forward,
            QueryExtents = QueryExtents,
            QueryLength = QueryLength,
            
            DebugMortonCurve = DebugMortonCurve,
            MortonCurveDebugIterations = MortonCurveDebugIterations,
            
            DebugBoundingBoxes = DebugBoundingBoxes,
            BoundingBoxDebugLevel = BoundingBoxDebugLevel,
            
            DebugNearestNeighbours = DebugNearestNeighbours,
            NearestNeighboursDebugLevel = NearestNeighboursDebugLevel,
        });
    }
}
