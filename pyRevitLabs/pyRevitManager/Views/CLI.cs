﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Diagnostics;

using pyRevitLabs.Common;
using pyRevitLabs.TargetApps.Revit;
using pyRevitLabs.Language.Properties;

using pyRevitManager.Properties;

using DocoptNet;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace pyRevitManager.Views {

    class pyRevitCLI {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const string helpUrl = "https://github.com/eirannejad/pyRevitLabs";
        private const string usage = @"pyrevit command line tool

    Usage:
        pyrevit (-h | --help)
        pyrevit (-V | --version)
        pyrevit help
        pyrevit install [--core] [--branch=<branch_name>] [<dest_path>]
        pyrevit install <repo_url> <dest_path> [--core] [--branch=<branch_name>]
        pyrevit register <repo_path>
        pyrevit unregister <repo_path>
        pyrevit uninstall [(--all | <repo_path>)] [--clearconfigs]
        pyrevit setprimary <repo_path>
        pyrevit checkout <branch_name> [<repo_path>]
        pyrevit setcommit <commit_hash> [<repo_path>]
        pyrevit setversion <tag_name> [<repo_path>]
        pyrevit update [--all] [<repo_path>]
        pyrevit attach (--all | <revit_version>) [<repo_path>] [--allusers]
        pyrevit detach (--all | <revit_version>)
        pyrevit setengine latest (--all | --attached | <revit_version>) [<repo_path>]
        pyrevit setengine dynamosafe (--all | --attached | <revit_version>) [<repo_path>]
        pyrevit setengine <engine_version> (--all | --attached | <revit_version>) [<repo_path>]
        pyrevit extensions list
        pyrevit extensions search [<search_pattern>]
        pyrevit extensions info <extension_name>
        pyrevit extensions install <extension_name> <dest_path> [--branch=<branch_name>]
        pyrevit extensions install <repo_url> <dest_path> [--branch=<branch_name>]
        pyrevit extensions uninstall <extension_name> <dest_path> [--branch=<branch_name>]
        pyrevit extensions paths
        pyrevit extensions paths (add | remove) <extensions_path>
        pyrevit extensions <extension_name> (enable | disable)
        pyrevit open
        pyrevit info
        pyrevit listrevits [--installed]
        pyrevit killrevits
        pyrevit clearcache (--all | <revit_version>)
        pyrevit allowremotedll [(enable | disable)]
        pyrevit checkupdates [(enable | disable)]
        pyrevit autoupdate [(enable | disable)]
        pyrevit rocketmode [(enable | disable)]
        pyrevit logs [(none | verbose | debug)]
        pyrevit filelogging [(enable | disable)]
        pyrevit loadbeta [(enable | disable)]
        pyrevit usagelogging
        pyrevit usagelogging enable (file | server) <dest_path>
        pyrevit usagelogging disable
        pyrevit outputcss [<css_path>]
        pyrevit config seed
        pyrevit config <option_path> (enable | disable)
        pyrevit config <option_path> [<option_value>]
        

    Options:
        -h --help                   Show this screen.
        -V --version                Show version.
        --verbose                   Print logger non-debug messages.
        --debug                     Print docopt options and logger debug messages.
        --core                      Install original pyRevit core only (no defualt tools).
        --all                       All applicable items.
        --attached                  All Revits that are configured to load pyRevit.
        --authgroup=<auth_groups>   User groups authorized to use the extension.
        --branch=<branch_name>      Target git branch name.
";

        public static void ProcessArguments(string[] args) {
            // process arguments for hidden debug mode switch
            bool debugMode = false;
            bool verboseMode = false;

            var argsList = new List<string>(args);
            if (argsList.Contains("--debug")) {
                argsList.Remove("--debug");
                debugMode = true;
            }

            // process arguments for verbose
            if (argsList.Contains("--verbose")) {
                argsList.Remove("--verbose");
                verboseMode = true;
            }

            // setup logger
            var config = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget("target1") {
                Layout = @"${level}: ${message} ${exception}"
            };
            config.AddTarget(consoleTarget);
            config.AddRuleForAllLevels(consoleTarget);
            LogManager.Configuration = config;

            // process docopt
            var arguments = new Docopt().Apply(
                usage,
                argsList,
                version: String.Format(StringLib.ConsoleVersionFormat,
                                       Assembly.GetExecutingAssembly().GetName().Version.ToString()),
                exit: true,
                help: true
            );

            // print active arguments in debug mode
            if (debugMode)
                foreach (var argument in arguments.OrderBy(x => x.Key)) {
                    if (argument.Value != null && (argument.Value.IsTrue || argument.Value.IsString))
                        Console.WriteLine("{0} = {1}", argument.Key, argument.Value);
                }

            if (!verboseMode)
                foreach (var rule in LogManager.Configuration.LoggingRules)
                    rule.DisableLoggingForLevel(LogLevel.Info);

            // set log level status
            if (!debugMode)
                foreach (var rule in LogManager.Configuration.LoggingRules)
                    rule.DisableLoggingForLevel(LogLevel.Debug);

            // now call methods based on inputs
            // =======================================================================================================
            // $ pyrevit help
            // =======================================================================================================
            if (arguments["help"].IsTrue) {
                Process.Start(helpUrl);

                ProcessErrorCodes();
            }


            // =======================================================================================================
            // $ pyrevit install [--core] [--branch=<branch_name>] [<dest_path>]
            // $ pyrevit install <repo_url> <dest_path> [--core] [--branch=<branch_name>]
            // =======================================================================================================
            else if (arguments["install"].IsTrue) {
                try {
                    pyRevit.Install(
                        coreOnly: arguments["--core"].IsTrue,
                        branchName: TryGetValue(arguments, "--branch"),
                        repoPath: TryGetValue(arguments, "<repo_url>"),
                        destPath: TryGetValue(arguments, "<dest_path>")
                        );
                }
                catch (Exception ex) {
                    LogException(ex);
                }

                ProcessErrorCodes();
            }


            // =======================================================================================================
            // $ pyrevit register <repo_path>
            // $ pyrevit unregister <repo_path>
            // =======================================================================================================
            else if (arguments["register"].IsTrue) {
                string repoPath = TryGetValue(arguments, "<repo_path>");
                if (repoPath != null) {
                    try {
                        pyRevit.RegisterClone(repoPath);
                    }
                    catch (pyRevitException ex) {
                        logger.Error(ex.ToString());
                    }
                }
            }

            else if (arguments["unregister"].IsTrue) {
                string repoPath = TryGetValue(arguments, "<repo_path>");
                if (repoPath != null)
                    pyRevit.UnregisterClone(repoPath);
            }



            // =======================================================================================================
            // $ pyrevit uninstall [(--all | <repo_path>)] [--clearconfigs]
            // =======================================================================================================
            else if (arguments["uninstall"].IsTrue) {
                if (arguments["--all"].IsTrue)
                    pyRevit.UninstallAllClones(clearConfigs: arguments["--clearconfigs"].IsTrue);
                else
                    pyRevit.Uninstall(
                        repoPath: TryGetValue(arguments, "<repo_path>"),
                        clearConfigs: arguments["--clearconfigs"].IsTrue
                        );
            }


            // =======================================================================================================
            // $ pyrevit setprimary <repo_path>
            // =======================================================================================================
            else if (arguments["setprimary"].IsTrue) {
                pyRevit.SetPrimaryClone(TryGetValue(arguments, "<repo_path>"));
            }


            // =======================================================================================================
            // $ pyrevit checkout <branch_name> [<repo_path>]
            // =======================================================================================================
            else if (arguments["checkout"].IsTrue) {
                pyRevit.Checkout(
                    TryGetValue(arguments, "<branch_name>"),
                    TryGetValue(arguments, "<repo_path>")
                    );
            }

            // =======================================================================================================
            // $ pyrevit setcommit <commit_hash> [<repo_path>]
            // =======================================================================================================
            else if (arguments["setcommit"].IsTrue) {
                pyRevit.SetCommit(
                    TryGetValue(arguments, "<commit_hash>"),
                    TryGetValue(arguments, "<repo_path>")
                    );
            }


            // =======================================================================================================
            // $ pyrevit setversion <tag_name> [<repo_path>]
            // =======================================================================================================
            else if (arguments["setversion"].IsTrue) {
                pyRevit.SetVersion(
                    TryGetValue(arguments, "<tag_name>"),
                    TryGetValue(arguments, "<repo_path>")
                    );
            }


            // =======================================================================================================
            // $ pyrevit update [--all] [<repo_path>]
            // =======================================================================================================
            else if (arguments["update"].IsTrue) {
                pyRevit.Update(repoPath: TryGetValue(arguments, "<repo_url>"));
            }


            // =======================================================================================================
            // $ pyrevit attach (--all | <revit_version>) [<repo_path>]
            // =======================================================================================================
            else if (arguments["attach"].IsTrue) {
                string revitVersion = TryGetValue(arguments, "<revit_version>");
                string repoPath = TryGetValue(arguments, "<repo_path>");

                if (revitVersion != null)
                    pyRevit.Attach(revitVersion, repoPath: repoPath, allUsers: arguments["--allusers"].IsTrue);
                else if (arguments["--all"].IsTrue)
                    pyRevit.AttachAll(repoPath: repoPath, allUsers: arguments["--allusers"].IsTrue);
            }


            // =======================================================================================================
            // $ pyrevit detach (--all | <revit_version>)
            // =======================================================================================================
            else if (arguments["detach"].IsTrue) {
                string revitVersion = TryGetValue(arguments, "<revit_version>");
                if (revitVersion != null)
                    pyRevit.Detach(revitVersion);
                else if (arguments["--all"].IsTrue)
                    pyRevit.DetachAll();
            }


            // =======================================================================================================
            // $ pyrevit setengine latest (--all | <revit_version>) [<repo_path>]
            // $ pyrevit setengine <engine_version> (--all | <revit_version>) [<repo_path>]
            // =======================================================================================================
            else if (arguments["setengine"].IsTrue) {
                int engineVersion = -001;

                // switch to latest if requested
                if (arguments["latest"].IsTrue)
                    engineVersion = 000;

                // switch to latest if requested
                else if (arguments["dynamosafe"].IsTrue)
                    engineVersion = pyRevit.pyRevitDynamoCompatibleEnginerVer;

                // check to see if engine version is specified
                else {
                    string engineVersionString = TryGetValue(arguments, "<engine_version>");
                    if (engineVersionString != null)
                        engineVersion = int.Parse(engineVersionString);
                }

                if (engineVersion > -1) {
                    string revitVersion = TryGetValue(arguments, "<revit_version>");
                    string repoPath = TryGetValue(arguments, "<repo_path>");

                    if (revitVersion != null)
                        pyRevit.Attach(
                            revitVersion,
                            repoPath: repoPath,
                            engineVer: engineVersion
                            );
                    else if (arguments["--all"].IsTrue) {
                        pyRevit.AttachAll(
                            repoPath: repoPath,
                            engineVer: engineVersion
                            );
                    }
                    else if (arguments["--attached"].IsTrue) {
                        foreach (var revitVer in pyRevit.GetAttachedRevitVersions())
                            pyRevit.Attach(
                                revitVer.Major.ToString(),
                                repoPath: repoPath,
                                engineVer: engineVersion
                                );
                    }
                }
            }


            // =======================================================================================================
            // $ pyrevit extensions search [<search_pattern>]
            // $ pyrevit extensions info <extension_name>
            // =======================================================================================================
            // TODO: Implement extensions install
            else if (arguments["extensions"].IsTrue && arguments["search"].IsTrue) {
                string searchPattern = TryGetValue(arguments, "<search_pattern>");
                var extList = pyRevit.LookupRegisteredExtensions(searchPattern);
                Console.WriteLine("==> UI Extensions");
                foreach (pyRevitExtension ext in extList)
                    Console.WriteLine(String.Format("{0}{1}", ext.Name.PadRight(24), ext.Url));
            }

            if (arguments["extensions"].IsTrue && arguments["info"].IsTrue) {
                string extName = TryGetValue(arguments, "<extension_name>");
                if (extName != null) {
                    var ext = pyRevit.LookupExtension(extName);
                    if (ext != null)
                        Console.WriteLine(ext.ToString());
                }
            }


            // =======================================================================================================
            // $ pyrevit extensions install <extension_name> <dest_path> [--branch=<branch_name>]
            // $ pyrevit extensions install <repo_url> <dest_path> [--branch=<branch_name>]
            // =======================================================================================================
            // TODO: Implement extensions install
            else if (arguments["extensions"].IsTrue && arguments["install"].IsTrue) {
                string destPath = TryGetValue(arguments, "<dest_path>");
                string extensionName = TryGetValue(arguments, "<extension_name>");
                string repoUrl = TryGetValue(arguments, "<repo_url>");
                string branchName = TryGetValue(arguments, "--branch");

                if (extensionName != null)
                    pyRevit.InstallExtension(extensionName, destPath, branchName);
                else if (repoUrl != null)
                    pyRevit.InstallExtensionFromRepo(repoUrl, destPath, branchName);
            }


            // =======================================================================================================
            // $ pyrevit extensions uninstall <extension_name> <dest_path> [--branch=<branch_name>]
            // =======================================================================================================
            // TODO: Implement extensions uninstall
            else if (arguments["extensions"].IsTrue && arguments["uninstall"].IsTrue) {
                logger.Error("Not Yet Implemented.");
            }


            // =======================================================================================================
            // $ pyrevit extensions paths
            // $ pyrevit extensions paths (add | remove) <extensions_path>
            // =======================================================================================================
            // TODO: Implement extensions paths
            if (arguments["extensions"].IsTrue && arguments["paths"].IsTrue) {
                logger.Error("Not Yet Implemented.");
            }


            // =======================================================================================================
            // $ pyrevit extensions <extension_name> (enable | disable)
            // =======================================================================================================
            else if (arguments["extensions"].IsTrue) {
                if (arguments["<extension_name>"] != null) {
                    string extensionName = TryGetValue(arguments, "<extension_name>");
                    if (arguments["enable"].IsTrue)
                        pyRevit.EnableExtension(extensionName);
                    else if (arguments["disable"].IsTrue)
                        pyRevit.DisableExtension(extensionName);
                }
            }


            // =======================================================================================================
            // $ pyrevit open
            // =======================================================================================================
            else if (arguments["open"].IsTrue) {
                string primaryRepo = pyRevit.GetPrimaryClone();
                Process.Start("explorer.exe", primaryRepo);
            }


            // =======================================================================================================
            // $ pyrevit info
            // =======================================================================================================
            // TODO: List attached revits
            else if (arguments["info"].IsTrue) {
                Console.Write(String.Format("Primary Repository: {0}", pyRevit.GetPrimaryClone()));
                Console.WriteLine("\nRegistered Repositories:");
                foreach (string clone in pyRevit.GetClones()) {
                    Console.WriteLine(clone);
                }
            }


            // =======================================================================================================
            // $ pyrevit listrevits [--installed]
            // =======================================================================================================
            else if (arguments["listrevits"].IsTrue) {
                if (arguments["--installed"].IsTrue)
                    foreach (var revit in RevitConnector.ListInstalledRevits())
                        Console.WriteLine(revit);
                else
                    foreach (var revit in RevitConnector.ListRunningRevits())
                        Console.WriteLine(revit);
            }


            // =======================================================================================================
            // $ pyrevit listrevits
            // =======================================================================================================
            else if (arguments["killrevits"].IsTrue) {
                RevitConnector.KillAllRunningRevits();
            }


            // =======================================================================================================
            // $ pyrevit clearcache (--all | <revit_version>)
            // =======================================================================================================
            else if (arguments["clearcache"].IsTrue) {
                if (arguments["--all"].IsTrue) {
                    pyRevit.ClearAllCaches();
                }
                else if (arguments["<revit_version>"] != null) {
                    pyRevit.ClearCache(TryGetValue(arguments, "<revit_version>"));
                }
            }


            // =======================================================================================================
            // $ pyrevit clearcache (--all | <revit_version>)
            // =======================================================================================================
            // TODO: Implement allowremotedll
            else if (arguments["allowremotedll"].IsTrue) {
                logger.Error("Not Yet Implemented.");
            }


            // =======================================================================================================
            // $ pyrevit checkupdate [(enable | disable)]
            // =======================================================================================================
            else if (arguments["checkupdates"].IsTrue) {
                if (arguments["enable"].IsFalse && arguments["disable"].IsFalse)
                    Console.WriteLine(
                        String.Format("Check Updates is {0}.",
                        pyRevit.GetCheckUpdates() ? "Enabled" : "Disabled")
                        );
                else
                    pyRevit.SetCheckUpdates(arguments["enable"].IsTrue);
            }


            // =======================================================================================================
            // $ pyrevit autoupdate [(enable | disable)]
            // =======================================================================================================
            else if (arguments["autoupdate"].IsTrue) {
                if (arguments["enable"].IsFalse && arguments["disable"].IsFalse)
                    Console.WriteLine(
                        String.Format("Auto Update is {0}.",
                        pyRevit.GetAutoUpdate() ? "Enabled" : "Disabled")
                        );
                else
                    pyRevit.SetAutoUpdate(arguments["enable"].IsTrue);
            }


            // =======================================================================================================
            // $ pyrevit rocketmode [(enable | disable)]
            // =======================================================================================================
            else if (arguments["rocketmode"].IsTrue) {
                if (arguments["enable"].IsFalse && arguments["disable"].IsFalse)
                    Console.WriteLine(
                        String.Format("Rocket Mode is {0}.",
                        pyRevit.GetRocketMode() ? "Enabled" : "Disabled")
                        );
                else
                    pyRevit.SetRocketMode(arguments["enable"].IsTrue);
            }


            // =======================================================================================================
            // $ pyrevit logs [(none | verbose | debug)]
            // =======================================================================================================
            else if (arguments["logs"].IsTrue) {
                if (arguments["none"].IsFalse && arguments["verbose"].IsFalse && arguments["debug"].IsFalse)
                    Console.WriteLine(String.Format("Logging Level is {0}.", pyRevit.GetLoggingLevel().ToString()));
                else {
                    if (arguments["none"].IsTrue)
                        pyRevit.SetLoggingLevel(PyRevitLogLevels.None);
                    else if (arguments["verbose"].IsTrue)
                        pyRevit.SetLoggingLevel(PyRevitLogLevels.Verbose);
                    else if (arguments["debug"].IsTrue)
                        pyRevit.SetLoggingLevel(PyRevitLogLevels.Debug);
                }
            }


            // =======================================================================================================
            // $ pyrevit filelogging [(enable | disable)]
            // =======================================================================================================
            else if (arguments["filelogging"].IsTrue) {
                if (arguments["enable"].IsFalse && arguments["disable"].IsFalse)
                    Console.WriteLine(
                        String.Format("File Logging is {0}.",
                        pyRevit.GetFileLogging() ? "Enabled" : "Disabled")
                        );
                else
                    pyRevit.SetFileLogging(arguments["enable"].IsTrue);
            }


            // =======================================================================================================
            // $ pyrevit loadbeta [(enable | disable)]
            // =======================================================================================================
            else if (arguments["loadbeta"].IsTrue) {
                if (arguments["enable"].IsFalse && arguments["disable"].IsFalse)
                    Console.WriteLine(
                        String.Format("Load Beta is {0}.",
                        pyRevit.GetLoadBetaTools() ? "Enabled" : "Disabled")
                        );
                else
                    pyRevit.SetLoadBetaTools(arguments["enable"].IsTrue);
            }


            // =======================================================================================================
            // $ pyrevit usagelogging
            // =======================================================================================================
            else if (arguments["usagelogging"].IsTrue
                    && arguments["enable"].IsFalse
                    && arguments["disable"].IsFalse) {
                Console.WriteLine(
                    String.Format("Usage logging is {0}.",
                    pyRevit.GetUsageReporting() ? "Enabled" : "Disabled")
                    );
                Console.WriteLine(String.Format("Log File Path: {0}", pyRevit.GetUsageLogFilePath()));
                Console.WriteLine(String.Format("Log Server Url: {0}", pyRevit.GetUsageLogServerUrl()));
            }


            // =======================================================================================================
            // $ pyrevit usagelogging enable (file | server) <dest_path>
            // =======================================================================================================
            else if (arguments["usagelogging"].IsTrue && arguments["enable"].IsTrue) {
                if (arguments["file"].IsTrue)
                    pyRevit.EnableUsageReporting(logFilePath: TryGetValue(arguments, "<dest_path>"));
                else
                    pyRevit.EnableUsageReporting(logServerUrl: TryGetValue(arguments, "<dest_path>"));
            }


            // =======================================================================================================
            // $ pyrevit usagelogging disable
            // =======================================================================================================
            else if (arguments["usagelogging"].IsTrue && arguments["disable"].IsTrue) {
                pyRevit.DisableUsageReporting();
            }


            // =======================================================================================================
            // $ pyrevit outputcss [<css_path>]
            // =======================================================================================================
            else if (arguments["outputcss"].IsTrue) {
                if (arguments["<css_path>"] == null)
                    Console.WriteLine(
                        String.Format("Output Style Sheet is set to: {0}",
                        pyRevit.GetOutputStyleSheet()
                        ));
                else
                    pyRevit.SetOutputStyleSheet(TryGetValue(arguments, "<css_path>"));
            }

            // =======================================================================================================
            // $ pyrevit config seed
            // =======================================================================================================
            else if (arguments["config"].IsTrue && arguments["seed"].IsTrue) {
                pyRevit.SeedConfig();
            }

            // =======================================================================================================
            // $ pyrevit config <option_path> (enable | disable)
            // $ pyrevit config <option_path> [<option_value>]
            // =======================================================================================================
            else if (arguments["config"].IsTrue && arguments["<option_path>"] != null) {
                // extract section and option names
                string orignalOptionValue = TryGetValue(arguments, "<option_path>");
                if (orignalOptionValue.Split(':').Count() == 2) {
                    string configSection = orignalOptionValue.Split(':')[0];
                    string configOption = orignalOptionValue.Split(':')[1];

                    // if no value provided, read the value
                    if (arguments["<option_value>"] == null
                                && arguments["enable"].IsFalse
                                && arguments["disable"].IsFalse)
                        Console.WriteLine(
                            String.Format("{0} = {1}",
                            configOption,
                            pyRevit.GetConfig(configSection, configOption)
                            ));
                    // if enable | disable
                    else if (arguments["enable"].IsTrue)
                        pyRevit.SetConfig(configSection, configOption, true);
                    else if (arguments["disable"].IsTrue)
                        pyRevit.SetConfig(configSection, configOption, false);
                    // if custom value 
                    else if (arguments["<option_value>"] != null)
                        pyRevit.SetConfig(
                            configSection,
                            configOption,
                            TryGetValue(arguments, "<option_value>")
                            );
                }
            }

            // now process any error codes
            ProcessErrorCodes();
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
        private static void LogException(Exception ex) {
            logger.Error(String.Format("{0} ({1})\n{2}", ex.Message, ex.GetType().ToString(), ex.StackTrace));
        }
    }

}
