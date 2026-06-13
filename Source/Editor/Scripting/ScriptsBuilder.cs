// Copyright (c) Wojciech Figat. All rights reserved.

using FlaxEditor.CustomEditors.Dedicated;
using FlaxEngine;
using System;

namespace FlaxEditor
{
    partial class ScriptsBuilder
    {
        /// <summary>
        /// Compilation end event delegate.
        /// </summary>
        /// <param name="success">False if compilation has failed, otherwise true.</param>
        public delegate void CompilationEndDelegate(bool success);

        /// <summary>
        /// Compilation message events delegate.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="file">The target file.</param>
        /// <param name="line">The target line.</param>
        public delegate void CompilationMessageDelegate(string message, string file, int line);

        /// <summary>
        /// Occurs when compilation ends.
        /// </summary>
        public static event CompilationEndDelegate CompilationEnd;

        /// <summary>
        /// Occurs when compilation success.
        /// </summary>
        public static event Action CompilationSuccess;

        /// <summary>
        /// Occurs when compilation failed.
        /// </summary>
        public static event Action CompilationFailed;

        /// <summary>
        /// Occurs when compilation begins.
        /// </summary>
        public static event Action CompilationBegin;

        /// <summary>
        /// Occurs when compilation just started.
        /// </summary>
        public static event Action CompilationStarted;

        /// <summary>
        /// Occurs when user scripts reload action is called.
        /// </summary>
        public static event Action ScriptsReloadCalled;

        /// <summary>
        /// Occurs when user scripts reload starts.
        /// User objects should be removed at this point to reduce leaks and issues. Game scripts and game editor scripts assemblies will be reloaded.
        /// </summary>
        public static event Action ScriptsReloadBegin;

        /// <summary>
        /// Occurs when user scripts reload is performed (just before the actual reload, scenes are serialized and unloaded). All user objects should be cleanup.
        /// </summary>
        public static event Action ScriptsReload;

        /// <summary>
        /// Occurs when user scripts reload ends.
        /// </summary>
        public static event Action ScriptsReloadEnd;

        /// <summary>
        /// Occurs when engine loads game scripts.
        /// </summary>
        public static event Action ScriptsLoaded;

        /// <summary>
        /// Occurs when code editor starts asynchronous open a file or a solution.
        /// </summary>
        public static event Action CodeEditorAsyncOpenBegin;

        /// <summary>
        /// Occurs when code editor ends asynchronous open a file or a solution.
        /// </summary>
        public static event Action CodeEditorAsyncOpenEnd;

        internal enum EventType
        {
            CompileBegin = 0,
            CompileStarted = 1,
            CompileEndGood = 2,
            CompileEndFailed = 3,
            ReloadCalled = 4,
            ReloadBegin = 5,
            Reload = 6,
            ReloadEnd = 7,
            ScriptsLoaded = 8,
        }

        private static int _eventIndex;
        internal static void Internal_OnEvent(EventType type)
        {
            Editor.Log($"[ScriptsBuilder] #{_eventIndex++} Event => {type}");

            switch (type)
            {
            case EventType.CompileBegin:
                CompilationBegin?.Invoke();
                break;
            case EventType.CompileStarted:
                CompilationStarted?.Invoke();
                break;
            case EventType.CompileEndGood:
                CompilationEnd?.Invoke(true);
                CompilationSuccess?.Invoke();
                break;
            case EventType.CompileEndFailed:
                CompilationEnd?.Invoke(false);
                CompilationFailed?.Invoke();
                break;
            case EventType.ReloadCalled:
                    SafeInvoke("ScriptsReloadCalled", ScriptsReloadCalled);
                    //ScriptsReloadCalled?.Invoke();
                break;
            case EventType.ReloadBegin:
                Editor.Log("=== RELOAD BEGIN ===");
                    SafeInvoke("ScriptsReloadBegin", ScriptsReloadBegin);
                    //ScriptsReloadBegin?.Invoke();
                    break;
            case EventType.Reload:
                Editor.Log("=== RELOAD PRE UNLOAD ===");
                    DumpEngineStateBeforeUnload();
                    SafeInvoke("ScriptsReload", ScriptsReload);
                    //ScriptsReload?.Invoke();
                break;
            case EventType.ReloadEnd:
                Editor.Log("=== RELOAD END ===");
                    SafeInvoke("ScriptsReloadEnd", ScriptsReloadEnd);
                    //ScriptsReloadEnd?.Invoke();
                break;
            case EventType.ScriptsLoaded:
                Editor.Log("=== SCRIPTS LOADED ===");
                    SafeInvoke("ScriptsLoaded", ScriptsLoaded);
                    //ScriptsLoaded?.Invoke();
                break;
            }
        }

        internal static void Internal_OnCodeEditorEvent(bool isEnd)
        {
            if (isEnd)
                CodeEditorAsyncOpenEnd?.Invoke();
            else
                CodeEditorAsyncOpenBegin?.Invoke();
        }

        // @Alewinn -----------------------------------------------------
        private static void SafeInvoke(string name, Action evt)
        {
            if (evt == null) return;

            Editor.Log($"[ScriptsBuilder] Invoking {name} -> {evt.GetInvocationList().Length} handlers");

            evt.Invoke();
        }

        private static void DumpEngineStateBeforeUnload()
        {
            Editor.Log("=== ENGINE STATE DUMP BEFORE UNLOAD ===");

            DumpManagedEventLeaks();
            DumpLoadedAssemblies();
            DumpPotentialLeakActors();
        }

        private static void DumpManagedEventLeaks()
        {
            Editor.Log("=== EVENT DELEGATES ===");

            var fields = typeof(ScriptsBuilder)
                .GetFields(System.Reflection.BindingFlags.Static |
                           System.Reflection.BindingFlags.NonPublic |
                           System.Reflection.BindingFlags.Public);

            foreach (var f in fields)
            {
                if (typeof(Delegate).IsAssignableFrom(f.FieldType))
                {
                    var value = f.GetValue(null) as Delegate;
                    if (value != null)
                    {
                        Editor.Log($"[EVENT] {f.Name} => {value.GetInvocationList().Length} subscribers");

                        foreach (var d in value.GetInvocationList())
                            Editor.Log($"  -> {d.Target?.GetType().FullName}");
                    }
                }
            }
        }

        private static void DumpLoadedAssemblies()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.FullName.Contains("Game") || asm.FullName.Contains("Interaction"))
                {
                    Editor.Log($"[ASSEMBLY] {asm.FullName}");
                }
            }
        }
        private static void DumpPotentialLeakActors()
        {
            Editor.Log("=== POTENTIAL LEAK ACTORS ===");

            foreach (var root in Level.GetActors<Actor>())
            {
                DumpActorRecursive(root);
            }
        }
        private static void DumpActorRecursive(Actor actor, int depth = 0)
        {
            if (actor == null)
                return;

            Editor.Log($"{new string(' ', depth * 2)}- {actor.Name} ({actor.GetType().Name})");

            for (int i = 0; i < actor.ChildrenCount; i++)
            {
                DumpActorRecursive(actor.GetChild(i), depth + 1);
            }
        }

    }
}
