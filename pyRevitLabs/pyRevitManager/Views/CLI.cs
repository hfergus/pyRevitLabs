﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Drawing;

using pyRevitLabs.Common;
using pyRevitLabs.Common.Extensions;
using pyRevitLabs.TargetApps.Revit;
using pyRevitLabs.Language.Properties;

using DocoptNet;
using NLog;
using NLog.Config;
using NLog.Targets;

using Console = Colorful.Console;

namespace pyRevitManager.Views {
    public enum pyRevitManagerLogLevel {
        Quiet,
        InfoMessages,
        Debug,
    }

    class pyRevitCLI {
        private static Logger logger = null;

        private const string helpUrl = "https://github.com/eirannejad/pyRevitLabs/blob/master/README_CLI.md";
        private const string usage = @"pyrevit command line tool

    Usage:
        pyrevit help
        pyrevit (-h | --help)
        pyrevit (-V | --version)
        pyrevit (blog | docs | source | youtube | support)
        pyrevit env
        pyrevit clone <clone_name> [<dest_path>] [--branch=<branch_name>] [--deploy=<deployment_name>] [--nogit]
        pyrevit clone <clone_name> <repo_or_archive_url> <dest_path> [--branch=<branch_name>] [--deploy=<deployment_name>] [--nogit]
        pyrevit clones
        pyrevit clones (info | open) <clone_name>
        pyrevit clones add <clone_name> <clone_path>
        pyrevit clones forget (--all | <clone_name>)
        pyrevit clones rename <clone_name> <clone_new_name>
        pyrevit clones delete [(--all | <clone_name>)] [--clearconfigs]
        pyrevit clones branch <clone_name> [<branch_name>]
        pyrevit clones version <clone_name> [<tag_name>]
        pyrevit clones commit <clone_name> [<commit_hash>]
        pyrevit clones update (--all | <clone_name>)
        pyrevit clones deployments <clone_name>
        pyrevit attach <clone_name> (latest | dynamosafe | <engine_version>) (<revit_year> | --all | --attached) [--allusers]
        pyrevit attached
        pyrevit detach (--all | <revit_year>)
        pyrevit extend <extension_name> <dest_path> [--branch=<branch_name>]
        pyrevit extend (ui | lib) <extension_name> <repo_url> <dest_path> [--branch=<branch_name>]
        pyrevit extensions
        pyrevit extensions search <search_pattern>
        pyrevit extensions (info | help | open) <extension_name>
        pyrevit extensions delete <extension_name>
        pyrevit extensions paths
        pyrevit extensions paths forget --all
        pyrevit extensions paths (add | forget) <extensions_path>
        pyrevit extensions (enable | disable) <extension_name>
        pyrevit extensions sources
        pyrevit extensions sources forget --all
        pyrevit extensions sources (add | forget) <source_json_or_url>
        pyrevit extensions update (--all | <extension_name>)
        pyrevit revits [--installed]
        pyrevit revits killall [<revit_year>]
        pyrevit revits fileinfo <file_or_dir_path> [--csv=<output_file>]
        pyrevit revits addons
        pyrevit revits addons install <addon_name> <dest_path> [--allusers]
        pyrevit revits addons uninstall <addon_name>
        pyrevit init (ui | lib) <extension_name>
        pyrevit init (tab | panel | pull | split | push | smart | command) <bundle_name>
        pyrevit init templates
        pyrevit init templates (add | forget) <init_templates_path>
        pyrevit caches clear (--all | <revit_year>)
        pyrevit config <template_config_path>
        pyrevit configs logs [(none | verbose | debug)]
        pyrevit configs allowremotedll [(enable | disable)]
        pyrevit configs checkupdates [(enable | disable)]
        pyrevit configs autoupdate [(enable | disable)]
        pyrevit configs rocketmode [(enable | disable)]
        pyrevit configs filelogging [(enable | disable)]
        pyrevit configs loadbeta [(enable | disable)]
        pyrevit configs usagelogging
        pyrevit configs usagelogging enable (file | server) <dest_path>
        pyrevit configs usagelogging disable
        pyrevit configs outputcss [<css_path>]
        pyrevit configs seed [--lock]
        pyrevit configs <option_path> (enable | disable)
        pyrevit configs <option_path> [<option_value>]
        

    Options:
        -h --help                   Show this screen.
        -V --version                Show version.
        --verbose                   Print info messages.
        --debug                     Print docopt options and logger debug messages.
        --core                      Install original pyRevit core only (no defualt tools).
        --all                       All applicable items.
        --attached                  All Revits that are configured to load pyRevit.
        --authgroup=<auth_groups>   User groups authorized to use the extension.
        --branch=<branch_name>      Target git branch name.
";

