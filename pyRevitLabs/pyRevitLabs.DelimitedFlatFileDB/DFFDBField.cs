using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pyRevitLabs.DelimitedFlatFileDB {
    public enum DFFDBFieldType {
        Boolean,
        Byte,
        Integer,
        Text,
        Date,
        Time,
        TimeStamp,
        TimeStampTZ,
        Decimal,
        UUID,
        JSON,
        XML
    }

    public class DFFDBField {
        public DFFDBField(string fieldName, string fieldDescription, DFFDBFieldType fieldType) {
            Name = fieldName;
            Description = fieldDescription;
            Type = fieldType;
        }

        public string Name { get; private set; }
        public string Description { get; private set; }
        public DFFDBFieldType Type { get; private set; }

        public override int GetHashCode() {
            return Name.GetHashCode();
        }
    }

    public sealed class DFFDBBooleanField : DFFDBField {
        public DFFDBBooleanField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.Boolean) { }
    }

    public sealed class DFFDBByteField : DFFDBField {
        public DFFDBByteField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.Byte) { }
    }

    public sealed class DFFDBIntegerField : DFFDBField {
        public DFFDBIntegerField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.Integer) { }
    }

    public sealed class DFFDBTextField : DFFDBField {
        public DFFDBTextField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.Text) { }
    }

    public sealed class DFFDBDateField : DFFDBField {
        public DFFDBDateField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.Date) { }
    }

    public sealed class DFFDBTimeField : DFFDBField {
        public DFFDBTimeField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.Time) { }
    }

    public sealed class DFFDBTimeStampField : DFFDBField {
        public DFFDBTimeStampField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.TimeStamp) { }
    }

    public sealed class DFFDBTimeStampTZField : DFFDBField {
        public DFFDBTimeStampTZField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.TimeStampTZ) { }
    }

    public sealed class DFFDBDecimalField : DFFDBField {
        public DFFDBDecimalField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.Decimal) { }
    }

    public sealed class DFFDBUUIDField : DFFDBField {
        public DFFDBUUIDField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.UUID) { }
    }

    public sealed class DFFDBJSONField : DFFDBField {
        public DFFDBJSONField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.JSON) { }
    }

    public sealed class DFFDBXMLField : DFFDBField {
        public DFFDBXMLField(string fieldName, string fieldDescription) : base(fieldName, fieldDescription, DFFDBFieldType.XML) { }
    }
}
