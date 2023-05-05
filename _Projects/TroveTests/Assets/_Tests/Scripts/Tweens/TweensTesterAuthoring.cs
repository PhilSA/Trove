using System.Collections;
using System.Collections.Generic;
using Trove.Tweens;
using Unity.Entities;
using UnityEngine;
using Trove;
using Unity.Transforms;

public class TweensTesterAuthoring : MonoBehaviour
{
    public int StressTestTweens = 100000;

    [Header("References")]
    public GameObject EntityA;
    public GameObject EntityB;
    public GameObject EntityC;
    public GameObject EntityD;
    public GameObject EntityE;
    public GameObject EntityF;
    public GameObject EntityG;
    public GameObject EntityH;
    public GameObject EntityI;
    public GameObject EntityJ;
    public GameObject EntityK;
    public GameObject EntityL;
    public GameObject EntityM;
    public GameObject EntityN;
    public GameObject EntityO;
    public GameObject EntityP;
    public GameObject EntityQ;
    public GameObject EntityR;
    public GameObject EntityS;
    public GameObject EntityT;
    public GameObject EntityU;
    public GameObject EntityV;
    public GameObject EntityW;
    public GameObject EntityX;

    class Baker : Baker<TweensTesterAuthoring>
    {
        public override void Bake(TweensTesterAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.None), new TweensTester
            {
                StressTestTweens = authoring.StressTestTweens,

                EntityA = GetEntity(authoring.EntityA, TransformUsageFlags.Dynamic),
                EntityB = GetEntity(authoring.EntityB, TransformUsageFlags.Dynamic),
                EntityC = GetEntity(authoring.EntityC, TransformUsageFlags.Dynamic),
                EntityD = GetEntity(authoring.EntityD, TransformUsageFlags.Dynamic),
                EntityE = GetEntity(authoring.EntityE, TransformUsageFlags.Dynamic),
                EntityF = GetEntity(authoring.EntityF, TransformUsageFlags.Dynamic),
                EntityG = GetEntity(authoring.EntityG, TransformUsageFlags.Dynamic),
                EntityH = GetEntity(authoring.EntityH, TransformUsageFlags.Dynamic),
                EntityI = GetEntity(authoring.EntityI, TransformUsageFlags.Dynamic),
                EntityJ = GetEntity(authoring.EntityJ, TransformUsageFlags.Dynamic),
                EntityK = GetEntity(authoring.EntityK, TransformUsageFlags.Dynamic),
                EntityL = GetEntity(authoring.EntityL, TransformUsageFlags.Dynamic),
                EntityM = GetEntity(authoring.EntityM, TransformUsageFlags.Dynamic),
                EntityN = GetEntity(authoring.EntityN, TransformUsageFlags.Dynamic),
                EntityO = GetEntity(authoring.EntityO, TransformUsageFlags.Dynamic),
                EntityP = GetEntity(authoring.EntityP, TransformUsageFlags.Dynamic),
                EntityQ = GetEntity(authoring.EntityQ, TransformUsageFlags.Dynamic),
                EntityR = GetEntity(authoring.EntityR, TransformUsageFlags.Dynamic),
                EntityS = GetEntity(authoring.EntityS, TransformUsageFlags.Dynamic),
                EntityT = GetEntity(authoring.EntityT, TransformUsageFlags.Dynamic),
                EntityU = GetEntity(authoring.EntityU, TransformUsageFlags.Dynamic),
                EntityV = GetEntity(authoring.EntityV, TransformUsageFlags.Dynamic),
                EntityW = GetEntity(authoring.EntityW, TransformUsageFlags.Dynamic),
                EntityX = GetEntity(authoring.EntityX, TransformUsageFlags.Dynamic),
            });

            this.AddComponent(GetEntity(TransformUsageFlags.None), new LocalTransform());
        }
    }
}
