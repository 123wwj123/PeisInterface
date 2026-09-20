using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;
using System.Collections;
using Oracle.ManagedDataAccess.Client;
using System.Data.OleDb;
using System.IO;
using System.Reflection.Emit;
//功能：实现数据库Command对象
//程序：陈玉强
//日期：2013-04-01

namespace zlSoft.Common
{
    public class zlCommand
    {

        //oracle数据库对象
        private static OracleCommand ora_cmd = new OracleCommand();
        private static OracleConnection ora_cn;
        private static OracleTransaction ora_st;

        //sqlServer数据库对象
        private static SqlCommand sql_cmd = new SqlCommand();
        private static SqlConnection sql_cn;
        private static SqlTransaction sql_st;

        #region zlCommand 对象的构造函数
        /// <summary>
        /// BTCommand对象的构造函数。
        /// </summary>
        /// <param name="strDataType">要连接的数据库类型，ORACLE、SQLSERVER</param>
        /// <param name="strSql">执行的SQL语句</param>
        public zlCommand(string strDataType,string strConn, string strSql)
        {
            //string strConn = zlConfiguration.GetAppSetting(strDataType);
            switch (strDataType)
            {
                case "SqlServer":
                    sql_cn = new SqlConnection(strConn);
                    sql_cn.Open();
                    sql_cmd = new SqlCommand(strSql, sql_cn);
                    break;
                case "Oracle":
                    ora_cn = new OracleConnection(strConn);
                    ora_cn.Open();
                    ora_cmd = new OracleCommand(strSql, ora_cn);
                    break;
            }
        }

        #endregion BTCommand 对象的构造函数

        #region 按顺序取得下一个编号

        /// <summary>
        /// 按顺序取得下一个编号。
        /// </summary>
        /// <param name="strDataType">数据库类型</param>
        /// <param name="TableName">表的名称</param>
        /// <param name="FieldName">要获得编号的列</param>
        /// <returns>返回整数编号</returns>
        public int GetNextvalID(string strDataType,string TableName, string FieldName)
        {
            int nextid = 0;
            switch (strDataType)
            {
                case "SqlServer":
                    string sql = "select max(" + FieldName + ") as maxid from " + TableName;
                    sql_cmd = new SqlCommand(sql, sql_cn);
                    System.Data.SqlClient.SqlDataReader dr = sql_cmd.ExecuteReader();
                    if (dr.Read())
                    {
                        if (dr["maxid"] != null)
                            if (!System.DBNull.Equals(dr["maxid"], System.DBNull.Value))
                                nextid = (int)dr["maxid"];
                    }
                    nextid += 1;
                    dr.Close();
                    sql_cmd.Dispose();
                    break;
                case "Oracle":
                    if (FieldName.Length > 0)
                    {
                        OracleCommand tempcmd = new OracleCommand("select " + FieldName + ".nextval from dual", ora_cn);
                        OracleDataReader m_ora_sdr = tempcmd.ExecuteReader();
                        m_ora_sdr.Read();
                        nextid = int.Parse(m_ora_sdr[0].ToString());
                        m_ora_sdr.Close();
                        tempcmd.Dispose();
                    }
                    break;
            }
            return nextid;
        }

        #endregion 按顺序取得下一个编号

        #region 获取Command对象

        public string GetCommandText()
        {
            return ora_cmd.CommandText;
        }

        /// <summary>
        /// 返回zlCommand内部的OracleCommand
        /// </summary>
        /// <returns></returns>

        public SqlCommand GetSqlCommand()
        {
            return sql_cmd;
        }

        public OracleCommand GetOracleCommand()
        {
            return ora_cmd;
        }
        #endregion 获取Command对象

        #region clear 清除所有参数

