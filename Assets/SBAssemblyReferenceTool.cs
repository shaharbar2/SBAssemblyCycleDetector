using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Shahar.Bar.Utils
{
    public class SBAssemblyRefCycleDetection : EditorWindow
    {
        private List<List<string>> _cycles = new();
        private Dictionary<string, bool> _foldouts = new();
        private Vector2 _scrollPosition;

        [MenuItem("SBTools/Assemblies Cycle Detector")]
        public static void ShowWindow()
        {
            GetWindow<SBAssemblyRefCycleDetection>("Assembly Reference Tool");
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Show Cycles"))
            {
                DetectCycles();
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawCycles();

            EditorGUILayout.EndScrollView();
        }

        private void DetectCycles()
        {
            var assemblies = CompilationPipeline.GetAssemblies();
            var assemblyMap = assemblies.ToDictionary(asm => asm.name, asm => asm);

            foreach (var assembly in assemblies)
            {
                _cycles = DetectCycles(assembly.name, assemblyMap);

                if (_cycles.Count != 0)
                {
                    foreach (var cycle in _cycles)
                    {
                        var cycleKey = string.Join(" -> ", cycle);
                        if (!_foldouts.ContainsKey(cycleKey))
                        {
                            _foldouts[cycleKey] = false; // Initializing foldout state
                        }
                    }
                    break;
                }
            }
        }

        private void DrawCycles()
        {
            if (_cycles.Count == 0)
            {
                EditorGUILayout.LabelField("No Cyclic Dependencies Found", EditorStyles.boldLabel);
                return;
            }

            EditorGUILayout.LabelField("Cyclic Dependencies:", EditorStyles.boldLabel);
            
            for (var index = 0; index < _cycles.Count; index++)
            {
                var cycle = DrawCycleFoldoutTitle(index, out var cycleKey);
                DrawCycleContent(cycleKey, cycle);
            }
        }

        private List<string> DrawCycleFoldoutTitle(int index, out string cycleKey)
        {
            var cycle = _cycles[index];
            cycleKey = string.Join(" -> ", cycle);
            _foldouts[cycleKey] = EditorGUILayout.Foldout(_foldouts[cycleKey], $"Cycle {index + 1}");
            return cycle;
        }

        private void DrawCycleContent(string cycleKey, List<string> cycle)
        {
            if (!_foldouts[cycleKey]) return;
            
            EditorGUILayout.BeginVertical();

            foreach (var item in cycle)
            {
                DrawAsmdefRefButton(item);
            }

            DrawAsmdefRefButton(cycle[0]);
            EditorGUILayout.EndVertical();
        }

        private void DrawAsmdefRefButton(string item)
        {
            if (GUILayout.Button($"{item}"))
            {
                SelectAssemblyInProject(item);
            }
        }

        private void SelectAssemblyInProject(string assemblyName)
        {
            var guids = AssetDatabase.FindAssets(assemblyName + " t:asmdef");
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                
                var asmdefFile = AssetDatabase.LoadAssetAtPath(path, typeof(Object));
                if (asmdefFile == null) continue;
                
                EditorGUIUtility.PingObject(asmdefFile);
                Selection.activeObject = asmdefFile;
                break;
            }
        }
        
        private List<List<string>> DetectCycles(string startAssembly, Dictionary<string, Assembly> assemblyMap)
        {
            var visited = new HashSet<string>();
            var recStack = new HashSet<string>();
            var cycles = new List<List<string>>();

            DepthFirstSearchRecursive(startAssembly, assemblyMap, visited, recStack, cycles, new List<string>());

            return cycles;
        }

        private void DepthFirstSearchRecursive(string current, IReadOnlyDictionary<string, Assembly> assemblyMap, HashSet<string> visited, HashSet<string> recStack, List<List<string>> cycles, List<string> path)
        {
            if (recStack.Contains(current))
            {
                var cycle = path.SkipWhile(x => x != current).ToList();
                cycles.Add(cycle);
                return;
            }

            if (!visited.Add(current))
                return;

            recStack.Add(current);
            path.Add(current);

            if (assemblyMap.TryGetValue(current, out var assembly))
            {
                foreach (var dependency in assembly.assemblyReferences)
                {
                    DepthFirstSearchRecursive(dependency.name, assemblyMap, visited, recStack, cycles, path);
                }
            }

            recStack.Remove(current);
            path.RemoveAt(path.Count - 1);
        }
    }
}