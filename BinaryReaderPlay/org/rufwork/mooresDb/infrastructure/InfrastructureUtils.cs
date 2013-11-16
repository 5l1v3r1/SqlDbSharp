﻿// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.mooresDb.infrastructure.tableParts;
using System.Data;
using org.rufwork.mooresDb.infrastructure.contexts;

namespace org.rufwork.mooresDb.infrastructure
{
    public class InfrastructureUtils
    {

        public static StringComparison caseSetting = StringComparison.CurrentCultureIgnoreCase;

        public static string dataTableToString(DataTable dtIn)
        {
            string strReturn = "";
            int[] aintColLength = new int[dtIn.Columns.Count];

            for (int i = 0; i < dtIn.Columns.Count; i++)
            {
                DataColumn dc = dtIn.Columns[i];
                aintColLength[i] = dc.ColumnName.Length;    // start with a minimum display length of the column name.

                // Figure out how many characters might be in this column
                // for the current data.  I'm going to ignore performance for now.

                foreach (DataRow dr in dtIn.Rows)
                {
                    if (dr[dc].ToString().Length > aintColLength[i])
                    {
                        aintColLength[i] = dr[dc].ToString().Length;
                    }
                }

                if (dc.ColumnName.Length > aintColLength[i])
                {
                    strReturn += dc.ColumnName.Substring(0, aintColLength[i]) + " | ";
                }
                else
                {
                    strReturn += dc.ColumnName.PadRight(aintColLength[i]) + " | ";
                }
            }
            strReturn += System.Environment.NewLine;

            foreach (DataRow dr in dtIn.Rows)
            {
                // TODO: Is foreach Column order guaranteed?
                for (int i = 0; i < dtIn.Columns.Count; i++)
                {
                    strReturn += dr[dtIn.Columns[i]].ToString().PadLeft(aintColLength[i]) + " # ";
                }
                strReturn += System.Environment.NewLine;
            }

            return strReturn;
        }

        // I could do this with DataSets and DataRelations, but this 
        // is more straightfoward for now, I think.
        static public DataTable equijoinTables(DataTable dt1, DataTable dt2, 
            TableContext tblContext1, TableContext tblContext2,
            string strDt1JoinColName, string strDt2JoinColName
        )
        {
            DataTable dtReturn = new DataTable();
            Dictionary<string, string> dictTable2ColAliases = new Dictionary<string,string>();

            // We could count on the programmer having already done this conversion
            // from menumonic to raw column name, but it's non-destructive to, at worst,
            // do it again here.
            strDt1JoinColName = tblContext1.getRawColName(strDt1JoinColName);
            strDt2JoinColName = tblContext2.getRawColName(strDt2JoinColName);

            if (null == strDt1JoinColName || null == strDt2JoinColName)
            {
                throw new Exception("Column name not found in join: " + dt1.TableName + " :: " + dt2.TableName);
            }

            // Make sure the two join columns are of the same type before starting.
            if (!dt1.Columns[strDt1JoinColName].DataType.Equals(dt2.Columns[strDt2JoinColName].DataType))
            {
                throw new Exception("DataTable join column data types do not match: "
                    + dt1.TableName + "." + strDt1JoinColName
                    + " :: "
                    + dt2.TableName + "." + strDt2JoinColName);
            }

            foreach (DataColumn dc in dt1.Columns)
            {
                DataColumn colForDt = new DataColumn(dc.ColumnName);
                colForDt.DataType = dc.DataType;
                dtReturn.Columns.Add(colForDt);
            }

            foreach (DataColumn dc in dt2.Columns)
            {
                string strColName = dc.ColumnName;

                // Check for names duplicated in both tables.
                // Alias dt2's if there's a conflict.
                if (dtReturn.Columns.Contains(strColName))
                {
                    // TODO: Of course this misses "Table2.Name' (inclusive of "Table2" -- see?  Crazy outlier.) as a column name for Table 1.
                    // We'll risk that for now.  It really shouldn't happen.
                    strColName = dt2.TableName + "." + strColName;
                    dictTable2ColAliases.Add(dc.ColumnName, strColName);
                }
                DataColumn colForDt = new DataColumn(strColName);
                colForDt.DataType = dc.DataType;
                dtReturn.Columns.Add(colForDt);
            }

            // Moore's Comparison it is.  TODO: Make it isn't. [sic]
            foreach (DataRow dr in dt1.Rows)
            {
                foreach (DataRow dr2 in dt2.Rows)
                {
                    if (dr[strDt1JoinColName].Equals(dr2[strDt2JoinColName]))
                    {
                        DataRow rowNew = dtReturn.NewRow();
                        // Copy the values from both rows into dtReturn.
                        foreach (DataColumn dc1 in dt1.Columns)
                        {
                            rowNew[dc1.ColumnName] = dr[dc1];
                        }

                        foreach (DataColumn dc2 in dt2.Columns)
                        {
                            string strColName = dc2.ColumnName;
                            if (dictTable2ColAliases.ContainsKey(strColName))
                            {
                                strColName = dictTable2ColAliases[strColName];
                            }

                            rowNew[strColName] = dr2[dc2];
                        }
                        dtReturn.Rows.Add(rowNew);
                    }
                }
            }

            return dtReturn;
        }

        /// <summary>
        /// Returns which COLUMN_TYPES enum value matches this type name.
        /// Eg, translates CHAR or VARCHAR into COLUMN_TYPES.SINGLE_CHAR 
        /// or COLUMN_TYPES.CHAR.
        /// </summary>
        /// <param name="strTypeName">The column type from the SQL</param>
        /// <param name="isSingleByteLength">True if it's a byte or SINGLE_CHAR, etc.</param>
        /// <returns></returns>
        static public COLUMN_TYPES? colTypeFromString(string strTypeName, bool isSingleByteLength, string strModifier)
        {
            COLUMN_TYPES? colType = null;

            if (StringComparison.CurrentCultureIgnoreCase == InfrastructureUtils.caseSetting) strTypeName = strTypeName.ToUpper();

            switch (strTypeName)
            {
                case "CHAR":
                case "VARCHAR":
                    if (isSingleByteLength)
                    {
                        colType = COLUMN_TYPES.SINGLE_CHAR;
                    }
                    else
                    {
                        colType = COLUMN_TYPES.CHAR;
                    }
                    break;

                case "INT":
                case "INTEGER":
                    if ("AUTO_INCREMENT".Equals(strModifier, InfrastructureUtils.caseSetting) 
                        || "AUTOINCREMENT".Equals(strModifier, InfrastructureUtils.caseSetting))
                    {
                        colType = COLUMN_TYPES.AUTOINCREMENT;
                    }
                    else if (isSingleByteLength)
                    {
                        colType = COLUMN_TYPES.BYTE;
                    }
                    else
                    {
                        colType = COLUMN_TYPES.INT;
                    }
                    break;

                case "FLOAT":
                case "DECIMAL": // so that's not really accurate.
                case "REAL":
                    colType = COLUMN_TYPES.FLOAT;
                    break;

                case "DATE":
                case "DATETIME":
                    colType = COLUMN_TYPES.DATETIME;
                    break;

                default:
                    throw new Exception("Illegal column type: " + strTypeName);
            }

            return colType;
        }
    }
}