        /// <summary>
        /// 清除所有参数，重新初始化BTCommand对象。
        /// </summary>
        /// <param name="strDataType">数据库类型</param>
        public void clear(string strDataType)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    sql_cmd.Parameters.Clear();
                    break;
                case "Oracle":
                    ora_cmd.Parameters.Clear();
                    break;
            }
        }

        #endregion clear 清除所有参数

        #region SetParameters 参数设置

        /// <summary>
        /// 设置参数
        /// </summary>
        /// <param name="strDataType">数据库类型</param>
        /// <param name="name">参数的名称</param>
        /// <param name="nvalue">参数的值</param>
        private void SetParameters(string strDataType, string name, object nvalue)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    SqlParameter sqlPara1 = new SqlParameter();
                    sqlPara1.ParameterName = name;
                    if (nvalue == null || nvalue == DBNull.Value)
                    {
                        sqlPara1.Value = DBNull.Value;
                        sql_cmd.Parameters.Add(sqlPara1);
                        return;
                    }

                    SqlDbType sdt = new SqlDbType();
                    switch (nvalue.GetType().Name)
                    {
                        case "String":
                            sdt = SqlDbType.VarChar;
                            break;
                        case "DateTime":
                            sdt = SqlDbType.DateTime;
                            break;
                        case "Int16":
                            sdt = SqlDbType.SmallInt;
                            break;
                        case "Int32":
                            sdt = SqlDbType.Int;
                            break;
                        case "Int64":
                            sdt = SqlDbType.BigInt;
                            break;
                        case "Double":
                            sdt = SqlDbType.Float;
                            break;
                        case "Single":
                            sdt = SqlDbType.Real;
                            break;
                        case "Boolean":
                            sdt = SqlDbType.VarChar;
                            if (nvalue.ToString() == "True")
                                nvalue = 1;
                            else
                                nvalue = 0;
                            break;

                    }
                    SqlParameter sqlPara = new SqlParameter("@"+name,sdt);
                    sqlPara.Value = nvalue;
                    if (nvalue.ToString().Length > 4000)
                        sqlPara.SqlDbType = SqlDbType.Text;
                    sql_cmd.Parameters.Add(sqlPara);

                    //if (nvalue.ToString().Length < 4000)
                    //{
                    //    sql_cmd.Parameters.Add(name, nvalue);
                    //}
                    //else
                    //{
                    //    SqlParameter param = new SqlParameter();
                    //    param.ParameterName = name;
                    //    param.Value = nvalue;
                    //    param.SqlDbType = SqlDbType.Text;

                    //    sql_cmd.Parameters.Add(param);
                    //}
                    break;
                case "Oracle":
                    OracleDbType odt = new OracleDbType();
                    switch (nvalue.GetType().Name)
                    {
                        case "String":
                            odt = OracleDbType.Varchar2;
                            break;
                        case "DateTime":
                            odt = OracleDbType.Date;
                            break;
                        case "Int16":
                            odt = OracleDbType.Int16;
                            break;
                        case "Int32":
                            odt = OracleDbType.Int32;
                            break;
                        case "Int64":
                            odt = OracleDbType.Int64;
                            break;
                        case "Double":
                            odt = OracleDbType.Double;
                            break;
                        case "Single":
                            odt = OracleDbType.Int32;
                            break;
                        case "Boolean":
                            odt = OracleDbType.Varchar2;
                            if (nvalue.ToString() == "True")
                                nvalue = 1;
                            else
                                nvalue = 0;
                            break;

                    }
                    OracleParameter oraPara = new OracleParameter(name, odt);
                    oraPara.Value = nvalue;
                    if (nvalue.ToString().Length > 4000)
                        oraPara.OracleDbType = OracleDbType.Clob;
                    ora_cmd.Parameters.Add(oraPara);
                    break;
            }
        }

        #endregion SetParameters 参数设置

        #region SetSQL 设置BTCommand对象的SQL语句

        /// <summary>
        /// 设置BTCommand对象的SQL语句。
        /// </summary>
        /// <param name="strDataType">数据库类型</param>
        /// <param name="appsql"></param>
        public void SetSQL(string strDataType,string appsql, params object[] par)
        {
            clear(strDataType);
            string strSQL = appsql;
            string strFind;
            string strLog;
            string strSeq;
            string strPar = "";

            int intLeft;
            int intRight;
            int I;
            int intMax = 0;
            object varValue;
            object[] objPara = par;
            //为了编码方便，相同参数的 序号 也相同，在处理sql语句时重新编码参数序号
            List<int> listReq = new List<int>();

            switch (strDataType)
            {
                case "SqlServer":
                    //分析自定的[x]参数
                    intLeft = strSQL.IndexOf("[");
                    while (intLeft > 0)
                    {
                        intRight = strSQL.IndexOf("]", intLeft + 1);
                        //可能是正常的"[编码]名称"字段名
                        strSeq = strSQL.Substring(intLeft + 1, intRight - intLeft - 1);
                        if (zlFunc.IsNumeric(strSeq))
                        {
                            intMax += 1;
                            I = Convert.ToInt32(strSeq);
                            strPar = strPar + "," + I.ToString();
                            listReq.Add(I - 1);
                            strSQL = strSQL.Substring(0, intLeft + 1) + intMax + strSQL.Substring(intRight);
                        }
                        intLeft = strSQL.IndexOf("[", intRight + 1);
                    }

                    //替换为"?"参数
                    strFind = strSQL;
                    for (I = 1; I <= intMax; I++)
                    {
                        strFind = strFind.Replace("[" + I + "]", "@PAR" + I.ToString());
                    }

                    //创建新的参数
                    for (I = 1; I <= intMax; I++)
                    {
                        varValue = objPara[listReq[I - 1]];
                        SetParameters(strDataType, "PAR" + I, varValue);
                    }
                    sql_cmd.CommandText = strFind;
                    break;
                case "Oracle":
                    ora_cmd.CommandType = CommandType.Text;
                    //分析自定的[x]参数
                    intLeft = strSQL.IndexOf("[");
                    while (intLeft > 0)
                    {
                        intRight = strSQL.IndexOf("]", intLeft + 1);
                        //可能是正常的"[编码]名称"字段名
                        strSeq = strSQL.Substring(intLeft + 1, intRight - intLeft - 1);
                        if (zlFunc.IsNumeric(strSeq))
                        {
                            intMax += 1;
                            I = Convert.ToInt32(strSeq);
                            strPar = strPar + "," + I.ToString();
                            listReq.Add(I - 1);
                            strSQL = strSQL.Substring(0, intLeft + 1) + intMax + strSQL.Substring(intRight);
                        }
                        intLeft = strSQL.IndexOf("[", intRight + 1);
                    }

                    //替换为"?"参数
                    strLog = strSQL;
                    strFind = strSQL;
                    for (I = 1; I <= intMax; I++)
                    {
                        strFind = strFind.Replace("[" + I + "]", ":PAR" + I.ToString());
                        //产生用于SQL跟踪的语句
                        varValue = objPara[listReq[I - 1]];
                        switch (varValue.GetType().Name)
                        {
                            case "String":
                                strLog = strLog.Replace("[" + I + "]", "'" + varValue.ToString().Replace("'", "''") + "'");
                                break;
                            case "DateTime":
                                strLog = strLog.Replace("[" + I + "]", "To_Date('" + string.Format("{0:yyyy-MM-dd HH:mm:ss}", varValue.ToString()) + "','YYYY-MM-DD HH24:MI:SS')");
                                break;
                            case "Int16":
                                strLog = strLog.Replace("[" + I + "]", varValue.ToString());
                                break;
                            case "Int32":
                                strLog = strLog.Replace("[" + I + "]", varValue.ToString());
                                break;
                            case "Int64":
                                strLog = strLog.Replace("[" + I + "]", varValue.ToString());
                                break;
                            case "Double":
                                strLog = strLog.Replace("[" + I + "]", varValue.ToString());
                                break;
                            case "Single":
                                strLog = strLog.Replace("[" + I + "]", varValue.ToString());
                                break;
                            case "Boolean":
                                strLog = strLog.Replace("[" + I + "]", "'" + (varValue.ToString() == "True" ? "1" : "0") + "'");
                                break;
                            default:
                                break;
                        }
                    }
                    //创建新的参数
                    for (I = 1; I <= intMax; I++)
                    {
                        varValue = objPara[listReq[I - 1]];
                        SetParameters(strDataType,"PAR" + I, varValue);
                    }
                    ora_cmd.CommandText = strFind;
                    break;
                default:
                    break;
            }

        }

        #endregion SetSQL 设置BTCommand对象的SQL语句

        #region RunProcedure 执行过程

        public string RunProcedure(string strName, OracleDbType oleDt, params object[] par)
        {
            int m;
            string strOut = "";
            object[] objPara = par;
            OracleParameter[] oraPara = new OracleParameter[objPara.Length];
            //定义执行类型为存储过程，并赋值存储过程名
            ora_cmd.CommandType = CommandType.StoredProcedure;
            ora_cmd.CommandText = strName;
            ora_cmd.Parameters.Clear();
            //添加存储过程参数
            for (m = 0; m < objPara.Length; m++)
            {
                switch (objPara[m].GetType().Name)
                {
                    case "String":
                        if (objPara[m].ToString().Contains("</"))
                        {
                            oraPara[m] = new OracleParameter(m.ToString().PadLeft(3, '0'), OracleDbType.XmlType);
                            oraPara[m].Value = objPara[m];
                        }
                        else if (objPara[m].ToString() == "Out_Type")
                        {
                            oraPara[m] = new OracleParameter(m.ToString().PadLeft(3, '0'), oleDt);
                            oraPara[m].Value = new string(' ', 2000); //ParameterDirection.Output;
                            strOut = m.ToString().PadLeft(3, '0');
                        }
                        else
                        {
                            oraPara[m] = new OracleParameter(m.ToString().PadLeft(3, '0'), OracleDbType.Varchar2);
                            oraPara[m].Value = objPara[m];
                        }
                        break;
                    case "DateTime":
                        oraPara[m] = new OracleParameter(m.ToString().PadLeft(3, '0'), OracleDbType.Date);
                        if (!objPara[m].Equals(Convert.ToDateTime("2000-01-01")))
                            oraPara[m].Value = objPara[m];
                        break;
                    case "Int16":
                        oraPara[m] = new OracleParameter(m.ToString().PadLeft(3, '0'), OracleDbType.Int16);
                        oraPara[m].Value = objPara[m];
                        break;
                    case "Int32":
                        oraPara[m] = new OracleParameter(m.ToString().PadLeft(3, '0'), OracleDbType.Int32);
                        oraPara[m].Value = objPara[m];
                        break;
                    case "Int64":
                        oraPara[m] = new OracleParameter(m.ToString().PadLeft(3, '0'), OracleDbType.Int64);
                        oraPara[m].Value = objPara[m];
                        break;
                    case "Double":
                        oraPara[m] = new OracleParameter(m.ToString().PadLeft(3, '0'), OracleDbType.Double);
                        oraPara[m].Value = objPara[m];
                        break;
                    case "Single":
                        oraPara[m] = new OracleParameter(m.ToString().PadLeft(3, '0'), OracleDbType.Single);
                        oraPara[m].Value = objPara[m];
                        break;
                    default:
                        break;
                }
                ora_cmd.Parameters.Add(oraPara[m]);
            }
            //执行过程
            ora_cmd.ExecuteNonQuery();
            //如果有出参，获取出参值
            if (!string.IsNullOrEmpty(strOut))
            {
                if (oleDt == OracleDbType.XmlType)
                {
                    //strOut = ((Oracle.ManagedDataAccess.Types.oraclex)ora_cmd.Parameters[strOut].Value).Value;
                }
                else
                {
                    strOut = ((Oracle.ManagedDataAccess.Types.OracleString)ora_cmd.Parameters[strOut].Value).Value;
                }
            }
            ora_cmd.CommandType = CommandType.Text;
            return strOut;
        }

        #endregion

        #region RunProcedurePar 执行过程(参数已编制)

        public string RunProcedurePar(string strName,  OracleParameter[] par)
        {
            //定义执行类型为存储过程，并赋值存储过程名
            ora_cmd.CommandType = CommandType.StoredProcedure;
            ora_cmd.CommandText = strName;
            ora_cmd.Parameters.Clear();

            for (int i = 0; i < par.Length; i++)
            {
                ora_cmd.Parameters.Add(par[i]);
            }
            //执行过程
            ora_cmd.ExecuteNonQuery();
            ora_cmd.CommandType = CommandType.Text;
            return "";
        }

        public void UpdateCommandType()
        {
            if (ora_cmd.CommandType == CommandType.StoredProcedure)
                ora_cmd.CommandType = CommandType.Text;
        }

        #endregion

        #region ExecuteNonQuery 执行非查询的SQL语句。它不返回任何结果集。

        /// <summary>
        /// 执行非查询的SQL语句。它不返回任何结果集。
        /// </summary>
        /// <returns>返回所影响的记录的行数。</returns>

        public int ExecuteNonQuery(string strDataType)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    return sql_cmd.ExecuteNonQuery();
                case "Oracle":
                    return ora_cmd.ExecuteNonQuery();
                default:
                    return ora_cmd.ExecuteNonQuery();
            }
        }

        #endregion ExecuteNonQuery 执行非查询的SQL语句。它不返回任何结果集。

        #region ExecuteScalar 执行查询的SQL语句。并且返回首行首列的值。

        /// <summary>
        /// 执行查询的SQL语句。并且返回首行首列的值。
        /// </summary>
        /// <param name="strDataType">数据库类型</param>
        /// <returns>返回首行首列的值。</returns>

        public object ExecuteScalar(string strDataType)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    return sql_cmd.ExecuteScalar();
                case "Oracle":
                    return ora_cmd.ExecuteScalar();
                default:
                    return ora_cmd.ExecuteScalar();
            }
            
        }

        #endregion ExecuteNonQuery 执行非查询的SQL语句。它不返回任何结果集。

        #region ExecuteReader 执行对数据集的读操作

        /// <summary>
        /// 执行对数据集的读操作。
        /// </summary>
        /// <returns>返回SqlDataReader或OleDbDataReader对象。</returns>
        public object ExecuteReader(string strDataType)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    return sql_cmd.ExecuteReader();
                case "Oracle":
                    return ora_cmd.ExecuteReader();
                default:
                    return ora_cmd.ExecuteReader();
            }
        }

        #endregion ExecuteReader 执行对数据集的读操作

        #region Close 关闭BTCommand对象，关释放所有资源。

        /// <summary>
        /// 关闭BTCommand对象，关释放所有资源。
        /// </summary>
        public void Close(string strDataType)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    sql_cmd.Connection.Close();
                    sql_cmd.Connection.Dispose();
                    break;
                case "Oracle":
                    ora_cmd.Connection.Close();
                    ora_cmd.Connection.Dispose();
                    break;
            }
        }

        #endregion Close 关闭BTCommand对象，关释放所有资源。

        #region BeginTransaction 在BTCommand对象上开始一个新的事务。

        /// <summary>
        /// 在BTCommand对象上开始一个新的事务。
        /// </summary>
        public void BeginTrans(string strDataType)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    sql_st = sql_cn.BeginTransaction();
                    sql_cmd.Transaction = sql_st;
                    break;
                case "Oracle":
                    ora_st = ora_cn.BeginTransaction();
                    ora_cmd.Transaction = ora_st;
                    break;
            }
        }

        #endregion BeginTrans 在BTCommand对象上开始一个新的事务。

        #region CommitTrans 提交BTCommand对象上的事务。

        /// <summary>
        /// 提交BTCommand对象上的事务。
        /// </summary>

        public void CommitTrans(string strDataType)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    sql_st.Commit();
                    break;
                case "Oracle":
                    ora_st.Commit();
                    break;
            }
        }

        #endregion CommitTrans 提交BTCommand对象上的事务。

        #region RollbackTrans 回退BTCommand对象上的事务。

        /// <summary>
        /// 回退BTCommand对象上的事务。
        /// </summary>
        public void RollbackTrans(string strDataType)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    sql_st.Rollback();
                    break;
                case "Oracle":
                    ora_st.Rollback();
                    break;
            }
        }

        #endregion RollbackTrans 回退BTCommand对象上的事务。

        #region GetDataTable 获取一个DataTable对象

        /// <summary>
        /// 返回一个DataTable对象
        /// </summary>
        /// <param name="CommandText">执行的SQL语句</param>
        /// <returns></returns>
        public static DataTable GetDataTable(string strDataType,string CommandText)
        {
            return zlCommand.GetDataTable(strDataType,CommandText, "", CommandType.Text);
        }
        /// <summary>
        /// 返回一个DataTable对象
        /// </summary>
        /// <param name="CommandText">执行的SQL语句</param>
        /// <param name="ConnString">WebConfig中配置的连接串键值</param>
        /// <returns></returns>
        public static DataTable GetDataTable(string strDataType,string CommandText, string ConnString)
        {
            return zlCommand.GetDataTable(strDataType,CommandText, ConnString, CommandType.Text);
        }
        /// <summary>
        /// 返回一个DataTable对象
        /// </summary>
        /// <param name="CommandText">执行的SQL语句</param>
        /// <param name="ConnString">WebConfig中配置的连接串键值</param>
        /// <param name="par">扩展参数</param>
        /// <returns></returns>
        public static DataTable GetDataTable(string strDataType,string CommandText, string ConnString, params object[] par)
        {
            DataTable result = new DataTable();
            try
            {
                zlCommand cmd = new zlCommand(strDataType,ConnString,"");
                cmd.SetSQL(strDataType, CommandText, par);

                using (OracleDataAdapter da = new OracleDataAdapter(cmd.GetOracleCommand()))
                {
                    da.Fill(result);
                }
                cmd.Close(strDataType);
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        #endregion GetDataTable 获取一个DataTable对象

    }
}
