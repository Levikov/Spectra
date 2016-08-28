﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace Spectra
{
    public class SQLiteDatabase
    {
        String dbConnection;
        public SQLiteConnection cnn;
        SQLiteTransaction trans;

        #region ctor
        /// <summary>
        ///     Default Constructor for SQLiteDatabase Class.
        /// </summary>
        public SQLiteDatabase()
        {
            dbConnection = "Data Source=recipes.s3db";
            cnn = new SQLiteConnection(dbConnection);
        }

        /// <summary>
        ///     Single Param Constructor for specifying the DB file.
        /// </summary>
        /// <param name="inputFile">The File containing the DB</param>
        public SQLiteDatabase(String inputFile)
        {
            dbConnection = String.Format("Data Source={0}", inputFile);
            cnn = new SQLiteConnection(dbConnection);
            cnn.Open();
        }

        /// <summary>
        ///     Single Param Constructor for specifying advanced connection options.
        /// </summary>
        /// <param name="connectionOpts">A dictionary containing all desired options and their values</param>
        public SQLiteDatabase(Dictionary<String, String> connectionOpts)
        {
            String str = "";
            foreach (KeyValuePair<String, String> row in connectionOpts)
            {
                str += String.Format("{0}={1}; ", row.Key, row.Value);
            }
            str = str.Trim().Substring(0, str.Length - 1);
            dbConnection = str;
            cnn = new SQLiteConnection(dbConnection);
        }
        #endregion

        /// <summary>
        ///     Allows the programmer to run a query against the Database.
        /// </summary>
        /// <param name="sql">The SQL to run</param>
        /// <returns>A DataTable containing the result set.</returns>
        public DataTable GetDataTable(string sql)
        {
            DataTable dt = new DataTable();
            try
            {
                SQLiteConnection cnn = new SQLiteConnection(dbConnection);
                cnn.Open();
                SQLiteCommand mycommand = new SQLiteCommand("", cnn);
                mycommand.CommandText = sql;
                SQLiteDataReader reader = mycommand.ExecuteReader();
                dt.Load(reader);
                reader.Close();
                cnn.Close();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            return dt;
        }

        public DataTable GetDataTable(string sql, IList<SQLiteParameter> cmdparams)
        {
            DataTable dt = new DataTable();
            try
            {
                SQLiteConnection cnn = new SQLiteConnection(dbConnection);
                cnn.Open();
                SQLiteCommand mycommand = new SQLiteCommand("", cnn);
                mycommand.CommandText = sql;
                mycommand.Parameters.AddRange(cmdparams.ToArray());
                mycommand.CommandTimeout = 180;
                SQLiteDataReader reader = mycommand.ExecuteReader();
                dt.Load(reader);
                reader.Close();
                cnn.Close();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            return dt;
        }

        /// <summary>
        ///     Allows the programmer to interact with the database for purposes other than a query.
        /// </summary>
        /// <param name="sql">The SQL to be run.</param>
        /// <returns>An Integer containing the number of rows updated.</returns>
        public bool ExecuteNonQuery(string sql)
        {
            bool successState = false;
            cnn.Open();
            using (SQLiteTransaction mytrans = cnn.BeginTransaction())
            {
                SQLiteCommand mycommand = new SQLiteCommand(sql, cnn);
                try
                {
                    mycommand.CommandTimeout = 180;
                    mycommand.ExecuteNonQuery();
                    mytrans.Commit();
                    successState = true;
                    cnn.Close();
                }
                catch (Exception e)
                {
                    mytrans.Rollback();
                }
                finally
                {
                    mycommand.Dispose();
                    cnn.Close();
                }
            }
            return successState;
        }

        public void BeginInsert()
        {
            //cnn.Open();
            trans = cnn.BeginTransaction();

        }

        public void EndInsert()
        {
            try
            {
                trans.Commit();
            }
            catch
            {
                trans.Rollback();
            }
            finally
            {
                trans.Dispose();
                cnn.Close();
            }
        }



        public bool ExecuteNonQuery(string sql, IList<SQLiteParameter> cmdparams)
        {
            bool successState = false;
            SQLiteCommand mycommand = new SQLiteCommand(sql, cnn, trans);
            mycommand.Parameters.AddRange(cmdparams.ToArray());
            mycommand.CommandTimeout = 180;
            mycommand.ExecuteNonQuery();
            successState = true;
            return successState;
        }

        /// <summary>
        ///     暂时用不到
        ///     Allows the programmer to retrieve single items from the DB.
        /// </summary>
        /// <param name="sql">The query to run.</param>
        /// <returns>A string.</returns>
        public object ExecuteScalar(string sql)
        {
            SQLiteCommand mycommand = new SQLiteCommand("", cnn);
            mycommand.CommandText = sql;
            object value = mycommand.ExecuteScalar();
            return value;
        }

        /// <summary>
        ///     Allows the programmer to easily update rows in the DB.
        /// </summary>
        /// <param name="tableName">The table to update.</param>
        /// <param name="data">A dictionary containing Column names and their new values.</param>
        /// <param name="where">The where clause for the update statement.</param>
        /// <returns>A boolean true or false to signify success or failure.</returns>
        public bool Update(String tableName, Dictionary<String, String> data, String where)
        {
            String vals = "";
            Boolean returnCode = true;
            if (data.Count >= 1)
            {
                foreach (KeyValuePair<String, String> val in data)
                {
                    vals += String.Format(" {0} = '{1}',", val.Key.ToString(), val.Value.ToString());
                }
                vals = vals.Substring(0, vals.Length - 1);
            }
            try
            {
                this.ExecuteNonQuery(String.Format("update {0} set {1} where {2};", tableName, vals, where));
            }
            catch
            {
                returnCode = false;
            }
            return returnCode;
        }
    }

    public static class SQLiteFunc
    {

        #region 查询数据库、插入数据库
        /*查询一行数据*/
        public static DataTable SelectDTSQL(string sql)
        {
            try
            {
                SQLiteConnection conn = new SQLiteConnection(Variables.dbConString);
                conn.Open();
                SQLiteCommand cmmd = new SQLiteCommand("", conn);
                cmmd.CommandText = sql;
                SQLiteDataAdapter adapter = new SQLiteDataAdapter(cmmd);
                DataTable data = new DataTable();
                adapter.Fill(data);
                conn.Close();
                return data;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        /*执行SQL语句*/
        /*调用的时候类似：ExcuteSQL("insert into xx (id,name) values (?,'?')", id, name);*/
        /*update 表名 set 字段1=text1，字段2=text2，字段3=text3 where 条件*/
        public static void ExcuteSQL(string sql, params object[] value)
        {
            try
            {
                SQLiteConnection conn = new SQLiteConnection(Variables.dbConString);
                conn.Open();
                SQLiteCommand cmmd = new SQLiteCommand("", conn);
                cmmd.CommandText = sql;
                if (value != null && value.Length > 0)
                {
                    string[] temp = sql.Split('?');
                    int i = 0;
                    sql = temp[i];
                    for (i = 0; i < value.Length; i++)
                    {
                        sql += value[i];
                        sql += temp[i + 1];
                    }
                    cmmd.CommandText = sql;
                }
                cmmd.ExecuteNonQuery();
                conn.Close();
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        /*插入错误信息*/
        public static void insertFileErrors(string filePath,string md5str, long errorPosi, string errorType)
        {
            string fileName = filePath.Substring(filePath.LastIndexOf('\\') + 1);
            if (SelectDTSQL("SELECT * from FileErrors where MD5='" + md5str + "' and 错误位置=" + errorPosi.ToString() + " and 错误类型='" + errorType + "'").Rows.Count == 0)
                ExcuteSQL("insert into FileErrors (文件路径,文件名,错误位置,错误类型,MD5) values ('?','?','?','?','?')", filePath, fileName, errorPosi, errorType,md5str);
        }
        #endregion
    }
}
