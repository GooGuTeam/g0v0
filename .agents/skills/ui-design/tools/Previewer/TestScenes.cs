// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Internal;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Testing;

namespace Previewer
{
    /// <summary>
    /// Finds the client's visual test scenes and adds their steps as the test browser would.
    /// </summary>
    public static class TestScenes
    {
        public static Type Find(string name)
        {
            foreach (string file in Directory.GetFiles(AppContext.BaseDirectory, "osu.Game*.Tests.dll"))
                Assembly.Load(Path.GetFileNameWithoutExtension(file));

            var matches = AppDomain.CurrentDomain.GetAssemblies()
                                   .Where(a => a.GetName().Name?.EndsWith(".Tests", StringComparison.Ordinal) == true)
                                   .SelectMany(a => a.GetTypes())
                                   .Where(t => t.IsSubclassOf(typeof(TestScene)) && t is { IsAbstract: false, IsPublic: true })
                                   .Where(t => t.FullName == name || t.Name == name || t.Name == "TestScene" + name)
                                   .ToArray();

            return matches.Length switch
            {
                1 => matches[0],
                0 => throw new ArgumentException($"No visual test scene named {name}."),
                _ => throw new ArgumentException($"{name} is ambiguous; use the full name: {string.Join(", ", matches.Select(t => t.FullName))}."),
            };
        }

        /// <summary>
        /// Adds the [SetUp] and [SetUpSteps] steps, then the steps of one test method (the <paramref name="testCase"/>th of its cases), if given.
        /// </summary>
        public static void AddSteps(TestScene scene, string? test, int testCase)
        {
            var setUps = methodsWith<SetUpAttribute>(scene.GetType());

            if (setUps.Length > 0)
                scene.AddStep("[SetUp]", () => setUps.ForEach(m => m.Invoke(scene, null)));

            foreach (var method in methodsWith<SetUpStepsAttribute>(scene.GetType()))
                method.Invoke(scene, null);

            if (test == null)
                return;

            var candidates = scene.GetType().GetMethods().Where(m => m.Name == test || m.Name == "Test" + test).ToArray();

            if (candidates.Length != 1)
                throw new ArgumentException($"{scene.GetType().Name} has {candidates.Length} methods named {test}.");

            object?[][] arguments = argumentsFor(candidates[0]).ToArray();

            if (testCase < 0 || testCase >= arguments.Length)
                throw new ArgumentException($"{candidates[0].Name} has {arguments.Length} cases; --case must be below that.");

            candidates[0].Invoke(scene, arguments[testCase]);
        }

        private static IEnumerable<object?[]> argumentsFor(MethodInfo method)
        {
            var wrapper = new MethodWrapper(method.DeclaringType!, method);
            var cases = method.GetCustomAttributes<TestCaseAttribute>(false).Select(tc => tc.BuildFrom(wrapper, null).Single().Arguments)
                              .Concat(method.GetCustomAttributes<TestCaseSourceAttribute>(false).SelectMany(tcs => tcs.BuildFrom(wrapper, null).Select(t => t.Arguments)))
                              .ToList();

            if (method.GetCustomAttribute<TestAttribute>(false) != null)
            {
                if (method.GetParameters().Length == 0)
                    cases.Add(Array.Empty<object?>());
                else
                {
                    var values = wrapper.GetParameters().Select(p => p.GetCustomAttributes<ValuesAttribute>(false).Single().GetData(p).Cast<object?>());
                    cases.AddRange(values.CartesianProduct().Select(c => c.ToArray()));
                }
            }

            return cases;
        }

        // GetMethods already returns one entry per overridden slot (the most-derived declaration, so an override is not run twice).
        // Attributes are matched with inherit so an override without its own attribute still counts, and grouping by declaring type
        // keeps base classes first.
        private static MethodInfo[] methodsWith<T>(Type type) where T : Attribute
        {
            var hierarchy = new List<Type>();

            for (var t = type; t != null; t = t.BaseType)
                hierarchy.Insert(0, t);

            var byDeclaringType = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                                      .Where(m => m.IsDefined(typeof(T), inherit: true))
                                      .ToLookup(m => m.DeclaringType);

            return hierarchy.SelectMany(t => byDeclaringType[t]).ToArray();
        }
    }
}
