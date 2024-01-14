using System.IO;
using System.Collections.Generic;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;
using NeovimEditor.Editor.ProjectGeneration;

namespace NeovimEditor.Editor {
    public class NeovimCodeEditor : IExternalCodeEditor {
        public static string CurrentEditor => EditorPrefs.GetString("kScriptsDefaultApp");

        private IGenerator _projectGenerator;
        private NeovimFileWatcher _fileWatcher;

        public CodeEditor.Installation[] Installations => GetNeovimPath();

        private CodeEditor.Installation[] GetNeovimPath() {
            if(Integration.NvimIntegration.GetNvimExecutablePath(out var nvimPath)) {
                return new CodeEditor.Installation[] { new() { Name = "nvim", Path = nvimPath}};
            } else {
                Debug.LogError("nvim executable not found in path, integration will not function properly.");
                return new CodeEditor.Installation[] { new() { Name = "!nvim not found", Path = "/bin/sh"}};
            }
        }

        public NeovimCodeEditor() {
            _fileWatcher = new NeovimFileWatcher();
            _fileWatcher.FilesModified += FilesModified;

            _projectGenerator = new ProjectGeneration.ProjectGeneration();
        }

        private void FilesModified(string[] files) {
            AssetDatabase.Refresh();
        }

        public void Initialize(string editorInstallationPath) {
            CreateSolutionIfDoesntExist();
        }

        private void CreateSolutionIfDoesntExist() {
            if (!_projectGenerator.HasSolutionBeenGenerated()) {
                _projectGenerator.Sync();
            }
        }

        public void OnGUI() {
            EditorGUILayout.LabelField("Generate .csproj files for:");
            EditorGUI.indentLevel++;
            SettingsButton(ProjectGenerationFlag.Embedded, "Embedded packages", "");
            SettingsButton(ProjectGenerationFlag.Local, "Local packages", "");
            SettingsButton(ProjectGenerationFlag.Registry, "Registry packages", "");
            SettingsButton(ProjectGenerationFlag.Git, "Git packages", "");
            SettingsButton(ProjectGenerationFlag.BuiltIn, "Built-in packages", "");
#if UNITY_2019_3_OR_NEWER
            SettingsButton(ProjectGenerationFlag.LocalTarBall, "Local tarball", "");
#endif
            SettingsButton(ProjectGenerationFlag.Unknown, "Packages from unknown sources", "");
            SettingsButton(ProjectGenerationFlag.PlayerAssemblies, "Player projects", "For each player project generate an additional csproj with the name 'project-player.csproj'");
            RegenerateProjectFiles();
            EditorGUI.indentLevel--;
        }

        private void RegenerateProjectFiles() {
            var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(new GUILayoutOption[] { }));
            rect.width = 252;
            if (GUI.Button(rect, "Regenerate project files")) {
                _projectGenerator.Sync();
            }
        }

        private void SettingsButton(ProjectGenerationFlag preference, string guiMessage, string toolTip) {
            var prevValue = _projectGenerator.AssemblyNameProvider.ProjectGenerationFlag.HasFlag(preference);
            var newValue = EditorGUILayout.Toggle(new GUIContent(guiMessage, toolTip), prevValue);
            if (newValue != prevValue) {
                _projectGenerator.AssemblyNameProvider.ToggleProjectGeneration(preference);
            }
        }

        public bool OpenProject(string path, int line, int column) {
            var projectGeneration = (ProjectGeneration.ProjectGeneration)_projectGenerator;

            // Assets - Open C# Project passes empty path here
            if (path != "" && !projectGeneration.HasValidExtension(path)) {
                return false;
            }

            var slnFile = GetSolutionFile(path);
            Integration.NvimIntegration.OpenFile(Installations[0].Path, slnFile, path, line, column);
            return true;
        }

        private string GetSolutionFile(string _) {
            var solutionFile = _projectGenerator.SolutionFile();
            return File.Exists(solutionFile) ? solutionFile : "";
        }

        public void SyncAll() {
            _projectGenerator.Sync();
        }

        public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles) {
            var files = new List<string>();
            foreach (var file in addedFiles) {
                if (files.Contains(file)) {
                    continue;
                }

                files.Add(file);
            }
            foreach (var file in deletedFiles) {
                if (files.Contains(file)) {
                    continue;
                }

                files.Add(file);
            }
            foreach (var file in movedFiles) {
                if (files.Contains(file)) {
                    continue;
                }

                files.Add(file);
            }
            foreach (var file in movedFromFiles) {
                if (files.Contains(file)) {
                    continue;
                }

                files.Add(file);
            }
            if (_projectGenerator.SyncIfNeeded(files, importedFiles)) {
                // csproj has been updated, restart omnisharp
                Integration.NvimIntegration.RestartOmnisharp(Installations[0].Path);
            }
        }

        public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation) {
            if(Integration.NvimIntegration.GetNvimExecutablePath(out var nvimPath)) {
                installation = new() { Name = "nvim", Path = nvimPath};
                return true;
            } else {
                installation = new() {};
                return false;
            }
        }
    }
}