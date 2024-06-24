
using System.Collections.Generic;
using Trove.ObjectHandles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Logging;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(MemoryVisualizerTester))]
public class MemoryVisualizerTesterEditor : Editor
{
    private EntityManager _entityManager => World.DefaultGameObjectInjectionWorld.EntityManager;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MemoryVisualizerTester tester = (target as MemoryVisualizerTester);
        if (tester._hasInitialized)
        {
            ref MemoryVisualizer memoryVisualizer = ref MemoryVisualizerTester.TryGetSingletonRW<MemoryVisualizer>(_entityManager, out bool success);
            if (success && memoryVisualizer.TestEntity != Entity.Null)
            {
                DynamicBuffer<byte> bytesBuffer = _entityManager.GetBuffer<TestVirtualObjectElement>(memoryVisualizer.TestEntity).Reinterpret<byte>();

                if (GUILayout.Button("Add Object1"))
                {
                    VirtualObjectHandle<MemoryVisualizerTester.TestObject1> handle = VirtualObjectManager.CreateObject(ref bytesBuffer, new MemoryVisualizerTester.TestObject1());
                    tester._allHandles.Add(handle);
                    tester._obj1Handles.Add(handle);
                    memoryVisualizer.Update = true;
                }
                if (GUILayout.Button("Add Object2"))
                {
                    VirtualObjectHandle<MemoryVisualizerTester.TestObject2> handle = VirtualObjectManager.CreateObject(ref bytesBuffer, new MemoryVisualizerTester.TestObject2());
                    tester._allHandles.Add(handle);
                    tester._obj2Handles.Add(handle);
                    memoryVisualizer.Update = true;
                }
                if (GUILayout.Button("Add Object3"))
                {
                    VirtualObjectHandle<MemoryVisualizerTester.TestObject3> handle = VirtualObjectManager.CreateObject(ref bytesBuffer, new MemoryVisualizerTester.TestObject3());
                    tester._allHandles.Add(handle);
                    tester._obj3Handles.Add(handle);
                    memoryVisualizer.Update = true;
                }

                if (GUILayout.Button("Remove First Object1"))
                {
                    if (tester._obj1Handles.Count > 0)
                    {
                        VirtualObjectHandle removedHandle = tester._obj1Handles[0];

                        VirtualObjectManager.FreeObject(ref bytesBuffer, removedHandle);
                        memoryVisualizer.Update = true;

                        tester._allHandles.Remove(removedHandle);
                        tester._obj1Handles.Remove(removedHandle);
                    }
                }
                if (GUILayout.Button("Remove First Object2"))
                {
                    if (tester._obj2Handles.Count > 0)
                    {
                        VirtualObjectHandle removedHandle = tester._obj2Handles[0];

                        VirtualObjectManager.FreeObject(ref bytesBuffer, removedHandle);
                        memoryVisualizer.Update = true;

                        tester._allHandles.Remove(removedHandle);
                        tester._obj2Handles.Remove(removedHandle);
                    }
                }
                if (GUILayout.Button("Remove First Object3"))
                {
                    if (tester._obj3Handles.Count > 0)
                    {
                        VirtualObjectHandle removedHandle = tester._obj3Handles[0];

                        VirtualObjectManager.FreeObject(ref bytesBuffer, removedHandle);
                        memoryVisualizer.Update = true;

                        tester._allHandles.Remove(removedHandle);
                        tester._obj3Handles.Remove(removedHandle);
                    }
                }

                if (GUILayout.Button("Remove Last Object1"))
                {
                    if (tester._obj1Handles.Count > 0)
                    {
                        VirtualObjectHandle removedHandle = tester._obj1Handles[tester._obj1Handles.Count - 1];

                        VirtualObjectManager.FreeObject(ref bytesBuffer, removedHandle);
                        memoryVisualizer.Update = true;

                        tester._allHandles.Remove(removedHandle);
                        tester._obj1Handles.Remove(removedHandle);
                    }
                }
                if (GUILayout.Button("Remove Last Object2"))
                {
                    if (tester._obj2Handles.Count > 0)
                    {
                        VirtualObjectHandle removedHandle = tester._obj2Handles[tester._obj2Handles.Count - 1];

                        VirtualObjectManager.FreeObject(ref bytesBuffer, removedHandle);
                        memoryVisualizer.Update = true;

                        tester._allHandles.Remove(removedHandle);
                        tester._obj2Handles.Remove(removedHandle);
                    }
                }
                if (GUILayout.Button("Remove Last Object3"))
                {
                    if (tester._obj3Handles.Count > 0)
                    {
                        VirtualObjectHandle removedHandle = tester._obj3Handles[tester._obj3Handles.Count - 1];

                        VirtualObjectManager.FreeObject(ref bytesBuffer, removedHandle);
                        memoryVisualizer.Update = true;

                        tester._allHandles.Remove(removedHandle);
                        tester._obj3Handles.Remove(removedHandle);
                    }
                }

                if (GUILayout.Button("Remove Random Object1"))
                {
                    if (tester._obj1Handles.Count > 0)
                    {
                        int randomIndex = UnityEngine.Random.Range(0, tester._obj1Handles.Count);
                        VirtualObjectHandle removedHandle = tester._obj1Handles[randomIndex];

                        VirtualObjectManager.FreeObject(ref bytesBuffer, removedHandle);
                        memoryVisualizer.Update = true;

                        tester._allHandles.Remove(removedHandle);
                        tester._obj1Handles.Remove(removedHandle);
                    }
                }
                if (GUILayout.Button("Remove Random Object2"))
                {
                    if (tester._obj2Handles.Count > 0)
                    {
                        int randomIndex = UnityEngine.Random.Range(0, tester._obj2Handles.Count);
                        VirtualObjectHandle removedHandle = tester._obj2Handles[randomIndex];

                        VirtualObjectManager.FreeObject(ref bytesBuffer, removedHandle);
                        memoryVisualizer.Update = true;

                        tester._allHandles.Remove(removedHandle);
                        tester._obj2Handles.Remove(removedHandle);
                    }
                }
                if (GUILayout.Button("Remove Random Object3"))
                {
                    if (tester._obj3Handles.Count > 0)
                    {
                        int randomIndex = UnityEngine.Random.Range(0, tester._obj3Handles.Count);
                        VirtualObjectHandle removedHandle = tester._obj3Handles[randomIndex];

                        VirtualObjectManager.FreeObject(ref bytesBuffer, removedHandle);
                        memoryVisualizer.Update = true;

                        tester._allHandles.Remove(removedHandle);
                        tester._obj3Handles.Remove(removedHandle);
                    }
                }

                if (GUILayout.Button("Remove Random Object"))
                {
                    if (tester._allHandles.Count > 0)
                    {
                        int randomIndex = UnityEngine.Random.Range(0, tester._allHandles.Count);
                        VirtualObjectHandle removedHandle = tester._allHandles[randomIndex];

                        VirtualObjectManager.FreeObject(ref bytesBuffer, removedHandle);
                        memoryVisualizer.Update = true;

                        tester._allHandles.Remove(removedHandle);
                        tester._obj1Handles.Remove(removedHandle);
                        tester._obj2Handles.Remove(removedHandle);
                        tester._obj3Handles.Remove(removedHandle);
                    }
                }

                if (GUILayout.Button("Trim Capacity"))
                {
                    VirtualObjectManager.TrimCapacity(ref bytesBuffer, 2, 2);
                    memoryVisualizer.Update = true;
                }

                if (GUILayout.Button("Reinitialize"))
                {
                    VirtualObjectManager.Initialize(ref bytesBuffer, tester.ObjectsCapacity, tester.ObjectDataBytesCapacity);
                    memoryVisualizer.Update = true;
                }
            }
        }
    }
}
#endif

