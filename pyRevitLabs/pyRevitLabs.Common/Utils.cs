﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace pyRevitLabs.Common {
    public static class CommonUtils {

        // helper for deleting directories recursively
        public static void DeleteDirectory(string target_dir) {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files) {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs) {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }

        public static void ConfirmPath(string path) {
            Directory.CreateDirectory(path);
        }

        public static void ConfirmFile(string filepath) {
            ConfirmPath(Path.GetDirectoryName(filepath));
            if (!File.Exists(filepath)) {
                var file = File.CreateText(filepath);
                file.Close();
            }
        }

        public static string DownloadFile(string url, string destPath) {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var client = new WebClient()) {
                client.DownloadFile(url, destPath);
            }

            return destPath;
        }
    }
}
