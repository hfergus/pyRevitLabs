using System;
using System.Collections.Generic;
using System.Linq;

namespace pyRevitLabs.DelimitedFlatFileDB {
    public static class DFFDBFormat {
        // generic metadata
        private const string medataLineStart = "#";
        private const string mdataPrefix = "@";
        private const string mdataValueOpen = "(";
        private const string mdataValueSeparator = ":";
        private const string mdataValueClose = ")";
        private const string mdataSeparator = " ";

        // values
        private const string mdataBoolTrue = "yes";
        private const string mdataBoolFalse = "no";
        private const string mdataNotSet = "none";
        private const string mdataNULL = "NULL";

        // db
        private const string mdataDBPrefix = "=====================";
        private const string mdataDB = "db";
        private const string mdataDBFile = "file";
        private const string mdataDBVersion = "version";

        // tables
        private const string mdataTablePrefix = "=====================";
        private const string mdataTableFieldsPrefix = "";
        private const string mdataTable = "table";
        private const string mdataTableDescription = "desc";
        private const string mdataTableInternal = "internal";
        private const string mdataTableHistory = "history";
        private const string mdataTableFieldSeparator = "sep";
        private const string mdataTableTags = "tags";

        private const string mdataPKField = "key";
        private const string mdataFieldBool = "bool";
        private const string mdataFieldByte = "byte";
        private const string mdataFieldInteger = "int";
        private const string mdataFieldText = "text";
        private const string mdataFieldDate = "date";
        private const string mdataFieldTime = "time";
        private const string mdataFieldTimeStamp = "tstamp";
        private const string mdataFieldTimeStampTZ = "tstamptz";
        private const string mdataFieldDecimal = "decimal";
        private const string mdataFieldUUID = "uuid";
        private const string mdataFieldJSON = "json";
        private const string mdataFieldXML = "xml";

        private static string buildValueString(object value) {
            if (value is bool)
                return (bool)value ? mdataBoolTrue : mdataBoolFalse;

            // write \t as ascii `\t`
            if (value is string && "\t" == (string)value)
                return "\\t";

            return value.ToString();
        }

        private static string buildMdataString(string dataName, IEnumerable<object> values) {
            var stringValues = new List<string>();
            foreach (var valueObj in values)
                stringValues.Add(buildValueString(valueObj));
            return string.Format("{0}{1}" + mdataValueOpen + "{2}" + mdataValueClose, mdataPrefix, dataName, string.Join(mdataValueSeparator, stringValues));
        }

        private static string buildMdataString(string dataName, object value) {
            return buildMdataString(dataName, new List<object>() { value });
        }

        private static string buildRecordString(DFFDBRecord record, DFFDBTableConfig configs) {
            var dataStringLines = new List<string>();

            var renderedRecordParts = new List<string>();
            foreach(DFFDBField field in configs.Fields) {
                switch (field.Type) {
                    case DFFDBFieldType.Text:
                        renderedRecordParts.Add(record.GetFieldValue((DFFDBTextField)field));
                        break;
                }
            }
            dataStringLines.Add(string.Join(configs.FieldSeparator, renderedRecordParts));

            return string.Join(Environment.NewLine, dataStringLines);
        }

        private static string buildTableString(DFFDBTable table) {
            var dataStringLines = new List<string>();

            // build table title line
            var titleParts = new List<string>();

            titleParts.Add(medataLineStart);
            if (mdataTablePrefix != string.Empty)
                titleParts.Add(mdataTablePrefix);

            titleParts.Add(buildMdataString(mdataTable, new List<string>() { table.Name, table.Description }));
            titleParts.Add(buildMdataString(mdataTableInternal, table.IsInternal));
            titleParts.Add(buildMdataString(mdataTableHistory, table.KeepsHistory));
            titleParts.Add(buildMdataString(mdataTableFieldSeparator, table.FieldSeparator));
            titleParts.Add(buildMdataString(mdataTableTags, table.AllowsTags));

            dataStringLines.Add(string.Join(mdataSeparator, titleParts));

            // build table fields line
            var fieldParts = new List<string>();

            fieldParts.Add(medataLineStart);
            if (mdataTableFieldsPrefix != string.Empty)
                fieldParts.Add(mdataTableFieldsPrefix);

            if (table.Configs.Key != null)
                fieldParts.Add(buildMdataString(mdataPKField, table.Configs.Key.Name));
            else
                fieldParts.Add(buildMdataString(mdataPKField, mdataNotSet));

            foreach (DFFDBField field in table.Fields) {
                string typeKeyName;
                switch (field.Type) {
                    case DFFDBFieldType.Boolean: typeKeyName = mdataFieldBool; break;
                    case DFFDBFieldType.Byte: typeKeyName = mdataFieldByte; break;
                    case DFFDBFieldType.Integer: typeKeyName = mdataFieldInteger; break;
                    case DFFDBFieldType.Text: typeKeyName = mdataFieldText; break;
                    case DFFDBFieldType.Date: typeKeyName = mdataFieldDate; break;
                    case DFFDBFieldType.Time: typeKeyName = mdataFieldTime; break;
                    case DFFDBFieldType.TimeStamp: typeKeyName = mdataFieldTimeStamp; break;
                    case DFFDBFieldType.TimeStampTZ: typeKeyName = mdataFieldTimeStampTZ; break;
                    case DFFDBFieldType.Decimal: typeKeyName = mdataFieldDecimal; break;
                    case DFFDBFieldType.UUID: typeKeyName = mdataFieldUUID; break;
                    case DFFDBFieldType.JSON: typeKeyName = mdataFieldJSON; break;
                    case DFFDBFieldType.XML: typeKeyName = mdataFieldXML; break;
                    default:
                        typeKeyName = mdataFieldText; break;
                }

                fieldParts.Add(buildMdataString(typeKeyName, new List<string>() { field.Name, field.Description }));
            }

            dataStringLines.Add(string.Join(mdataSeparator, fieldParts));

            // build records
            foreach (DFFDBRecord record in table.Records)
                dataStringLines.Add(buildRecordString(record, table.Configs) + (table.KeepsHistory ? Environment.NewLine : ""));
                

            return string.Join(Environment.NewLine, dataStringLines);
        }

        private static string buildDBString(DFFDBDatabase db) {
            var dataStringLines = new List<string>();

            var lineParts = new List<string>();
            lineParts.Add(medataLineStart);
            lineParts.Add(buildMdataString(mdataDB, db.Name));
            lineParts.Add(buildMdataString(mdataDBFile, db.FilePath));
            lineParts.Add(buildMdataString(mdataDBVersion, db.Version));
            dataStringLines.Add(string.Join(mdataSeparator, lineParts) + Environment.NewLine);

            // write internal tables first
            foreach (DFFDBTable table in db.Tables.Where(t => t.IsInternal))
                dataStringLines.Add(buildTableString(table) + Environment.NewLine);

            foreach (DFFDBTable table in db.Tables.Where(t => !t.IsInternal))
                dataStringLines.Add(buildTableString(table) + Environment.NewLine);

            return string.Join(Environment.NewLine, dataStringLines);
        }

        // public
        public static string BuildDBStringRepr(DFFDBDatabase db) => buildDBString(db);
    }
}
