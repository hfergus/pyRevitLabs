﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Security.Principal;

using pyRevitLabs.Common;
using pyRevitLabs.Common.Extensions;

using MadMilkman.Ini;
using Newtonsoft.Json.Linq;
using NLog;

namespace pyRevitLabs.TargetApps.Revit {
    // main pyrevit functionality class
    public static class PyRevit {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // STANDARD PATHS ============================================================================================
        // pyRevit %appdata% path
        // @reviewed
        public static string pyRevitAppDataPath {
            get {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                        PyRevitConsts.AppdataDirName
                    );
            }
        }

        // pyRevit %programdata% path
        // @reviewed
        public static string pyRevitProgramDataPath {
            get {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.CommonApplicationData),
                        PyRevitConsts.AppdataDirName
                    );
            }
        }

        // pyRevit config file path
        // @reviewed
        public static string pyRevitConfigFilePath { get { return FindConfigFileInDirectory(pyRevitAppDataPath); } }

        // pyRevit config file path
        // @reviewed
        public static string pyRevitSeedConfigFilePath { get { return FindConfigFileInDirectory(pyRevitProgramDataPath); } }

        // pyrevit cache folder 
        // @reviewed
        public static string GetCacheDirectory(int revitYear) {
            return Path.Combine(pyRevitAppDataPath, revitYear.ToString());
        }

        // pyrevit logs folder 
        // @reviewed
        public static string GetLogsDirectory() {
            return Path.Combine(pyRevitAppDataPath, PyRevitConsts.AppdataLogsDirName);
        }

        // managing clones ===========================================================================================
        // check at least one pyRevit clone is available
        public static bool IsInstalled() {
            return GetRegisteredClones().Count > 0;
        }

        // install pyRevit by cloning from git repo
        // @handled @logs
        public static void Clone(string cloneName,
                                 bool coreOnly = false,
                                 string branchName = null,
                                 string repoPath = null,
                                 string destPath = null) {
            string repoSourcePath = repoPath ?? PyRevitConsts.OriginalRepoPath;
            string repoBranch = branchName != null ? branchName : PyRevitConsts.OriginalRepoMainBranch;
            logger.Debug(string.Format("Repo source determined as \"{0}:{1}\"", repoSourcePath, repoBranch));

            // determine destination path if not provided
            if (destPath == null) {
                destPath = Path.Combine(pyRevitAppDataPath, PyRevitConsts.InstallName);
            }
            logger.Debug(string.Format("Destination path determined as \"{0}\"", destPath));

            // start the clone process
            LibGit2Sharp.Repository repo = null;
            if (coreOnly) {
                // TODO: Add core checkout option. Figure out how to checkout certain folders in libgit2sharp
                throw new NotImplementedException("Core checkout option not implemented yet.");
            }
            else {
                repo = GitInstaller.Clone(repoSourcePath, repoBranch, destPath);
            }

            // Check installation
            if (repo != null) {
                // make sure to delete the repo if error occured after cloning
                var clonedPath = repo.Info.WorkingDirectory;
                try {
                    if (PyRevitClone.VerifyRepoValidity(clonedPath)) {
                        logger.Debug(string.Format("Clone successful \"{0}\"", clonedPath));
                        RegisterClone(cloneName, clonedPath);
                    }
                }
                catch (Exception ex) {
                    logger.Debug(string.Format("Exception occured after clone complete. Deleting clone \"{0}\" | {1}",
                                               clonedPath, ex.Message));
                    try {
                        CommonUtils.DeleteDirectory(clonedPath);
                    }
                    catch (Exception delEx) {
                        logger.Error(string.Format("Error post-install cleanup on \"{0}\" | {1}",
                                                   clonedPath, delEx.Message));
                    }

                    // cleanup completed, now baloon up the exception
                    throw ex;
                }
            }
            else
                throw new pyRevitException(string.Format("Error installing pyRevit. Null repo error on \"{0}\"",
                                                         repoPath));
        }

        // uninstall primary or specified clone, has option for clearing configs
        // @handled @logs
        public static void Uninstall(PyRevitClone clone, bool clearConfigs = false) {
            logger.Debug(string.Format("Unregistering clone \"{0}\"", clone));
            UnregisterClone(clone);

            logger.Debug(string.Format("Removing directory \"{0}\"", clone.RepoPath));
            CommonUtils.DeleteDirectory(clone.RepoPath);

            if (clearConfigs)
                DeleteConfigs();
        }

        // uninstall all registered clones
        // @handled @logs
        public static void UninstallAllClones(bool clearConfigs = false) {
            foreach (var clone in GetRegisteredClones())
                Uninstall(clone, clearConfigs: false);

            if (clearConfigs)
                DeleteConfigs();
        }

        // deletes config file
        // @handled @logs
        public static void DeleteConfigs() {
            if (File.Exists(pyRevitConfigFilePath))
                try {
                    File.Delete(pyRevitConfigFilePath);
                }
                catch (Exception ex) {
                    throw new pyRevitException(string.Format("Failed deleting config file \"{0}\" | {1}",
                                                              pyRevitConfigFilePath, ex.Message));
                }
        }

        // force update given or all registered clones
        // @handled @logs
        public static void UpdateAllClones() {
            logger.Debug("Updating all pyRevit clones");
            foreach (var clone in GetRegisteredClones())
                clone.Update();
        }

        // clear cache
        // @handled @logs
        public static void ClearCache(int revitYear) {
            // make sure all revit instances are closed
            if (CommonUtils.VerifyPath(pyRevitAppDataPath)) {
                RevitController.KillRunningRevits(revitYear);
                CommonUtils.DeleteDirectory(GetCacheDirectory(revitYear));
            }
            else
                throw new pyRevitResourceMissingException(pyRevitAppDataPath);
        }

        // clear all caches
        // @handled @logs
        public static void ClearAllCaches() {
            var cacheDirFinder = new Regex(@"\d\d\d\d");
            if (CommonUtils.VerifyPath(pyRevitAppDataPath)) {
                foreach (string subDir in Directory.GetDirectories(pyRevitAppDataPath)) {
                    var dirName = Path.GetFileName(subDir);
                    if (cacheDirFinder.IsMatch(dirName))
                        ClearCache(int.Parse(dirName));
                }
            }
            else
                throw new pyRevitResourceMissingException(pyRevitAppDataPath);
        }

        // managing copies ===========================================================================================
        public static void Copy(string copyName,
                                bool coreOnly = false,
                                string branchName = null,
                                string archivePath = null,
                                string destPath = null) {
            string repoBranch = branchName != null ? branchName : PyRevitConsts.OriginalRepoMainBranch;
            string archiveFileUrl = PyRevitConsts.GetZipPackageUrl(repoBranch);
            string archiveFilePath = null;
            try {
                archiveFilePath =
                    CommonUtils.DownloadFile(
                        PyRevitConsts.GetZipPackageUrl(repoBranch),
                        Path.Combine(Environment.GetEnvironmentVariable("TEMP"),
                                    Path.GetFileName(archiveFileUrl))
                    );
            }
            catch (Exception ex) {
                throw new pyRevitException(
                    string.Format("Error downloading repo archive file \"{0}\" | {1}", archiveFileUrl, ex.Message)
                    );
            }

            // now extract the file
            if (archiveFilePath != null) {
                ZipFile.ExtractToDirectory(archiveFilePath,
                                           Path.Combine(Environment.GetEnvironmentVariable("TEMP"),
                                                        "pyRevitArchive"));
            }

            // TODO: now copy the needed directories
            // TODO: register the copy
            // TODO: cleanup
        }

        // managing attachments ======================================================================================
        // attach primary or given clone to revit version
        // @handled @logs
        public static void Attach(int revitYear,
                                  PyRevitClone clone,
                                  int engineVer = 000,
                                  bool allUsers = false) {
            // make the addin manifest file
            logger.Debug(string.Format("Attaching Clone \"{0}\" @ \"{1}\" to Revit {0}",
                                        clone.Name, clone.RepoPath, revitYear));
            Addons.CreateManifestFile(revitYear,
                                      PyRevitConsts.AddinFileName,
                                      PyRevitConsts.AddinName,
                                      GetEnginePath(clone.RepoPath, engineVer),
                                      PyRevitConsts.AddinId,
                                      PyRevitConsts.AddinClassName,
                                      PyRevitConsts.VendorId,
                                      allusers: allUsers);
        }

        // attach clone to all installed revit versions
        // @handled @logs
        public static void AttachToAll(PyRevitClone clone, int engineVer = 000, bool allUsers = false) {
            foreach (var revit in RevitController.ListInstalledRevits())
                Attach(revit.FullVersion.Major, clone, engineVer: engineVer, allUsers: allUsers);
        }

        // detach from revit version
        // @handled @logs
        public static void Detach(int revitYear) {
            logger.Debug(string.Format("Detaching from Revit {0}", revitYear));
            Addons.RemoveManifestFile(revitYear, PyRevitConsts.AddinName);
        }

        // detach from all attached revits
        // @handled @logs
        public static void DetachAll() {
            foreach (var revit in GetAttachedRevits()) {
                Detach(revit.FullVersion.Major);
            }
        }

        public static PyRevitClone GetAttachedClone(int revitYear) {
            logger.Debug(string.Format("Querying clone attached to Revit {0}", revitYear));
            var localManif = Addons.GetManifest(revitYear, PyRevitConsts.AddinName, allUsers: false);
            string assemblyPath = null;

            if (localManif != null)
                assemblyPath = localManif.Assembly;
            else {
                var alluserManif = Addons.GetManifest(revitYear, PyRevitConsts.AddinName, allUsers: true);
                if (alluserManif != null)
                    assemblyPath = alluserManif.Assembly;
            }

            if (assemblyPath != null)
                foreach (var clone in GetRegisteredClones())
                    if (assemblyPath.Contains(clone.RepoPath))
                        return clone;

            return null;
        }

        // get all attached revit versions
        // @handled @logs
        public static List<RevitProduct> GetAttachedRevits() {
            var attachedRevits = new List<RevitProduct>();

            foreach (var revit in RevitController.ListInstalledRevits()) {
                logger.Debug(string.Format("Checking attachment to Revit \"{0}\"", revit.Version));
                if (Addons.GetManifest(revit.FullVersion.Major, PyRevitConsts.AddinName, allUsers: false) != null
                    || Addons.GetManifest(revit.FullVersion.Major, PyRevitConsts.AddinName, allUsers: true) != null) {
                    logger.Debug(string.Format("pyRevit is attached to Revit \"{0}\"", revit.Version));
                    attachedRevits.Add(revit);
                }
            }

            return attachedRevits;
        }

        // managing clones ===========================================================================================
        // clones are git clones. pyRevit module likes to know about available clones to
        // perform operations on (switching engines, clones, uninstalling, ...)

        // register a clone in a configs
        // @handled @logs
        public static void RegisterClone(string cloneName, string repoPath) {
            var normalPath = repoPath.NormalizeAsPath();
            logger.Debug(string.Format("Registering clone \"{0}\"", normalPath));
            if (PyRevitClone.VerifyRepoValidity(repoPath)) {
                logger.Debug(string.Format("Clone is valid. Registering \"{0}\"", normalPath));
                var registeredClones = GetRegisteredClones();
                var clone = new PyRevitClone(cloneName, repoPath);
                if (!registeredClones.Contains(clone)) {
                    registeredClones.Add(new PyRevitClone(cloneName, repoPath));
                    SaveRegisteredClones(registeredClones);
                }
                else
                    throw new pyRevitException(
                        string.Format("clone with repo path \"{0}\" already exists.", repoPath)
                        );
            }
        }

        // renames a clone in a configs
        // @handled @logs
        public static void RenameClone(string cloneName, string newName) {
            logger.Debug(string.Format("Renaming clone \"{0}\" to \"{1}\"", cloneName, newName));
            var registeredClones = GetRegisteredClones();
            foreach (var clone in registeredClones)
                if (clone.Name == cloneName)
                    clone.Rename(newName);
            SaveRegisteredClones(registeredClones);
        }

        // unregister a clone from configs
        // @handled @logs
        public static void UnregisterClone(PyRevitClone clone) {
            logger.Debug(string.Format("Unregistering clone \"{0}\"", clone));

            // remove the clone path from list
            var clones = GetRegisteredClones();
            clones.Remove(clone);
            SaveRegisteredClones(clones);
        }

        // unregister all clone from configs
        // @handled @logs
        public static void UnregisterAllClones() {
            logger.Debug("Unregistering all clones...");

            foreach (var clone in GetRegisteredClones())
                UnregisterClone(clone);
        }

        // return list of registered clones
        // @handled @logs
        public static List<PyRevitClone> GetRegisteredClones() {
            var validatedClones = new List<PyRevitClone>();

            // safely get clone list
            var clonesList = GetKeyValueAsDict(PyRevitConsts.EnvConfigsSectionName,
                                               PyRevitConsts.EnvConfigsInstalledClonesKey,
                                               defaultValue: new List<string>());

            // verify all registered clones, protect against tampering
            foreach (var cloneKV in clonesList) {
                var clone = new PyRevitClone(cloneKV.Key, cloneKV.Value.NormalizeAsPath());
                if (clone.IsValidpyRevitRepo() && !validatedClones.Contains(clone)) {
                    logger.Debug(string.Format("Verified clone \"{0}={1}\"", clone.Name, clone.RepoPath));
                    validatedClones.Add(clone);
                }
            }

            // rewrite the verified clones list back to config file
            SaveRegisteredClones(validatedClones);

            return validatedClones;
        }

        // return requested registered clone
        // @handled @logs
        public static PyRevitClone GetRegisteredClone(string cloneNameOrRepoPath) {
            foreach (var clone in GetRegisteredClones())
                if (clone.Matches(cloneNameOrRepoPath))
                    return clone;

            throw new pyRevitException(string.Format("Can not find clone \"{0}\"", cloneNameOrRepoPath));
        }

        // managing extensions =======================================================================================
        private static bool CompareExtensionNames(string extName, string searchTerm) {
            var extMatcher = new Regex(searchTerm,
                                       RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            return extMatcher.IsMatch(extName);
        }

        // list registered extensions based on search pattern if provided, if not list all
        // @handled @logs
        public static List<PyRevitExtension> LookupRegisteredExtensions(string searchPattern = null) {
            List<PyRevitExtension> matchedExtensions = new List<PyRevitExtension>();

            // attemp to find the extension in default ext file
            try {
                matchedExtensions = 
                    LookupExtensionInDefinitionFile(GetDefaultExtensionLookupSource(),
                                                    searchPattern);
            }
            catch (Exception ex) {
                logger.Error(
                    string.Format(
                        "Error looking up extension with pattern \"{0}\" in default extension source."
                        + " | {1}", searchPattern, ex.Message)
                        );
            }

            // if not found in downloaded file or downlod failed, try the additional sources
            if (matchedExtensions.Count == 0 )
                foreach(var extLookupSrc in GetRegisteredExtensionLookupSources()) {
                    try {
                        // attemp to find the extension in ext lookup source
                        matchedExtensions = LookupExtensionInDefinitionFile(extLookupSrc, searchPattern);
                        if (matchedExtensions.Count > 0)
                            return matchedExtensions;
                    }
                    catch (Exception ex) {
                        logger.Error(
                            string.Format(
                                "Error looking up extension with pattern \"{0}\" in extension lookup source \"{1}\""
                                + " | {2}" , searchPattern, extLookupSrc, ex.Message)
                                );
                    }
                }

            // return empty results since nothing has been found and no exception has occured
            return matchedExtensions;
        }

        // return a list of installed extensions found under registered search paths
        // @handled @logs
        public static List<PyRevitExtension> GetInstalledExtensions(string searchPath = null) {
            List<string> searchPaths;
            if (searchPath == null)
                searchPaths = GetRegisteredExtensionSearchPaths();
            else
                searchPaths = new List<string>() { searchPath };

            var installedExtensions = new List<PyRevitExtension>();
            foreach (var path in searchPaths) {
                logger.Debug(string.Format("Looking for installed extensions under \"{0}\"...", path));
                foreach (var subdir in Directory.GetDirectories(path)) {
                    if (PyRevitExtension.IsExtensionDirectory(subdir)) {
                        logger.Debug(string.Format("Found installed extension \"{0}\"...", subdir));
                        installedExtensions.Add(new PyRevitExtension(subdir));
                    }
                }
            }

            return installedExtensions;
        }

        // find extension installed under registered search paths
        // @handled @logs
        public static PyRevitExtension GetInstalledExtension(string extensionName) {
            logger.Debug(string.Format("Looking up installed extension \"{0}\"...", extensionName));
            foreach (var ext in GetInstalledExtensions())
                if (CompareExtensionNames(ext.Name, extensionName)) {
                    logger.Debug(string.Format("\"{0}\" Matched installed extension \"{1}\"",
                                               extensionName, ext.Name));
                    return ext;
                }

            logger.Debug(string.Format("Installed extension \"{0}\" not found.", extensionName));
            return null;
        }

        // lookup registered extension by name
        // @handled @logs
        public static PyRevitExtension FindExtension(string extensionName) {
            logger.Debug(string.Format("Looking up registered extension \"{0}\"...", extensionName));
            var matchingExts = LookupRegisteredExtensions(extensionName);
            if (matchingExts.Count == 0) {
                return GetInstalledExtension(extensionName);
            }
            else if (matchingExts.Count == 1) {
                logger.Debug(string.Format("Extension found \"{0}\"...", matchingExts[0].Name));
                return matchingExts[0];
            }
            else if (matchingExts.Count > 1)
                Errors.LatestError = ErrorCodes.MoreThanOneItemMatched;

            return null;
        }

        // installs extension from repo url
        // @handled @logs
        public static void InstallExtension(string extensionName, PyRevitExtensionTypes extensionType,
                                            string repoPath, string destPath, string branchName) {
            // make sure extension is not installed already
            var existExt = GetInstalledExtension(extensionName);
            if (existExt != null)
                throw new pyRevitException(string.Format("Extension \"{0}\" is already installed under \"{1}\"",
                                                         existExt.Name, existExt.InstallPath));

            // determine repo folder name
            // Name.extension for UI Extensions
            // Name.lib for Library Extensions
            string extDestDirName = PyRevitExtension.MakeConfigName(extensionName, extensionType);
            string finalExtRepoPath = Path.Combine(destPath, extDestDirName).NormalizeAsPath();

            // determine branch name
            branchName = branchName ?? PyRevitConsts.ExtensionRepoMainBranch;

            logger.Debug(string.Format("Extension branch name determined as \"{0}\"", branchName));
            logger.Debug(string.Format("Installing extension into \"{0}\"", finalExtRepoPath));

            // start the clone process
            var repo = GitInstaller.Clone(repoPath, branchName, finalExtRepoPath);

            // Check installation
            if (repo != null) {
                // make sure to delete the repo if error occured after cloning
                var clonedPath = repo.Info.WorkingDirectory;
                if (GitInstaller.IsValidRepo(clonedPath)) {
                    logger.Debug(string.Format("Clone successful \"{0}\"", clonedPath));
                    RegisterExtensionSearchPath(destPath.NormalizeAsPath());
                }
                else {
                    logger.Debug(string.Format("Invalid repo after cloning. Deleting clone \"{0}\"", repoPath));
                    try {
                        CommonUtils.DeleteDirectory(repoPath);
                    }
                    catch (Exception delEx) {
                        logger.Error(string.Format("Error post-install cleanup on \"{0}\" | {1}",
                                                   repoPath, delEx.Message));
                    }
                }
            }
            else
                throw new pyRevitException(string.Format("Error installing extension. Null repo error on \"{0}\"",
                                                         repoPath));

        }

        // installs extension
        // @handled @logs
        public static void InstallExtension(PyRevitExtension ext, string destPath, string branchName) {
            logger.Debug(string.Format("Installing extension \"{0}\"", ext.Name));
            if (CommonUtils.VerifyPath(destPath)) {
                InstallExtension(ext.Name, ext.Type, ext.Url, destPath, branchName);
            }
            else
                throw new pyRevitResourceMissingException(destPath);
        }

        // uninstalls an extension by repo
        // @handled @logs
        public static void RemoveExtension(string repoPath, bool removeSearchPath = false) {
            if (repoPath != null) {
                logger.Debug(string.Format("Uninstalling extension at \"{0}\"", repoPath));
                CommonUtils.DeleteDirectory(repoPath);
                // remove search path if requested
                if (removeSearchPath)
                    UnregisterExtensionSearchPath(Path.GetDirectoryName(Path.GetDirectoryName(repoPath)));
            }
            else
                throw new pyRevitResourceMissingException(repoPath);
        }

        // uninstalls an extension
        // @handled @logs
        public static void UninstallExtension(PyRevitExtension ext, bool removeSearchPath = false) {
            RemoveExtension(ext.InstallPath, removeSearchPath: removeSearchPath);
        }

        // uninstalls an extension by name
        // @handled @logs
        public static void UninstallExtension(string extensionName, bool removeSearchPath = false) {
            logger.Debug(string.Format("Uninstalling extension \"{0}\"", extensionName));
            var ext = GetInstalledExtension(extensionName);
            if (ext != null)
                RemoveExtension(ext.InstallPath, removeSearchPath: removeSearchPath);
            else
                throw new pyRevitException(string.Format("Can not find extension \"{0}\"", extensionName));
        }

        // force update all extensions
        // @handled @logs
        public static void UpdateAllInstalledExtensions() {
            logger.Debug("Updating all installed extensions.");
            // update all installed extensions
            foreach (var ext in GetInstalledExtensions())
                ext.Update();
        }

        // enable extension in config
        // @handled @logs
        private static void ToggleExtension(string extName, bool state) {
            var ext = FindExtension(extName);
            if (ext != null) {
                logger.Debug(string.Format("{0} extension \"{1}\"", state ? "Enabling" : "Disabling", ext.Name));
                SetKeyValue(ext.ConfigName, PyRevitConsts.ExtensionJsonDisabledKey, !state);
            }
            else
                throw new pyRevitException(
                    string.Format("Can not find extension or more than one extension matches \"{0}\"", extName));
        }

        // disable extension in config
        // @handled @logs
        public static void EnableExtension(string extName) {
            ToggleExtension(extName, true);
        }

        // disable extension in config
        // @handled @logs
        public static void DisableExtension(string extName) {
            ToggleExtension(extName, false);
        }

        // get list of registered extension search paths
        // @handled @logs
        public static List<string> GetRegisteredExtensionSearchPaths() {
            var validatedPaths = new List<string>();
            var searchPaths = GetKeyValueAsList(PyRevitConsts.ConfigsCoreSection,
                                                PyRevitConsts.ConfigsUserExtensionsKey);
            // make sure paths exist
            foreach (var path in searchPaths) {
                var normPath = path.NormalizeAsPath();
                if (CommonUtils.VerifyPath(path) && !validatedPaths.Contains(normPath)) {
                    logger.Debug(string.Format("Verified extension search path \"{0}\"", normPath));
                    validatedPaths.Add(normPath);
                }
            }

            // rewrite verified list
            SetKeyValue(PyRevitConsts.ConfigsCoreSection,
                        PyRevitConsts.ConfigsUserExtensionsKey,
                        validatedPaths);

            return validatedPaths;
        }

        // add extension search path
        // @handled @logs
        public static void RegisterExtensionSearchPath(string searchPath) {
            if (CommonUtils.VerifyPath(searchPath)) {
                logger.Debug(string.Format("Adding extension search path \"{0}\"", searchPath));
                var searchPaths = GetRegisteredExtensionSearchPaths();
                searchPaths.Add(searchPath.NormalizeAsPath());
                SetKeyValue(PyRevitConsts.ConfigsCoreSection,
                            PyRevitConsts.ConfigsUserExtensionsKey,
                            searchPaths);
            }
            else
                throw new pyRevitResourceMissingException(searchPath);
        }

        // remove extension search path
        // @handled @logs
        public static void UnregisterExtensionSearchPath(string searchPath) {
            var normPath = searchPath.NormalizeAsPath();
            logger.Debug(string.Format("Removing extension search path \"{0}\"", normPath));
            var searchPaths = GetRegisteredExtensionSearchPaths();
            searchPaths.Remove(normPath);
            SetKeyValue(PyRevitConsts.ConfigsCoreSection,
                        PyRevitConsts.ConfigsUserExtensionsKey,
                        searchPaths);
        }

        // managing extension sources ================================================================================
        // get default extension lookup source
        // @handled @logs
        public static string GetDefaultExtensionLookupSource() {
            return PyRevitConsts.ExtensionsDefinitionFileUri;
        }

        // get extension lookup sources
        // @handled @logs
        public static List<string> GetRegisteredExtensionLookupSources() {
            var sources = GetKeyValueAsList(PyRevitConsts.EnvConfigsSectionName,
                                             PyRevitConsts.EnvConfigsExtensionLookupSourcesKey);
            var normSources = new List<string>();
            foreach (var src in sources) {
                var normSrc = src.NormalizeAsPath();
                logger.Debug(string.Format("Extension lookup source \"{0}\"", normSrc));
                normSources.Add(normSrc);
            }
            return normSources;
        }

        // register new extension lookup source
        // @handled @logs
        public static void RegisterExtensionLookupSource(string extLookupSource) {
            var normSource = extLookupSource.NormalizeAsPath();
            var sources = GetRegisteredExtensionLookupSources();
            if (!sources.Contains(normSource)) {
                logger.Debug(string.Format("Registering extension lookup source \"{0}\"", normSource));
                sources.Add(normSource);
                SaveExtensionLookupSources(sources);
            }
            else
                logger.Debug("Extension lookup source already exists. Skipping registration.");
        }

        // unregister extension lookup source
        // @handled @logs
        public static void UnregisterExtensionLookupSource(string extLookupSource) {
            var normSource = extLookupSource.NormalizeAsPath();
            var sources = GetRegisteredExtensionLookupSources();
            if (sources.Contains(normSource)) {
                logger.Debug(string.Format("Unregistering extension lookup source \"{0}\"", normSource));
                sources.Remove(normSource);
                SaveExtensionLookupSources(sources);
            }
            else
                logger.Debug("Extension lookup source does not exist. Skipping unregistration.");
        }

        // unregister all extension lookup sources
        // @handled @logs
        public static void UnregisterAllExtensionLookupSources() {
            foreach (var src in GetRegisteredExtensionLookupSources())
                UnregisterExtensionLookupSource(src);
        }

        // managing init templates ===================================================================================
        public static void GetInitTemplate(PyRevitExtensionTypes extType) {

        }

        public static void GetInitTemplate(PyRevitBundleTypes extType) {

        }

        public static void InitExtension(PyRevitExtensionTypes extType, string destPath) {

        }

        public static void InitBundle(PyRevitBundleTypes bundleType, string destPath) {

        }

        public static void AddInitTemplatePath(string templatesPath) {

        }

        public static void RemoveInitTemplatePath(string templatesPath) {

        }

        public static List<string> GetInitTemplatePaths() {
            return new List<string>();
        }

        // managing configs ==========================================================================================
        // pyrevit config getter/setter
        // usage logging config
        // @handled @logs
        public static string FindConfigFileInDirectory(string sourcePath) {
            var configMatcher = new Regex(PyRevitConsts.ConfigsFileRegexPattern, RegexOptions.IgnoreCase);
            if (CommonUtils.VerifyPath(sourcePath))
                foreach (string subFile in Directory.GetFiles(sourcePath))
                    if (configMatcher.IsMatch(Path.GetFileName(subFile)))
                        return subFile;
            return null;
        }

        public static bool GetUsageReporting() {
            return bool.Parse(GetKeyValue(PyRevitConsts.ConfigsUsageLoggingSection,
                                          PyRevitConsts.ConfigsUsageLoggingStatusKey));
        }

        public static string GetUsageLogFilePath() {
            return GetKeyValue(PyRevitConsts.ConfigsUsageLoggingSection,
                               PyRevitConsts.ConfigsUsageLogFilePathKey);
        }

        public static string GetUsageLogServerUrl() {
            return GetKeyValue(PyRevitConsts.ConfigsUsageLoggingSection,
                               PyRevitConsts.ConfigsUsageLogServerUrlKey);
        }

        public static void EnableUsageReporting(string logFilePath = null, string logServerUrl = null) {
            logger.Debug(string.Format("Enabling usage logging... path: \"{0}\" server: {1}",
                                       logFilePath, logServerUrl));
            SetKeyValue(PyRevitConsts.ConfigsUsageLoggingSection,
                        PyRevitConsts.ConfigsUsageLoggingStatusKey,
                        true);

            if (logFilePath != null)
                if (CommonUtils.VerifyPath(logFilePath))
                    SetKeyValue(PyRevitConsts.ConfigsUsageLoggingSection,
                                PyRevitConsts.ConfigsUsageLogFilePathKey,
                                logFilePath);
                else
                    logger.Debug(string.Format("Invalid log path \"{0}\"", logFilePath));

            if (logServerUrl != null)
                SetKeyValue(PyRevitConsts.ConfigsUsageLoggingSection,
                            PyRevitConsts.ConfigsUsageLogServerUrlKey,
                            logServerUrl);
        }

        public static void DisableUsageReporting() {
            logger.Debug("Disabling usage reporting...");
            SetKeyValue(PyRevitConsts.ConfigsUsageLoggingSection,
                        PyRevitConsts.ConfigsUsageLoggingStatusKey,
                        false);
        }

        // update checking config
        // @handled @logs
        public static bool GetCheckUpdates() {
            return bool.Parse(GetKeyValue(PyRevitConsts.ConfigsCoreSection,
                                             PyRevitConsts.ConfigsCheckUpdatesKey));
        }

        public static void SetCheckUpdates(bool state) {
            SetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsCheckUpdatesKey, state);
        }

        // auto update config
        // @handled @logs
        public static bool GetAutoUpdate() {
            return bool.Parse(GetKeyValue(PyRevitConsts.ConfigsCoreSection,
                                          PyRevitConsts.ConfigsAutoUpdateKey));
        }

        public static void SetAutoUpdate(bool state) {
            SetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsAutoUpdateKey, state);
        }

        // rocket mode config
        // @handled @logs
        public static bool GetRocketMode() {
            return bool.Parse(GetKeyValue(PyRevitConsts.ConfigsCoreSection,
                                          PyRevitConsts.ConfigsRocketModeKey));
        }

        public static void SetRocketMode(bool state) {
            SetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsRocketModeKey, state);
        }

        // logging level config
        // @handled @logs
        public static PyRevitLogLevels GetLoggingLevel() {
            bool verbose = bool.Parse(GetKeyValue(PyRevitConsts.ConfigsCoreSection,
                                                  PyRevitConsts.ConfigsVerboseKey));
            bool debug = bool.Parse(GetKeyValue(PyRevitConsts.ConfigsCoreSection,
                                                PyRevitConsts.ConfigsDebugKey));

            if (verbose && !debug)
                return PyRevitLogLevels.Verbose;
            else if (debug)
                return PyRevitLogLevels.Debug;

            return PyRevitLogLevels.None;
        }

        public static void SetLoggingLevel(PyRevitLogLevels level) {
            if (level == PyRevitLogLevels.None) {
                SetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsVerboseKey, false);
                SetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsDebugKey, false);
            }

            if (level == PyRevitLogLevels.Verbose) {
                SetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsVerboseKey, true);
                SetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsDebugKey, false);
            }

            if (level == PyRevitLogLevels.Debug) {
                SetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsVerboseKey, true);
                SetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsDebugKey, true);
            }
        }

        // file logging config
        // @handled @logs
        public static bool GetFileLogging() {
            return bool.Parse(GetKeyValue(PyRevitConsts.ConfigsCoreSection,
                                          PyRevitConsts.ConfigsFileLoggingKey));
        }

        public static void SetFileLogging(bool state) {
            SetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsFileLoggingKey, state);
        }

        // load beta config
        // @handled @logs
        public static bool GetLoadBetaTools() {
            return bool.Parse(GetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsLoadBetaKey));
        }

        public static void SetLoadBetaTools(bool state) {
            SetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsLoadBetaKey, state);
        }

        // output style sheet config
        // @handled @logs
        public static string GetOutputStyleSheet() {
            return GetKeyValue(PyRevitConsts.ConfigsCoreSection, PyRevitConsts.ConfigsOutputStyleSheet);
        }

        public static void SetOutputStyleSheet(string outputCSSFilePath) {
            if (File.Exists(outputCSSFilePath))
                SetKeyValue(PyRevitConsts.ConfigsCoreSection,
                            PyRevitConsts.ConfigsOutputStyleSheet,
                            outputCSSFilePath);
        }

        // generic configuration public access  ======================================================================
        // @handled @logs
        public static string GetConfig(string sectionName, string keyName) {
            return GetKeyValue(sectionName, keyName);
        }

        // @handled @logs
        public static void SetConfig(string sectionName, string keyName, bool boolValue) {
            SetKeyValue(sectionName, keyName, boolValue);
        }

        // @handled @logs
        public static void SetConfig(string sectionName, string keyName, int intValue) {
            SetKeyValue(sectionName, keyName, intValue);
        }

        // @handled @logs
        public static void SetConfig(string sectionName, string keyName, string stringValue) {
            SetKeyValue(sectionName, keyName, stringValue);
        }

        // @handled @logs
        public static void SetConfig(string sectionName, string keyName, IEnumerable<string> stringListValue) {
            SetKeyValue(sectionName, keyName, stringListValue);
        }

        // copy config file into all users directory as seed config file
        // @handled @logs
        public static void SeedConfig(bool makeCurrentUserAsOwner = false, string setupFromTemplate = null) {
            // if setupFromTemplate is not specified: copy current config into Allusers folder
            // if setupFromTemplate is specified: copy setupFromTemplate as the main config
            string sourceFile = setupFromTemplate != null ? setupFromTemplate : pyRevitConfigFilePath;
            string targetFile = setupFromTemplate != null ? pyRevitConfigFilePath : pyRevitSeedConfigFilePath;

            logger.Debug(string.Format("Seeding config file \"{0}\" to \"{1}\"", sourceFile, targetFile));

            try {
                if (File.Exists(sourceFile)) {
                    CommonUtils.ConfirmFile(targetFile);
                    File.Copy(sourceFile, targetFile, true);

                    if (makeCurrentUserAsOwner) {
                        var fs = File.GetAccessControl(targetFile);
                        var currentUser = WindowsIdentity.GetCurrent();
                        try {
                            CommonUtils.SetFileSecurity(targetFile, currentUser.Name);
                        }
                        catch (InvalidOperationException ex) {
                            logger.Error(
                                string.Format(
                                    "You cannot assign ownership to user \"{0}\"." +
                                    "Either you don't have TakeOwnership permissions, " +
                                    "or it is not your user account. | {1}", currentUser.Name, ex.Message
                                    )
                            );
                        }
                    }
                }
            }
            catch (Exception ex) {
                throw new pyRevitException(string.Format("Failed seeding config file. | {0}", ex.Message));
            }
        }

        // configurations private access methods  ====================================================================
        private static IniFile GetConfigFile() {
            // INI formatting
            var cfgOps = new IniOptions();
            cfgOps.KeySpaceAroundDelimiter = true;
            IniFile cfgFile = new IniFile(cfgOps);

            // default to current user config
            string configFile = pyRevitConfigFilePath;
            // make sure the file exists and if not create an empty one
            CommonUtils.ConfirmFile(configFile);

            // load the config file
            cfgFile.Load(configFile);
            return cfgFile;
        }

        // save config file to standard location
        // @handled @logs
        private static void SaveConfigFile(IniFile cfgFile) {
            logger.Debug(string.Format("Saving config file \"{0}\"", pyRevitConfigFilePath));
            try {
                cfgFile.Save(pyRevitConfigFilePath);
            }
            catch (Exception ex) {
                throw new pyRevitException(string.Format("Failed to save config to \"{0}\". | {1}",
                                                         pyRevitConfigFilePath, ex.Message));
            }
        }

        // get config key value
        // @handled @logs
        private static string GetKeyValue(string sectionName,
                                          string keyName,
                                          string defaultValue = null,
                                          bool throwNotSetException = true) {
            var c = GetConfigFile();
            logger.Debug(string.Format("Try getting config \"{0}:{1}\" ?? {2}",
                                       sectionName, keyName, defaultValue ?? "NULL"));
            if (c.Sections.Contains(sectionName) && c.Sections[sectionName].Keys.Contains(keyName))
                return c.Sections[sectionName].Keys[keyName].Value as string;
            else {
                if (defaultValue == null && throwNotSetException)
                    throw new pyRevitConfigValueNotSet(sectionName, keyName);
                else {
                    logger.Debug(string.Format("Config is not set. Returning default value \"{0}\"",
                                               defaultValue ?? "NULL"));
                    return defaultValue;
                }
            }
        }

        // get config key value and make a string list out of it
        // @handled @logs
        private static List<string> GetKeyValueAsList(string sectionName,
                                                      string keyName,
                                                      IEnumerable<string> defaultValue = null,
                                                      bool throwNotSetException = true) {
            logger.Debug(string.Format("Try getting config as list \"{0}:{1}\"", sectionName, keyName));
            var stringValue = GetKeyValue(sectionName, keyName, "", throwNotSetException: throwNotSetException);
            return stringValue.ConvertFromTomlListString();
        }

        // get config key value and make a string dictionary out of it
        // @handled @logs
        private static Dictionary<string, string> GetKeyValueAsDict(string sectionName,
                                                                    string keyName,
                                                                    IEnumerable<string> defaultValue = null,
                                                                    bool throwNotSetException = true) {
            logger.Debug(string.Format("Try getting config as dict \"{0}:{1}\"", sectionName, keyName));
            var stringValue = GetKeyValue(sectionName, keyName, "", throwNotSetException: throwNotSetException);
            return stringValue.ConvertFromTomlDictString();
        }

        // updates config key value, creates the config if not set yet
        // @handled @logs
        private static void UpdateKeyValue(string sectionName, string keyName, string stringValue) {
            if (stringValue != null) {
                var c = GetConfigFile();

                if (!c.Sections.Contains(sectionName)) {
                    logger.Debug(string.Format("Adding config section \"{0}\"", sectionName));
                    c.Sections.Add(sectionName);
                }

                if (!c.Sections[sectionName].Keys.Contains(keyName)) {
                    logger.Debug(string.Format("Adding config key \"{0}:{1}\"", sectionName, keyName));
                    c.Sections[sectionName].Keys.Add(keyName);
                }

                logger.Debug(string.Format("Updating config \"{0}:{1} = {2}\"", sectionName, keyName, stringValue));
                c.Sections[sectionName].Keys[keyName].Value = stringValue;

                SaveConfigFile(c);
            }
            else
                logger.Debug(string.Format("Can not set null value for \"{0}:{1}\"", sectionName, keyName));
        }

        // sets config key value as bool
        // @handled @logs
        private static void SetKeyValue(string sectionName, string keyName, bool boolVaue) {
            UpdateKeyValue(sectionName, keyName, boolVaue.ToString());
        }

        // sets config key value as int
        // @handled @logs
        private static void SetKeyValue(string sectionName, string keyName, int intValue) {
            UpdateKeyValue(sectionName, keyName, intValue.ToString());
        }

        // sets config key value as string
        // @handled @logs
        private static void SetKeyValue(string sectionName, string keyName, string stringValue) {
            UpdateKeyValue(sectionName, keyName, stringValue);
        }

        // sets config key value as string list
        // @handled @logs
        private static void SetKeyValue(string sectionName, string keyName, IEnumerable<string> listString) {
            UpdateKeyValue(sectionName, keyName, listString.ConvertToTomlListString());
        }

        // sets config key value as string dictionary
        // @handled @logs
        private static void SetKeyValue(string sectionName, string keyName, IDictionary<string, string> dictString) {
            UpdateKeyValue(sectionName, keyName, dictString.ConvertToTomlDictString());
        }

        // updates the config value for registered clones
        // @handled @logs
        private static void SaveRegisteredClones(IEnumerable<PyRevitClone> clonesList) {
            var newValueDic = new Dictionary<string, string>();
            foreach (var clone in clonesList)
                newValueDic[clone.Name] = clone.RepoPath;

            SetKeyValue(PyRevitConsts.EnvConfigsSectionName,
                        PyRevitConsts.EnvConfigsInstalledClonesKey,
                        newValueDic);
        }

        // updates the config value for extensions lookup sources
        // @handled @logs
        private static void SaveExtensionLookupSources(IEnumerable<string> sourcesList) {
            SetKeyValue(PyRevitConsts.EnvConfigsSectionName,
                        PyRevitConsts.EnvConfigsExtensionLookupSourcesKey,
                        sourcesList);
        }

        // other private helprs  =====================================================================================
        // finds the engine path for given repo based on repo version and engine location
        // @handled @logs
        private static string GetEnginePath(string repoPath, int engineVer = 000) {
            logger.Debug(string.Format("Finding engine \"{0}\" path for \"{1}\"", engineVer, repoPath));

            // determine repo version based on directory availability
            string enginesDir = Path.Combine(repoPath, "bin", "engines");
            if (!CommonUtils.VerifyPath(enginesDir)) {
                enginesDir = Path.Combine(repoPath, "pyrevitlib", "pyrevit", "loader", "addin");
                if (!CommonUtils.VerifyPath(enginesDir))
                    throw new pyRevitInvalidGitCloneException(repoPath);
            }

            // now determine engine path; latest or requested
            if (engineVer == 000) {
                enginesDir = FindLatestEngine(enginesDir);
            }
            else {
                string fullEnginePath = Path.Combine(enginesDir,
                                                     engineVer.ToString(),
                                                     PyRevitConsts.DllName).NormalizeAsPath();
                if (File.Exists(fullEnginePath))
                    enginesDir = fullEnginePath;
                else
                    throw new pyRevitException(
                        string.Format("Engine \"{0}\" is not available at \"{1}\"", engineVer, fullEnginePath)
                        );
            }

            logger.Debug(string.Format("Determined engine path \"{0}\"", enginesDir ?? "NULL"));
            return enginesDir;
        }

        // find latest engine path
        // @handled @logs
        private static string FindLatestEngine(string enginesDir) {
            // engines are stored in directory named XXX based on engine version (e.g. 273)
            var engineFinder = new Regex(@"\d\d\d");
            int latestEnginerVer = 000;
            string latestEnginePath = null;

            if (CommonUtils.VerifyPath(enginesDir)) {
                foreach (string subDir in Directory.GetDirectories(enginesDir)) {
                    var engineDir = Path.GetFileName(subDir);
                    if (engineFinder.IsMatch(engineDir)) {
                        var engineVer = int.Parse(engineDir);
                        if (engineVer > latestEnginerVer)
                            latestEnginerVer = engineVer;
                    }
                }

                string fullEnginePath = Path.Combine(enginesDir,
                                                     latestEnginerVer.ToString(),
                                                     PyRevitConsts.DllName).NormalizeAsPath();
                if (File.Exists(fullEnginePath))
                    latestEnginePath = fullEnginePath;
            }
            else
                throw new pyRevitResourceMissingException(enginesDir);

            logger.Debug(string.Format("Latest engine path \"{0}\"", latestEnginePath ?? "NULL"));
            return latestEnginePath;
        }

        // find extension with search patten in extension lookup resource (file or url to a remote file)
        // @handled @logs
        private static List<PyRevitExtension> LookupExtensionInDefinitionFile(
                string fileOrUri,
                string searchPattern = null) {
            var pyrevtExts = new List<PyRevitExtension>();
            string filePath = null;

            // determine if path is file or uri
            logger.Debug(string.Format("Determining file or remote source \"{0}\"", fileOrUri));
            Uri uriResult;
            var validPath = Uri.TryCreate(fileOrUri, UriKind.Absolute, out uriResult);
            if (validPath) {
                if (uriResult.IsFile) {
                    filePath = fileOrUri;
                    logger.Debug(string.Format("Source is a file \"{0}\"", filePath));
                }
                else if (uriResult.HostNameType == UriHostNameType.Dns
                            || uriResult.HostNameType == UriHostNameType.IPv4
                            || uriResult.HostNameType == UriHostNameType.IPv6) {

                    logger.Debug(string.Format("Source is a remote resource \"{0}\"", fileOrUri));
                    logger.Debug(string.Format("Downloading remote resource \"{0}\"...", fileOrUri));
                    // download the resource into TEMP
                    try {
                        filePath = 
                            CommonUtils.DownloadFile(fileOrUri,
                                                     Path.Combine(Environment.GetEnvironmentVariable("TEMP"),
                                                                  PyRevitConsts.EnvConfigsExtensionDBFileName)
                            );
                    }
                    catch (Exception ex) {
                        throw new pyRevitException(
                            string.Format("Error downloading extension metadata file. | {0}", ex.Message)
                            );
                    }
                }
            }
            else
                throw new pyRevitException(
                    string.Format("Source is not a valid file or remote resource \"{0}\"", fileOrUri)
                    );

            // process file now
            if (filePath != null) {
                if (Path.GetExtension(filePath).ToLower() == ".json") {
                    logger.Debug("Parsing extension metadata file...");
                    dynamic extensionsObj;
                    if (filePath != null) {
                        try {
                            extensionsObj = JObject.Parse(File.ReadAllText(filePath));
                        }
                        catch (Exception ex) {
                            throw new pyRevitException(string.Format("Error parsing extension metadata. | {0}", ex.Message));
                        }

                        // make extension list
                        foreach (JObject extObj in extensionsObj.extensions) {
                            var ext = new PyRevitExtension(extObj);
                            logger.Debug(string.Format("Registered extension \"{0}\"", ext.Name));
                            if (searchPattern != null) {
                                if (CompareExtensionNames(ext.Name, searchPattern)) {
                                    logger.Debug(string.Format("\"{0}\" Matched registered extension \"{1}\"",
                                                               searchPattern, ext.Name));
                                    pyrevtExts.Add(ext);
                                }
                            }
                            else
                                pyrevtExts.Add(ext);
                        }
                    }
                }
                else
                    throw new pyRevitException(
                        string.Format("Definition file is not a valid json file \"{0}\"", filePath)
                        );
            }

            return pyrevtExts;
        }
    }
}
