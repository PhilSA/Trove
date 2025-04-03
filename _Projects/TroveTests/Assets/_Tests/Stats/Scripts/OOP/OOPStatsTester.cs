using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Trove.Stats;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

public class OOPStatsTester : MonoBehaviour
{
    public int ChangingAttributesCount;
    public int ChangingAttributesChildDepth;
    public int UnchangingAttributesCount;

    [NonSerialized]
    private List<OOP_StatOwner> _StatOwners = new List<OOP_StatOwner>();
    [NonSerialized]
    private List<OOP_StatOwner> _UpdatingStatOwners = new List<OOP_StatOwner>();

    void Start()
    {
        for (int i = 0; i < UnchangingAttributesCount; i++)
        {
            OOP_StatOwner statOwner = CreateStatOwner();
            _StatOwners.Add(statOwner);
        }

        for (int i = 0; i < ChangingAttributesCount; i++)
        {
            OOP_StatOwner statOwner = CreateStatOwner();
            _StatOwners.Add(statOwner);
            _UpdatingStatOwners.Add(statOwner);

            OOP_StatOwner parentStatOwner = statOwner;

            for (int c = 0; c < ChangingAttributesChildDepth; c++)
            {
                OOP_StatOwner childStatOwner = CreateStatOwner();
                _StatOwners.Add(childStatOwner);

                OOP_StatModifier modifier = new OOP_StatModifier
                {
                    ModifierType = OOP_StatModifier.Type.AddFromStat,
                    StatA = parentStatOwner.StatA,
                    ValueA = 1f,
                };
                childStatOwner.StatA.AddModifier(modifier);

                parentStatOwner = childStatOwner;
            }
        }
    }

    void Update()
    {
        for (int i = 0; i < _UpdatingStatOwners.Count; i++)
        {
            OOP_StatOwner o = _UpdatingStatOwners[i];
            o.StatA.BaseValue += Time.deltaTime;
            o.StatA.Recalculate();
        }
    }

    OOP_StatOwner CreateStatOwner()
    {
        OOP_StatOwner owner = new OOP_StatOwner();
        owner.StatA = new OOP_Stat { BaseValue = 10f, Value = 10f };
        owner.StatB = new OOP_Stat { BaseValue = 10f, Value = 10f };
        owner.StatC = new OOP_Stat { BaseValue = 10f, Value = 10f };
        return owner;
    }
}

[System.Serializable]
public class OOP_StatOwner
{
    public OOP_Stat StatA;
    public OOP_Stat StatB;
    public OOP_Stat StatC;
}

[System.Serializable]
public class OOP_Stat
{
    public float BaseValue;
    public float Value;

    public List<OOP_StatModifier> Modifiers = new List<OOP_StatModifier>();

    public System.Action OnValueChanged;

    public void Recalculate()
    {
        OOP_StatModifier.Stack modifierStack = new OOP_StatModifier.Stack();
        modifierStack.Reset();
        for (int m = 0; m < Modifiers.Count; m++)
        {
            OOP_StatModifier modifier = Modifiers[m];
            modifier.Apply(ref modifierStack);
        }
        modifierStack.Apply(this);
         
        OnValueChanged?.Invoke();
    }

    public void AddModifier(OOP_StatModifier modifier)
    {
        Modifiers.Add(modifier);
        modifier.OnAdded(this);
    }
}

[System.Serializable]
public class OOP_StatModifier
{
    public enum Type
    {
        Add,
        AddFromStat,
        AddMultiplier,
        AddMultiplierFromStat,
    }

    public struct Stack
    {
        public float Add;
        public float AddMultiply;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            Add = 0f;
            AddMultiply = 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(OOP_Stat stat)
        {
            stat.Value = stat.BaseValue;
            stat.Value += Add;
            stat.Value *= AddMultiply;
        }
    }

    public Type ModifierType;
    public float ValueA;
    public OOP_Stat StatA;

    public void OnAdded(OOP_Stat affectedStat)
    {
        switch (ModifierType)
        {
            case (Type.AddFromStat):
            case (Type.AddMultiplierFromStat):
                StatA.OnValueChanged += affectedStat.Recalculate;
                break;
        }
    }

    public void Apply(ref Stack stack)
    {
        switch (ModifierType)
        {
            case (Type.Add):
                {
                    stack.Add += ValueA;
                    break;
                }
            case (Type.AddFromStat):
                {
                    if (StatA != null)
                    {
                        stack.Add += StatA.Value;
                    }
                    break;
                }
            case (Type.AddMultiplier):
                {
                    stack.AddMultiply += ValueA;
                    break;
                }
            case (Type.AddMultiplierFromStat):
                {
                    if (StatA != null)
                    {
                        stack.AddMultiply += StatA.Value;
                    }
                    break;
                }
        }
    }
}
