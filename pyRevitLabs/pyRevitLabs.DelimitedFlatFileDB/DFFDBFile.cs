using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using pyRevitLabs.Common;

namespace pyRevitLabs.DelimitedFlatFileDB {
    public class DFFDBFile {
        public DFFDBFile(string filePath, Encoding encoding = null) {
            CommonUtils.ConfirmFile(filePath);
            FilePath = filePath;

            if (encoding != null)
                Encoding = encoding;
        }

        public string FilePath { get; private set; }
        public Encoding Encoding = Encoding.GetEncoding("ISO-8859-1"); // the default utf-8 uses BOM

        public void Read() {
            // TODO: read db file
        }

        public void Write(string contents) {
            using (StreamWriter sw = new StreamWriter(File.Open(FilePath, FileMode.Create), Encoding)) {
                sw.Write(contents);
            }
        }
    }
}
