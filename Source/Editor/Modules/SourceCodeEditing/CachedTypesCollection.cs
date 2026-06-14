// Copyright (c) Wojciech Figat. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using FlaxEditor.Scripting;
using FlaxEngine;
using FlaxEngine.Utilities;

namespace FlaxEditor.Modules.SourceCodeEditing
{
    /// <summary>
    /// Cached types collection container.
    /// </summary>
    [HideInEditor]
    public class CachedTypesCollection
    {
        // @Alewinn : short-circuit cached values to temporary remove hotload bug
        private bool _hasValidData;
        //private bool _hasValidData { get { return false; } set { } }

        private readonly int _capacity;
        private readonly Func<ScriptType, bool> _checkFunc;
        private readonly Func<Assembly, bool> _checkAssembly;

        /// <summary>
        /// The type.
        /// </summary>
        protected readonly ScriptType _type;

        /// <summary>
        /// The list.
        /// </summary>
        protected List<ScriptType> _list;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedTypesCollection"/> class.
        /// </summary>
        /// <param name="capacity">The initial collection capacity.</param>
        /// <param name="type">The type of things to find. It can be attribute to find all classes with the given attribute defined.</param>
        /// <param name="checkFunc">Additional callback used to check if the given type is valid. Returns true if add type, otherwise false.</param>
        /// <param name="checkAssembly">Additional callback used to check if the given assembly is valid. Returns true if search for types in the given assembly, otherwise false.</param>
        public CachedTypesCollection(int capacity, ScriptType type, Func<ScriptType, bool> checkFunc, Func<Assembly, bool> checkAssembly)
        {
            _capacity = capacity;
            _type = type;
            _checkFunc = checkFunc;
            _checkAssembly = checkAssembly;

            Editor.Log($"CTOR {GetType().Name} {GetHashCode()}");
        }

        /// <summary>
        /// Gets the type matching the certain Script.
        /// </summary>
        /// <param name="script">The content item.</param>
        /// <returns>The type matching that item, or null if not found.</returns>
        public ScriptType Get(Content.ScriptItem script)
        {
            foreach (var type in Get())
            {
                if (type.ContentItem == script)
                    return type;
            }
            return ScriptType.Null;
        }

        /// <summary>
        /// Gets all the types from the all loaded assemblies (including project scripts and scripts from the plugins).
        /// </summary>
        /// <returns>The types collection (readonly).</returns>
        public List<ScriptType> Get()
        {
            Editor.Log(
                $"GET {GetType().Name} " +
                $"valid={_hasValidData} " +
                $"count={_list?.Count ?? 0}" +
                $"ListId={_list?.GetHashCode()}");

            if (!_hasValidData)
            {
                //if (_list == null)
                _list ??= new List<ScriptType>(_capacity);
                _list.Clear();

                Editor.Log($"REBUILD {GetType().Name}");
                _hasValidData = true;

                Editor.Log("Searching for valid " + (_type != ScriptType.Null ? _type.ToString() : "types"));
                Profiler.BeginEvent("Search " + _type);
                var start = DateTime.Now;

                Search();

                var end = DateTime.Now;
                Profiler.EndEvent();
                Editor.Log(string.Format("Found {0} types (in {1} ms)", _list.Count, (int)(end - start).TotalMilliseconds));
            }

            return _list;
        }

        /// <summary>
        /// Searches for the types and fills with data.
        /// </summary>
        protected virtual void Search()
        {
            if (_type == ScriptType.Null)
            {
                // Special case for all types
                TypeUtils.GetTypes(_list, _checkFunc, _checkAssembly);
            }
            else if (_type.Type != null && new ScriptType(typeof(Attribute)).IsAssignableFrom(_type))
            {
                // Special case for attributes
                TypeUtils.GetTypesWithAttributeDefined(_type.Type, _list, _checkFunc, _checkAssembly);
            }
            else
            {
                TypeUtils.GetDerivedTypes(_type, _list, _checkFunc, _checkAssembly);
            }
        }

        /// <summary>
        /// Clears the types.
        /// </summary>
        public virtual void ClearTypes()
        {
            _list?.Clear();
            _hasValidData = false;

            // Alewinn
            // Need unreadable logs ?
            // Comment 2 lines above / Uncomment line below.
            // N-Joy.
            // LogedClearType();
        }


        private static readonly List<WeakReference> _oldLists = new();
        private void LogedClearType()
        {
            _oldLists.Add(new WeakReference(_list));

            Editor.Log($"ClearTypes on {GetType().Name} id={GetHashCode()}");
            if (_list != null)
            {
                foreach (var t in _list)
                {
                    if (t.Type != null)
                    {
                        var asm = t.Type.Assembly;

                        if (asm.GetName().Name == "Game.CSharp")
                        {
                            Editor.LogWarning(
                                $"CLEAR Game type {t.Type.FullName}");
                        }
                    }
                }
            }
            _list?.Clear();
            _list = new List<ScriptType>();

            _hasValidData = false;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            for (int i = 0; i < _oldLists.Count; i++)
            {
                Editor.Log($"OldList[{i}] Alive={_oldLists[i].IsAlive}");
            }

            int gameTypes = 0;

            foreach (var t in _list)
            {
                if (t.Type?.Assembly?.GetName().Name == "Game.CSharp")
                    gameTypes++;
            }

            Editor.LogWarning(
                $"{GetType().Name} clearing {gameTypes} Game types");
        }
    }
}
