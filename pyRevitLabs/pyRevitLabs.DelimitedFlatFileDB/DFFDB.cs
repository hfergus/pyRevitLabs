using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pyRevitLabs.DelimitedFlatFileDB
{
    public static class DFFDB
    {
        // database functions
        // CREATE
        public static DFFDBDatabase CreateDB(string db_filePath, string db_name = "", Encoding encoding = null) {
            return new DFFDBDatabase(db_filePath, db_name, encoding);
        }

        // DELETE
        public static bool DropDB(DFFDBDatabase db) {
            // TODO: implement delete db
            return false;
        }

    }
}