public class MemoryVisualizerTester : MonoBehaviour
{
    public enum TestVOType
    {
        Object1,
        Object2,
        Object3,
    }

    public struct TestObject1
    {
        public int A;
    }

    public struct TestObject2
    {
        public float3 A;
    }

    public struct TestObject3
    {
        public int4 A;
        public int4 B;
        public int4 C;
    }

    public int ObjectsCapacity = 12;
    public int ObjectDataBytesCapacity = 64;

    public List<VirtualObjectHandle> _allHandles = new List<VirtualObjectHandle>();
    [NonSerialized]
    public List<VirtualObjectHandle> _obj1Handles = new List<VirtualObjectHandle>();
    [NonSerialized]
    public List<VirtualObjectHandle> _obj2Handles = new List<VirtualObjectHandle>();
    [NonSerialized]
    public List<VirtualObjectHandle> _obj3Handles = new List<VirtualObjectHandle>();

    [NonSerialized]
    public bool _hasInitialized = false;
    private EntityManager _entityManager => World.DefaultGameObjectInjectionWorld.EntityManager;


    private void Update()
    {
        if (!_hasInitialized)
        {
            ref MemoryVisualizer memoryVisualizer = ref TryGetSingletonRW<MemoryVisualizer>(_entityManager, out bool success);
            if(success && memoryVisualizer.TestEntity != Entity.Null)
            {
                _allHandles.Clear();

                DynamicBuffer<byte> bytesBuffer = _entityManager.GetBuffer<TestVirtualObjectElement>(memoryVisualizer.TestEntity).Reinterpret<byte>();
                VirtualObjectManager.Initialize(ref bytesBuffer, ObjectsCapacity, ObjectDataBytesCapacity);

                _hasInitialized = true;

                memoryVisualizer.Update = true;
            }
        }
    }

    public unsafe static ref T TryGetSingletonRW<T>(EntityManager entityManager, out bool success) where T : unmanaged, IComponentData
    {
        EntityQuery singletonQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<T>().Build(entityManager);
        if (singletonQuery.HasSingleton<T>())
        {
            success = true;
            return ref singletonQuery.GetSingletonRW<T>().ValueRW;
        }

        success = false;
        return ref *(T*)null;
    }
}
