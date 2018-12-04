using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pyRevitLabs.DelimitedFlatFileDB {
    public sealed class DFFDBTableConfig {
        public DFFDBTableConfig() {}

        public IEnumerable<DFFDBField> Fields { get; set; } = new List<DFFDBField>();
        public DFFDBField Key = null;
        public string FieldSeparator { get; set; } = "\t";
        public bool AllowsTags { get; set; } = true;
        public bool IsInternal { get; set; } = false;
        public bool KeepsHistory { get; set; } = false;
    }

    public class DFFDBTable {

        public DFFDBTable(string tableName, string tableDescription, DFFDBTableConfig configs = null) {
            Name = tableName;
            Description = tableDescription;

            if (configs == null)
                configs = new DFFDBTableConfig();

            Configs = configs;
        }

        public string Name { get; private set; }
        public string Description { get; private set; }

        public DFFDBTableConfig Configs { get; private set; }

        public List<DFFDBField> Fields { get { return new List<DFFDBField>(Configs.Fields); } }
        public DFFDBField Key { get { return Configs.Key; } }
        public string FieldSeparator { get { return Configs.FieldSeparator; } }
        public bool AllowsTags { get { return Configs.AllowsTags; } }
        public bool IsInternal { get { return Configs.IsInternal; } }
        public bool KeepsHistory { get { return Configs.KeepsHistory; } }

        public List<DFFDBRecord> Records { get; private set; } = new List<DFFDBRecord>();
    }
}
