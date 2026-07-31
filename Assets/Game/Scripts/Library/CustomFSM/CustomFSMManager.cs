using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class CustomFSMManager : MonoBehaviour
{
    public string fsmName;
    private Enum _currentState;
    private Type _stateEnumType;
    private object _owner;
    private bool _debug;

    private Dictionary<Enum, MethodInfo> _enterMethods = new Dictionary<Enum, MethodInfo>();
    private Dictionary<Enum, MethodInfo> _exitMethods = new Dictionary<Enum, MethodInfo>();

    public void Initialize(Type stateEnumType, Type ownerType, bool debug = false)
    {
        _stateEnumType = stateEnumType;
        _owner = GetComponent(ownerType);
        if (_owner == null)
        {
            _owner = this; // Fallback if not found as component
        }
        _debug = debug;

        // Cache all state methods
        foreach (var state in Enum.GetValues(stateEnumType))
        {
            string stateName = state.ToString();

            MethodInfo enterMethod = ownerType.GetMethod("StateMachineEnter_" + stateName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (enterMethod != null) _enterMethods[(Enum)state] = enterMethod;

            MethodInfo exitMethod = ownerType.GetMethod("StateMachineExit_" + stateName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (exitMethod != null) _exitMethods[(Enum)state] = exitMethod;
        }

        if (_debug) Debug.Log($"[FSM {fsmName}] Initialized with {stateEnumType.Name}");
    }

    public void StateMachineChange(Enum newState, Dictionary<string, object> options = null)
    {
        if (_debug) Debug.Log($"[FSM {fsmName}] Changing state: {_currentState} -> {newState}");

        Enum prevState = _currentState;

        // Exit current state
        if (_currentState != null && _exitMethods.ContainsKey(_currentState))
        {
            _exitMethods[_currentState].Invoke(_owner, new object[] { newState, options });
        }

        _currentState = newState;

        // Enter new state
        if (_currentState != null && _enterMethods.ContainsKey(_currentState))
        {
            _enterMethods[_currentState].Invoke(_owner, new object[] { prevState, options });
        }
    }

    public Enum GetCurrentState()
    {
        return _currentState;
    }
}