        public static void ProcessArguments(string[] args) {
            // process arguments for hidden debug mode switch
            pyRevitManagerLogLevel logLevel = pyRevitManagerLogLevel.InfoMessages;

            // setup logger
            var config = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget("target1") {
                Layout = @"${level}: ${message} ${exception}"
            };
            config.AddTarget(consoleTarget);
            config.AddRuleForAllLevels(consoleTarget);
            LogManager.Configuration = config;
            // disable debug by default
            foreach (var rule in LogManager.Configuration.LoggingRules) {
                rule.DisableLoggingForLevel(LogLevel.Info);
                rule.DisableLoggingForLevel(LogLevel.Debug);
            }

            // process arguments for logging level
            var argsList = new List<string>(args);

            if (argsList.Contains("--test")) {
                argsList.Remove("--test");
                GlobalConfigs.UnderTest = true;
            }

            if (argsList.Contains("--verbose")) {
                argsList.Remove("--verbose");
                logLevel = pyRevitManagerLogLevel.InfoMessages;
                foreach (var rule in LogManager.Configuration.LoggingRules)
                    rule.EnableLoggingForLevel(LogLevel.Info);
            }

            if (argsList.Contains("--debug")) {
                argsList.Remove("--debug");
                logLevel = pyRevitManagerLogLevel.Debug;
                foreach (var rule in LogManager.Configuration.LoggingRules)
                    rule.EnableLoggingForLevel(LogLevel.Debug);
            }

            // process docopt
            var arguments = new Docopt().Apply(
                usage,
                argsList,
                version: string.Format(StringLib.ConsoleVersionFormat,
                                       Assembly.GetExecutingAssembly().GetName().Version.ToString()),
                exit: true,
                help: true
            );

            // print active arguments in debug mode
            if (logLevel == pyRevitManagerLogLevel.Debug)
                foreach (var argument in arguments.OrderBy(x => x.Key)) {
                    if (argument.Value != null && (argument.Value.IsTrue || argument.Value.IsString))
                        Console.WriteLine("{0} = {1}", argument.Key, argument.Value);
                }

            // get logger
            logger = LogManager.GetCurrentClassLogger();
            // get active keys for safe command extraction
            var activeKeys = ExtractEnabledKeywords(arguments);

            // now call methods based on inputs
            try {
                ExecuteCommand(arguments, activeKeys);
            }
            catch (Exception ex) {
                LogException(ex, logLevel);
            }

            ProcessErrorCodes();
        }


        private static void ExecuteCommand(IDictionary<string, ValueObject> arguments,
                                           IEnumerable<string> activeKeys) {
            // =======================================================================================================
            // $ pyrevit help
            // =======================================================================================================
            if (VerifyCommand(activeKeys, "help"))
                CommonUtils.OpenUrl(
                    helpUrl,
                    errMsg: "Can not open online help page. Try `pyrevit --help` instead"
                    );

            // =======================================================================================================
            // $ pyrevit (blog | docs | source | youtube | support)
            // =======================================================================================================
            if (VerifyCommand(activeKeys, "blog"))
                CommonUtils.OpenUrl(PyRevitConsts.BlogsUrl);

            else if (VerifyCommand(activeKeys, "docs"))
                CommonUtils.OpenUrl(PyRevitConsts.DocsUrl);

            else if (VerifyCommand(activeKeys, "source"))
                CommonUtils.OpenUrl(PyRevitConsts.SourceRepoUrl);

            else if (VerifyCommand(activeKeys, "youtube"))
                CommonUtils.OpenUrl(PyRevitConsts.YoutubeUrl);

            else if (VerifyCommand(activeKeys, "support"))
                CommonUtils.OpenUrl(PyRevitConsts.SupportRepoUrl);

            // =======================================================================================================
            // $ pyrevit info
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "env")) {
                PrintClones();
                PrintAttachments();
                PrintExtensions();
                PrintExtensionSearchPaths();
                PrintExtensionLookupSources();
                PrintInstalledRevits();
                PrintRunningRevits();
            }

