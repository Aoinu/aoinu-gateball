using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework.Interfaces;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Pm.Booth.Aoinu607.Udon.Gateball.Editor
{
    [InitializeOnLoad]
    internal static class GateballTestRunnerCompatibility
    {
        // SDK 3.10.5 UnityEventFilter removes Test Runner callback fields from
        // the temporary PlayMode runner. Clear only its cached allow-list while
        // tests run, then rebuild it before returning to edit mode.
        private static readonly TestRunnerApi testRunnerApi;
        private static readonly TestRunCallbacks callbacks;
        private static readonly Type playModeLauncherType = Type.GetType(
            "UnityEditor.TestTools.TestRunner.PlaymodeLauncher, UnityEditor.TestRunner");
        private static readonly FieldInfo playModeLauncherRunningField = playModeLauncherType == null
            ? null
            : playModeLauncherType.GetField(
                "IsRunning",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo testRunnerIsRunActiveMethod = typeof(TestRunnerApi).GetMethod(
            "IsRunActive",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static bool unityEventTypesCleared;

        static GateballTestRunnerCompatibility()
        {
            testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new TestRunCallbacks();
            testRunnerApi.RegisterCallbacks(callbacks, 10000);
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update += DisableFilterBeforePlayModeTest;
        }

        private static void DisableFilterBeforePlayModeTest()
        {
            if (unityEventTypesCleared || !IsPlayModeTestRunning())
            {
                return;
            }

            ClearUnityEventTypes();
        }

        private static bool IsPlayModeTestRunning()
        {
            if (playModeLauncherRunningField != null && (bool)playModeLauncherRunningField.GetValue(null))
            {
                return true;
            }

            return testRunnerIsRunActiveMethod != null && (bool)testRunnerIsRunActiveMethod.Invoke(null, null);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ClearUnityEventTypes();
            }
            else if (state == PlayModeStateChange.ExitingEditMode && IsPlayModeTestRunning())
            {
                ClearUnityEventTypes();
            }
            else if (state == PlayModeStateChange.EnteredEditMode && unityEventTypesCleared)
            {
                RestoreUnityEventTypes();
            }
        }

        private static void ClearUnityEventTypes()
        {
            IDictionary unityEventTypes = GetUnityEventTypes();
            if (unityEventTypes == null)
            {
                return;
            }

            unityEventTypes.Clear();
            unityEventTypesCleared = true;
        }

        private static void RestoreUnityEventTypes()
        {
            Type filterType = Type.GetType("VRC.Core.UnityEventFilter, VRC.SDK3");
            if (filterType == null)
            {
                return;
            }

            MethodInfo initializer = filterType.GetMethod(
                "InitTypesWithUnityEvents",
                BindingFlags.Static | BindingFlags.NonPublic);
            IDictionary unityEventTypes = GetUnityEventTypes();
            if (initializer == null || unityEventTypes == null)
            {
                return;
            }

            IDictionary restoredTypes = (IDictionary)initializer.Invoke(null, null);
            foreach (DictionaryEntry entry in restoredTypes)
            {
                unityEventTypes.Add(entry.Key, entry.Value);
            }

            unityEventTypesCleared = false;
        }

        private static IDictionary GetUnityEventTypes()
        {
            Type filterType = Type.GetType("VRC.Core.UnityEventFilter, VRC.SDK3");
            if (filterType == null)
            {
                return null;
            }

            FieldInfo lazyField = filterType.GetField(
                "_typesWithUnityEventFields",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (lazyField == null)
            {
                return null;
            }

            object lazyValue = lazyField.GetValue(null);
            PropertyInfo valueProperty = lazyValue.GetType().GetProperty("Value");
            return valueProperty == null ? null : (IDictionary)valueProperty.GetValue(lazyValue, null);
        }

        private sealed class TestRunCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                ClearUnityEventTypes();
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                RestoreUnityEventTypes();
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }
    }
}
