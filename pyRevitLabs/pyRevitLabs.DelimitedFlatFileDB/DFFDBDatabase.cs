using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using pyRevitLabs.Common;

namespace pyRevitLabs.DelimitedFlatFileDB {
    public sealed class DFFDBDatabase {
        public DFFDBDatabase(string filePath, string name = "", Encoding encoding = null) {
            Name = name;
            File = new DFFDBFile(filePath, encoding: encoding);
        }

        public string Name { get; private set; }
        public DFFDBFile File { get; private set; }
        public string FilePath { get { return File.FilePath;  } }

        public Version Version { get; } = new Version(0, 1);

        public List<DFFDBTable> Tables = new List<DFFDBTable>();

        public string Commit() {
            var dataString = DFFDBFormat.BuildDBStringRepr(this);
            try {
                File.Write(dataString);
                return dataString;
            }
            catch {
                return "";
            }
        }
    }
}