            // =======================================================================================================
            // $ pyrevit clone <clone_name> [<dest_path>] [--branch=<branch_name>] [--deploy=<deployment_name>] [--nogit]
            // $ pyrevit clone <clone_name> <repo_or_archive_url> <dest_path> [--branch=<branch_name>] [--deploy=<deployment_name>] [--nogit]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clone")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                var deployName = TryGetValue(arguments, "--deploy");
                if (cloneName != null) {
                    PyRevit.Clone(
                        cloneName,
                        deploymentName: deployName,
                        branchName: TryGetValue(arguments, "--branch"),
                        repoOrArchivePath: TryGetValue(arguments, "<repo_or_archive_url>"),
                        destPath: TryGetValue(arguments, "<dest_path>"),
                        nogit: arguments["--nogit"].IsTrue
                        );
                }
            }

            // =======================================================================================================
            // $ pyrevit clones
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones"))
                PrintClones();

            // =======================================================================================================
            // $ pyrevit clones (info | open) <clone_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "info")
                    || VerifyCommand(activeKeys, "clones", "open")) {
                string cloneName = TryGetValue(arguments, "<clone_name>");
                PyRevitClone clone = PyRevit.GetRegisteredClone(cloneName);
                if (clone != null) {
                    if (arguments["info"].IsTrue) {
                        PrintHeader("Clone info");
                        Console.WriteLine(string.Format("\"{0}\" = \"{1}\"", clone.Name, clone.ClonePath));
                        if (clone.IsRepoDeploy) {
                            Console.WriteLine(string.Format("Clone is on branch \"{0}\"", clone.Branch));
                            // TODO: grab version from repo (last tag?)
                            Console.WriteLine(string.Format("Clone is on commit \"{0}\"", clone.Commit));
                        }
                        else
                            ReportCloneAsNoGit(clone);
                    }
                    else
                        CommonUtils.OpenInExplorer(clone.ClonePath);
                }
            }

            // =======================================================================================================
            // $ pyrevit clones add <clone_name> <clone_path>
            // $ pyrevit clones forget (--all | <clone_name>)
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "add")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                string clonePath = TryGetValue(arguments, "<clone_path>");
                if (clonePath != null)
                    PyRevit.RegisterClone(cloneName, clonePath);
            }

            else if (VerifyCommand(activeKeys, "clones", "forget")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                if (arguments["--all"].IsTrue)
                    PyRevit.UnregisterAllClones();
                else {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    PyRevit.UnregisterClone(clone);
                }
            }

            // =======================================================================================================
            // $ pyrevit clones rename <clone_name> <clone_new_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "rename")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                string cloneNewName = TryGetValue(arguments, "<clone_new_name>");
                if (cloneNewName != null) {
                    PyRevit.RenameClone(cloneName, cloneNewName);
                }
            }

            // =======================================================================================================
            // $ pyrevit clones delete [(--all | <clone_name>)] [--clearconfigs]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "delete")) {
                if (arguments["--all"].IsTrue)
                    PyRevit.DeleteAllClones(clearConfigs: arguments["--clearconfigs"].IsTrue);
                else {
                    var cloneName = TryGetValue(arguments, "<clone_name>");
                    if (cloneName != null) {
                        var clone = PyRevit.GetRegisteredClone(cloneName);
                        if (clone != null)
                            PyRevit.Delete(clone, clearConfigs: arguments["--clearconfigs"].IsTrue);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones branch <clone_name> [<branch_name>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "branch")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                var branchName = TryGetValue(arguments, "<branch_name>");
                if (cloneName != null) {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    if (clone != null) {
                        if (clone.IsRepoDeploy) {
                            if (branchName != null) {
                                clone.SetBranch(branchName);
                            }
                            else {
                                Console.WriteLine(string.Format("Clone \"{0}\" is on branch \"{1}\"",
                                                                 clone.Name, clone.Branch));
                            }
                        }
                        else
                            ReportCloneAsNoGit(clone);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones version <clone_name> [<tag_name>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "version")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                var tagName = TryGetValue(arguments, "<tag_name>");
                if (cloneName != null) {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    if (clone != null) {
                        if (clone.IsRepoDeploy) {
                            if (tagName != null) {
                                clone.SetTag(tagName);
                            }
                            else {
                                logger.Error("Version finder not yet implemented");
                                // TODO: grab version from repo (last tag?)
                            }
                        }
                        else
                            ReportCloneAsNoGit(clone);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones commit <clone_name> [<commit_hash>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "commit")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                var commitHash = TryGetValue(arguments, "<commit_hash>");
                if (cloneName != null) {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    if (clone != null) {
                        if (clone.IsRepoDeploy) {
                            if (commitHash != null) {
                                clone.SetCommit(commitHash);
                            }
                            else {
                                Console.WriteLine(string.Format("Clone \"{0}\" is on commit \"{1}\"",
                                                                 clone.Name, clone.Commit));
                            }
                        }
                        else
                            ReportCloneAsNoGit(clone);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones deployments <clone_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "deployments")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                if (cloneName != null) {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    if (clone != null) {
                        PrintHeader(string.Format("Deployments for \"{0}\"", clone.Name));
                        foreach (var dep in clone.GetDeployments()) {
                            Console.WriteLine(string.Format("\"{0}\" deploys:", dep.Name));
                            foreach (var path in dep.Paths)
                                Console.WriteLine("    " + path);
                            Console.WriteLine();
                        }
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones update (--all | <clone_name>)
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "update", "--all"))
                PyRevit.UpdateAllClones();

            else if (VerifyCommand(activeKeys, "clones", "update")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                if (cloneName != null) {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    PyRevit.Update(clone);
                }
            }

            // =======================================================================================================
            // $ pyrevit attach <clone_name> (latest | dynamosafe | <engine_version>) (<revit_year> | --all | --attached) [--allusers]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "attach")
                    || VerifyCommand(activeKeys, "attach", "latest")
                    || VerifyCommand(activeKeys, "attach", "dynamosafe")) {
                string cloneName = TryGetValue(arguments, "<clone_name>");
                var clone = PyRevit.GetRegisteredClone(cloneName);
                if (clone != null) {
                    int engineVer = 0;
                    if (arguments["dynamosafe"].IsTrue)
                        engineVer = PyRevitConsts.ConfigsDynamoCompatibleEnginerVer;
                    else {
                        string engineVersion = TryGetValue(arguments, "<engine_version>");
                        if (engineVersion != null)
                            engineVer = int.Parse(engineVersion);
                    }

                    if (arguments["--all"].IsTrue) {
                        foreach (var revit in RevitController.ListInstalledRevits())
                            PyRevit.Attach(revit.FullVersion.Major,
                                           clone,
                                           engineVer: engineVer,
                                           allUsers: arguments["--allusers"].IsTrue);
                    }
                    else if (arguments["--attached"].IsTrue) {
                        foreach (var revit in PyRevit.GetAttachedRevits())
                            PyRevit.Attach(revit.FullVersion.Major,
                                           clone,
                                           engineVer: engineVer,
                                           allUsers: arguments["--allusers"].IsTrue);
                    }
                    else {
                        string revitYear = TryGetValue(arguments, "<revit_year>");
                        if (revitYear != null)
                            PyRevit.Attach(int.Parse(revitYear),
                                           clone,
                                           engineVer: engineVer,
                                           allUsers: arguments["--allusers"].IsTrue);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit detach (--all | <revit_year>)
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "detach")) {
                string revitYear = TryGetValue(arguments, "<revit_year>");

                if (revitYear != null)
                    PyRevit.Detach(int.Parse(revitYear));
                else if (arguments["--all"].IsTrue)
                    PyRevit.DetachAll();
            }

            // =======================================================================================================
            // $ pyrevit attached
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "attached"))
                PrintAttachments();

            // =======================================================================================================
            // $ pyrevit extend <extension_name> <dest_path> [--branch=<branch_name>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extend")) {
                string destPath = TryGetValue(arguments, "<dest_path>");
                string extName = TryGetValue(arguments, "<extension_name>");
                string branchName = TryGetValue(arguments, "--branch");

                var ext = PyRevit.FindExtension(extName);
                if (ext != null) {
                    logger.Debug(string.Format("Matching extension found \"{0}\"", ext.Name));
                    PyRevit.InstallExtension(ext, destPath, branchName);
                }
                else {
                    if (Errors.LatestError == ErrorCodes.MoreThanOneItemMatched)
                        throw new pyRevitException(
                            string.Format("More than one extension matches the name \"{0}\"",
                                            extName));
                    else
                        throw new pyRevitException(
                            string.Format("Not valid extension name or repo url \"{0}\"",
                                            extName));
                }
            }

            // =======================================================================================================
            // $ pyrevit extend (ui | lib) <extension_name> <repo_url> <dest_path> [--branch=<branch_name>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extend", "ui")
                        || VerifyCommand(activeKeys, "extend", "lib")) {
                string destPath = TryGetValue(arguments, "<dest_path>");
                string extName = TryGetValue(arguments, "<extension_name>");
                string repoUrl = TryGetValue(arguments, "<repo_url>");
                string branchName = TryGetValue(arguments, "--branch");

                var extType =
                    arguments["ui"].IsTrue ?
                        PyRevitExtensionTypes.UIExtension : PyRevitExtensionTypes.LibraryExtension;

                if (repoUrl.IsValidUrl())
                    PyRevit.InstallExtension(extName, extType, repoUrl, destPath, branchName);
                else
                    logger.Error(string.Format("Repo url is not valid \"{0}\"", repoUrl));
            }

            // =======================================================================================================
            // $ pyrevit extensions
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions"))
                PrintExtensions();

            // =======================================================================================================
            // $ pyrevit extensions search <search_pattern>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "search")) {
                string searchPattern = TryGetValue(arguments, "<search_pattern>");
                var matchedExts = PyRevit.LookupRegisteredExtensions(searchPattern);
                PrintExtensions(extList: matchedExts, headerPrefix: "Matched");
            }

            // =======================================================================================================
            // $ pyrevit extensions (info | help | open) <extension_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "info")
                        || VerifyCommand(activeKeys, "extensions", "help")
                        || VerifyCommand(activeKeys, "extensions", "open")) {
                string extName = TryGetValue(arguments, "<extension_name>");
                if (extName != null) {
                    var ext = PyRevit.FindExtension(extName);
                    if (Errors.LatestError == ErrorCodes.MoreThanOneItemMatched)
                        logger.Warn(string.Format("More than one extension matches the search pattern \"{0}\"",
                                                    extName));
                    else {
                        if (arguments["info"].IsTrue)
                            Console.WriteLine(ext.ToString());
                        else if (arguments["help"].IsTrue)
                            Process.Start(ext.Website);
                        else if (arguments["open"].IsTrue)
                            CommonUtils.OpenInExplorer(ext.InstallPath);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit extensions delete <extension_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "delete")) {
                string extName = TryGetValue(arguments, "<extension_name>");
                PyRevit.UninstallExtension(extName);
            }

            // =======================================================================================================
            // $ pyrevit extensions paths
            // $ pyrevit extensions paths forget --all
            // $ pyrevit extensions paths (add | forget) <extensions_path>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "paths"))
                PrintExtensionSearchPaths();

            else if (VerifyCommand(activeKeys, "extensions", "forget", "--all")) {
                foreach (string searchPath in PyRevit.GetRegisteredExtensionSearchPaths())
                    PyRevit.UnregisterExtensionSearchPath(searchPath);
            }

            else if (VerifyCommand(activeKeys, "extensions", "paths", "add")
                        || VerifyCommand(activeKeys, "extensions", "paths", "forget")) {
                var searchPath = TryGetValue(arguments, "<extensions_path>");
                if (searchPath != null) {
                    if (arguments["add"].IsTrue)
                        PyRevit.RegisterExtensionSearchPath(searchPath);
                    else
                        PyRevit.UnregisterExtensionSearchPath(searchPath);
                }
            }

            // =======================================================================================================
            // $ pyrevit extensions (enable | disable) <extension_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "enable")
                        || VerifyCommand(activeKeys, "extensions", "disable")) {
                if (arguments["<extension_name>"] != null) {
                    string extensionName = TryGetValue(arguments, "<extension_name>");
                    if (extensionName != null) {
                        if (arguments["enable"].IsTrue)
                            PyRevit.EnableExtension(extensionName);
                        else
                            PyRevit.DisableExtension(extensionName);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit extensions sources
            // $ pyrevit extensions sources forget --all
            // $ pyrevit extensions sources (add | forget) <source_json_or_url>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "sources")
                        || VerifyCommand(activeKeys, "extensions", "disable"))
                PrintExtensionLookupSources();

            else if (VerifyCommand(activeKeys, "extensions", "sources", "add")
                        || VerifyCommand(activeKeys, "extensions", "sources", "forget")) {
                if (arguments["forget"].IsTrue && arguments["--all"].IsTrue)
                    PyRevit.UnregisterAllExtensionLookupSources();

                var sourceFileOrUrl = TryGetValue(arguments, "<source_json_or_url>");
                if (sourceFileOrUrl != null) {
                    if (arguments["add"].IsTrue)
                        PyRevit.RegisterExtensionLookupSource(sourceFileOrUrl);
                    else
                        PyRevit.UnregisterExtensionLookupSource(sourceFileOrUrl);
                }
            }


            // =======================================================================================================
            // $ pyrevit extensions update (--all | <extension_name>)
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "update", "--all"))
                PyRevit.UpdateAllInstalledExtensions();

            else if (VerifyCommand(activeKeys, "extensions", "update")) {
                var extName = TryGetValue(arguments, "<extension_name>");
                if (extName != null) {
                    var ext = PyRevit.GetInstalledExtension(extName);
                    PyRevit.UpdateExtension(ext);
                }
            }

            // =======================================================================================================
            // $ pyrevit revits [--installed]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "revits")) {
                if (arguments["--installed"].IsTrue)
                    PrintInstalledRevits();
                else
                    PrintRunningRevits();
            }

            // =======================================================================================================
            // $ pyrevit revits killall [<revit_year>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "revits", "killall")) {
                var revitYear = TryGetValue(arguments, "<revit_year>");
                if (revitYear != null)
                    RevitController.KillRunningRevits(int.Parse(revitYear));
                else
                    RevitController.KillAllRunningRevits();
            }

            // =======================================================================================================
            // $ pyrevit revits fileinfo <file_or_dir_path> [--csv=<output_file>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "revits", "fileinfo")) {
                var targetPath = TryGetValue(arguments, "<file_or_dir_path>");
                var outputCSV = TryGetValue(arguments, "--csv");

                // if targetpath is a single model print the model info
                if (File.Exists(targetPath))
                    if (outputCSV != null)
                        ExportModelInfoToCSV(
                            new List<RevitModelFile>() { new RevitModelFile(targetPath) },
                            outputCSV
                            );
                    else
                        PrintModelInfo(new RevitModelFile(targetPath));

                // collect all revit models
                else {
                    var models = new List<RevitModelFile>();
                    var errorList = new List<(string, string)>();

                    logger.Info(string.Format("Searching for revit files under \"{0}\"", targetPath));
                    FileAttributes attr = File.GetAttributes(targetPath);
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory) {
                        var files = Directory.EnumerateFiles(targetPath, "*.rvt", SearchOption.AllDirectories);
                        logger.Info(string.Format(" {0} revit files found under \"{1}\"", files.Count(), targetPath));
                        foreach (var file in files) {
                            try {
                                logger.Info(string.Format("Revit file found \"{0}\"", file));
                                var model = new RevitModelFile(file);
                                models.Add(model);
                            }
                            catch (Exception ex) {
                                errorList.Add((file, ex.Message));
                            }
                        }
                    }

                    if (outputCSV != null)
                        ExportModelInfoToCSV(models, outputCSV, errorList);
                    else {
                        // report info on all files
                        foreach (var model in models) {
                            Console.WriteLine(model.FilePath);
                            PrintModelInfo(new RevitModelFile(model.FilePath));
                            Console.WriteLine();
                        }

                        // write list of files with errors
                        if (errorList.Count > 0) {
                            Console.WriteLine("An error occured while processing these files:");
                            foreach (var errinfo in errorList)
                                Console.WriteLine(string.Format("\"{0}\": {1}\n", errinfo.Item1, errinfo.Item2));
                        }
                    }

                }
            }

            // =======================================================================================================
            // $ pyrevit revits addons
            // $ pyrevit revits addons install <addon_name> <dest_path> [--allusers]
            // $ pyrevit revits addons uninstall <addon_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "revits", "addons")
                        || VerifyCommand(activeKeys, "revits", "addons", "install")
                        || VerifyCommand(activeKeys, "revits", "addons", "uninstall")) {
                // TODO: implement revit addon manager
                logger.Error("Revit addon manager is not implemented yet");
            }

            // =======================================================================================================
            // $ pyrevit init (ui | lib) <extension_name>
            // $ pyrevit init (tab | panel | pull | split | push | smart | command) <bundle_name>
            // $ pyrevit init templates
            // $ pyrevit init templates (add | forget) <init_templates_path>
            // =======================================================================================================
            else if (arguments["init"].IsTrue) {
                // TODO: implement init and templates
                logger.Error("Init feature is not implemented yet");
            }

            // =======================================================================================================
            // $ pyrevit caches clear (--all | <revit_year>)
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "caches", "clear")) {
                if (arguments["--all"].IsTrue)
                    PyRevit.ClearAllCaches();
                else if (arguments["<revit_year>"] != null) {
                    var revitYear = TryGetValue(arguments, "<revit_year>");
                    if (revitYear != null)
                        PyRevit.ClearCache(int.Parse(revitYear));
                }
            }

            // =======================================================================================================
            // $ pyrevit config <template_config_path>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "config")) {
                var templateConfigFilePath = TryGetValue(arguments, "<template_config_path>");
                if (templateConfigFilePath != null)
                    PyRevit.SeedConfig(setupFromTemplate: templateConfigFilePath);
            }

            // =======================================================================================================
            // $ pyrevit configs logs [(none | verbose | debug)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "logs"))
                Console.WriteLine(String.Format("Logging Level is {0}", PyRevit.GetLoggingLevel().ToString()));

            else if (VerifyCommand(activeKeys, "configs", "logs", "none"))
                PyRevit.SetLoggingLevel(PyRevitLogLevels.None);

            else if (VerifyCommand(activeKeys, "configs", "logs", "verbose"))
                PyRevit.SetLoggingLevel(PyRevitLogLevels.Verbose);

            else if (VerifyCommand(activeKeys, "configs", "logs", "debug"))
                PyRevit.SetLoggingLevel(PyRevitLogLevels.Debug);

            // =======================================================================================================
            // $ pyrevit configs allowremotedll [(enable | disable)]
            // =======================================================================================================
            // TODO: Implement allowremotedll
            else if (VerifyCommand(activeKeys, "configs", "allowremotedll"))
                logger.Error("Not Yet Implemented");

            // =======================================================================================================
            // $ pyrevit configs checkupdates [(enable | disable)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "checkupdates"))
                Console.WriteLine(
                    String.Format("Check Updates is {0}",
                    PyRevit.GetCheckUpdates() ? "Enabled" : "Disabled")
                    );

            else if (VerifyCommand(activeKeys, "configs", "checkupdates", "enable")
                    || VerifyCommand(activeKeys, "configs", "checkupdates", "disable"))
                PyRevit.SetCheckUpdates(arguments["enable"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs autoupdate [(enable | disable)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "autoupdate"))
                Console.WriteLine(
                    String.Format("Auto Update is {0}",
                    PyRevit.GetAutoUpdate() ? "Enabled" : "Disabled")
                    );

            else if (VerifyCommand(activeKeys, "configs", "autoupdate", "enable")
                    || VerifyCommand(activeKeys, "configs", "autoupdate", "disable"))
                PyRevit.SetAutoUpdate(arguments["enable"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs rocketmode [(enable | disable)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "rocketmode"))
                Console.WriteLine(
                    String.Format("Rocket Mode is {0}",
                    PyRevit.GetRocketMode() ? "Enabled" : "Disabled")
                    );

            else if (VerifyCommand(activeKeys, "configs", "rocketmode", "enable")
                    || VerifyCommand(activeKeys, "configs", "rocketmode", "disable"))
                PyRevit.SetRocketMode(arguments["enable"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs filelogging [(enable | disable)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "filelogging"))
                Console.WriteLine(
                    String.Format("File Logging is {0}",
                    PyRevit.GetFileLogging() ? "Enabled" : "Disabled")
                    );

            else if (VerifyCommand(activeKeys, "configs", "filelogging", "enable")
                    || VerifyCommand(activeKeys, "configs", "filelogging", "disable"))
                PyRevit.SetFileLogging(arguments["enable"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs loadbeta [(enable | disable)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "loadbeta"))
                Console.WriteLine(
                    String.Format("Load Beta is {0}",
                    PyRevit.GetLoadBetaTools() ? "Enabled" : "Disabled")
                    );

            else if (VerifyCommand(activeKeys, "configs", "loadbeta", "enable")
                    || VerifyCommand(activeKeys, "configs", "loadbeta", "disable"))
                PyRevit.SetLoadBetaTools(arguments["enable"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs usagelogging
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "usagelogging")) {
                Console.WriteLine(
                    String.Format("Usage logging is {0}",
                    PyRevit.GetUsageReporting() ? "Enabled" : "Disabled")
                    );
                Console.WriteLine(String.Format("Log File Path: {0}", PyRevit.GetUsageLogFilePath()));
                Console.WriteLine(String.Format("Log Server Url: {0}", PyRevit.GetUsageLogServerUrl()));
            }

            // =======================================================================================================
            // $ pyrevit configs usagelogging enable (file | server) <dest_path>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "usagelogging", "enable", "file"))
                PyRevit.EnableUsageReporting(logFilePath: TryGetValue(arguments, "<dest_path>"));

            else if (VerifyCommand(activeKeys, "configs", "usagelogging", "enable", "server"))
                PyRevit.EnableUsageReporting(logServerUrl: TryGetValue(arguments, "<dest_path>"));

            // =======================================================================================================
            // $ pyrevit configs usagelogging disable
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "usagelogging", "disable"))
                PyRevit.DisableUsageReporting();

            // =======================================================================================================
            // $ pyrevit configs outputcss [<css_path>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "outputcss")) {
                if (arguments["<css_path>"] == null)
                    Console.WriteLine(
                        String.Format("Output Style Sheet is set to: {0}",
                        PyRevit.GetOutputStyleSheet()
                        ));
                else
                    PyRevit.SetOutputStyleSheet(TryGetValue(arguments, "<css_path>"));
            }

            // =======================================================================================================
            // $ pyrevit configs seed [--lock]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "seed"))
                PyRevit.SeedConfig(makeCurrentUserAsOwner: arguments["--lock"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs <option_path> (enable | disable)
            // $ pyrevit configs <option_path> [<option_value>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs")) {
                if (arguments["<option_path>"] != null) {
                    // extract section and option names
                    string orignalOptionValue = TryGetValue(arguments, "<option_path>");
                    if (orignalOptionValue.Split(':').Count() == 2) {
                        string configSection = orignalOptionValue.Split(':')[0];
                        string configOption = orignalOptionValue.Split(':')[1];

                        // if no value provided, read the value
                        if (arguments["<option_value>"] != null)
                            PyRevit.SetConfig(
                                configSection,
                                configOption,
                                TryGetValue(arguments, "<option_value>")
                                );
                        else if (arguments["<option_value>"] == null)
                            Console.WriteLine(
                                String.Format("{0} = {1}",
                                configOption,
                                PyRevit.GetConfig(configSection, configOption)
                                ));
                    }
                }
            }

            else if (VerifyCommand(activeKeys, "configs", "enable")
                    || VerifyCommand(activeKeys, "configs", "disable")) {
                if (arguments["<option_path>"] != null) {
                    // extract section and option names
                    string orignalOptionValue = TryGetValue(arguments, "<option_path>");
                    if (orignalOptionValue.Split(':').Count() == 2) {
                        string configSection = orignalOptionValue.Split(':')[0];
                        string configOption = orignalOptionValue.Split(':')[1];

                        PyRevit.SetConfig(configSection, configOption, arguments["enable"].IsTrue);
                    }
                }
            }
        }

        // get enabled keywords
        private static List<string> ExtractEnabledKeywords(IDictionary<string, ValueObject> arguments) {
            // grab active keywords
            var enabledKeywords = new List<string>();
            foreach (var argument in arguments.OrderBy(x => x.Key)) {
                if (argument.Value != null
                        && !argument.Key.Contains("<")
                        && !argument.Key.Contains(">")
                        && !argument.Key.Contains("--")
                        && argument.Value.IsTrue) {
                    logger.Debug(string.Format("Active Keyword: {0}", argument.Key));
                    enabledKeywords.Add(argument.Key);
                }
            }
            return enabledKeywords;
        }

        // verify cli command based on keywords that must be true and the rest of keywords must be false
        private static bool VerifyCommand(
                IEnumerable<string> enabledKeywords, params string[] keywords) {
            // check all given keywords are active
            if (keywords.Length != enabledKeywords.Count())
                return false;

            foreach (var keyword in keywords)
                if (!enabledKeywords.Contains(keyword))
                    return false;

            return true;
        }

        // safely try to get a value from arguments dictionary, return null on errors
        private static string TryGetValue(
                IDictionary<string, ValueObject> arguments, string key, string defaultValue = null) {
            return arguments[key] != null ? arguments[key].Value as string : defaultValue;
        }

        // process generated error codes and show prompts if necessary
        private static void ProcessErrorCodes() {
        }

        // process generated error codes and show prompts if necessary
        private static void LogException(Exception ex, pyRevitManagerLogLevel logLevel) {
            if (logLevel == pyRevitManagerLogLevel.Debug)
                logger.Error(string.Format("{0} ({1})\n{2}", ex.Message, ex.GetType().ToString(), ex.StackTrace));
            else
                logger.Error(string.Format("{0}\nRun with \"--debug\" option to see debug messages", ex.Message));
        }

        // print info on a revit model
        private static void PrintModelInfo(RevitModelFile model) {
            Console.WriteLine(string.Format("Created in: {0} ({1}({2}))",
                                model.RevitProduct.ProductName,
                                model.RevitProduct.BuildNumber,
                                model.RevitProduct.BuildTarget));
            Console.WriteLine(string.Format("Workshared: {0}", model.IsWorkshared ? "Yes" : "No"));
            if (model.IsWorkshared)
                Console.WriteLine(string.Format("Central Model Path: {0}", model.CentralModelPath));
            Console.WriteLine(string.Format("Last Saved Path: {0}", model.LastSavedPath));
            Console.WriteLine(string.Format("Document Id: {0}", model.UniqueId));
            Console.WriteLine(string.Format("Open Workset Settings: {0}", model.OpenWorksetConfig));
            Console.WriteLine(string.Format("Document Increment: {0}", model.DocumentIncrement));

            if (model.IsFamily) {
                Console.WriteLine("Model is a Revit Family!");
                Console.WriteLine(string.Format("Category Name: {0}", model.CategoryName));
                Console.WriteLine(string.Format("Host Category Name: {0}", model.HostCategoryName));
            }
        }

        // export model info to csv
        private static void ExportModelInfoToCSV(IEnumerable<RevitModelFile> models,
                                                 string outputCSV,
                                                 List<(string, string)> errorList = null) {
            logger.Info(string.Format("Building CSV data to \"{0}\"", outputCSV));
            var csv = new StringBuilder();
            csv.Append(
                "filepath,productname,buildnumber,isworkshared,centralmodelpath,lastsavedpath,uniqueid,error\n"
                );
            foreach (var model in models) {
                var data = new List<string>() {
                    string.Format("\"{0}\"", model.FilePath),
                    string.Format("\"{0}\"", model.RevitProduct != null ? model.RevitProduct.ProductName : ""),
                    string.Format("\"{0}\"", model.RevitProduct != null ? model.RevitProduct.BuildNumber : ""),
                    string.Format("\"{0}\"", model.IsWorkshared ? "True" : "False"),
                    string.Format("\"{0}\"", model.CentralModelPath),
                    string.Format("\"{0}\"", model.LastSavedPath),
                    string.Format("\"{0}\"", model.UniqueId.ToString()),
                    ""
                };

                csv.Append(string.Join(",", data) + "\n");
            }

            // write list of files with errors
            logger.Debug(string.Format("Adding errors to \"{0}\"", outputCSV));
            foreach (var errinfo in errorList)
                csv.Append(string.Format("\"{0}\",,,,,,,\"{1}\"\n", errinfo.Item1, errinfo.Item2));

            logger.Info(string.Format("Writing results to \"{0}\"", outputCSV));
            File.WriteAllText(outputCSV, csv.ToString());
        }

        private static void ReportCloneAsNoGit(PyRevitClone clone) {
            Console.WriteLine(
                string.Format("Clone \"{0}\" is deployed with `--nogit` option and is not a git repo.",
                clone.Name)
                );
        }

        private static void PrintHeader(string header) {
            Console.WriteLine(string.Format("==> {0}", header), Color.Green);
        }

        private static void PrintClones() {
            PrintHeader("Registered Clones (full repos)");
            var clones = PyRevit.GetRegisteredClones();
            foreach (var clone in clones)
                if (clone.IsRepoDeploy)
                    Console.WriteLine(string.Format("Name: \"{0}\" | Path: \"{1}\"", clone.Name, clone.ClonePath));

            PrintHeader("Registered Clones (`--nogit` from archive)");
            foreach (var clone in clones)
                if (!clone.IsRepoDeploy)
                    Console.WriteLine(string.Format("Name: \"{0}\" | Path: \"{1}\"", clone.Name, clone.ClonePath));
        }

        private static void PrintAttachments() {
            PrintHeader("Attachments");
            foreach (var revit in PyRevit.GetAttachedRevits()) {
                var clone = PyRevit.GetAttachedClone(revit.FullVersion.Major);
                if (clone != null)
                    Console.WriteLine(string.Format("{0} | Clone: \"{1}\"",
                                                    revit.ProductName, clone.Name));
                else
                    logger.Error(
                        string.Format("pyRevit is attached to Revit {0} but can not determine the clone",
                                      revit.FullVersion.Major)
                        );
            }
        }

        private static void PrintExtensions(IEnumerable<PyRevitExtension> extList = null,
                                            string headerPrefix = "Installed") {
            if (extList == null)
                extList = PyRevit.GetInstalledExtensions();

            PrintHeader(string.Format("{0} UI Extensions", headerPrefix));
            foreach (PyRevitExtension ext in extList.Where(e => e.Type == PyRevitExtensionTypes.UIExtension))
                Console.WriteLine(string.Format("Name: \"{0}\" | Repo: \"{1}\" | Installed: \"{2}\"",
                                                ext.Name, ext.Url, ext.InstallPath));

            PrintHeader(string.Format("{0} Library Extensions", headerPrefix));
            foreach (PyRevitExtension ext in extList.Where(e => e.Type == PyRevitExtensionTypes.LibraryExtension))
                Console.WriteLine(string.Format("Name: \"{0}\" | Repo: \"{1}\" | Installed: \"{2}\"",
                                                ext.Name, ext.Url, ext.InstallPath));
        }

        private static void PrintExtensionSearchPaths() {
            PrintHeader("Extension Search Paths");
            foreach (var searchPath in PyRevit.GetRegisteredExtensionSearchPaths())
                Console.WriteLine(searchPath);
        }

        private static void PrintExtensionLookupSources() {
            PrintHeader("Extension Sources - Default");
            Console.WriteLine(PyRevit.GetDefaultExtensionLookupSource());
            PrintHeader("Extension Sources - Additional");
            foreach (var extLookupSrc in PyRevit.GetRegisteredExtensionLookupSources())
                Console.WriteLine(extLookupSrc);
        }

        private static void PrintInstalledRevits() {
            PrintHeader("Installed Revits");
            foreach (var revit in RevitController.ListInstalledRevits())
                Console.WriteLine(revit);
        }

        private static void PrintRunningRevits() {
            PrintHeader("Running Revit Instances");
            foreach (var revit in RevitController.ListRunningRevits())
                Console.WriteLine(revit);
        }

        private static void PrintPyRevitPaths() {
            PrintHeader("Cache Directory");
            Console.WriteLine(string.Format("\"{0}\"", PyRevit.pyRevitAppDataPath));
        }
    }
}
