using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pyRevitLabs.DelimitedFlatFileDB {
    public sealed class DFFDBRecord {
        private Dictionary<DFFDBField, object> fieldValues = new Dictionary<DFFDBField, object>();

        public DFFDBRecord() { }

        public void SetFieldValue(DFFDBTextField field, string fieldValue) {
            fieldValues[field] = fieldValue;
        }

        public string GetFieldValue(DFFDBTextField field) {
            object value;
            if (fieldValues.TryGetValue(field, out value))
                return (string)value;

            throw new KeyNotFoundException();
        }
    }
}
