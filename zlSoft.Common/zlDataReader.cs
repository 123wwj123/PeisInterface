using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;
using System.Collections;
using Oracle.ManagedDataAccess.Client;
using System.Data.OleDb;
//功能：实现数据集架构
//程序：陈玉强
//日期：2013-04-01

namespace zlSoft.Common
{
    public class zlDataReader
    {
        private SqlDataReader m_sql_sdr;
        private OracleDataReader m_ora_sdr;

        /// <summary>
        /// BTDataReader的构造函数。
        /// </summary>

        public int FieldCount(string strDataType)
        {
            int fieldCount = 0;
            switch (strDataType)
            {
                case "SqlServer":
                    fieldCount = m_sql_sdr.FieldCount;
                    break;
                case "Oracle":
                    fieldCount = m_ora_sdr.FieldCount;
                    break;
            }
            return fieldCount;
        }

        /// <summary>
        /// 返回BTDataReade内部的SqlDataReader
        /// </summary>
        /// <returns></returns>

        public SqlDataReader GetSqlDataReader()
        {
            return m_sql_sdr;
        }

        /// <summary>
        /// 返回BTDataReade内部的OracleDataReader
        /// </summary>
        /// <returns></returns>

        public OracleDataReader GetOracleDataReader()
        {
            return m_ora_sdr;
        }

        /// <summary>
        /// 设置BTDataReader的数据对象。
        /// </summary>
        /// <param name="sdr">SqlDataReader或OleDbDataReader对象。</param>

        public void SetReader(string strDataType,object sdr)
        {
            Close(strDataType);
            switch (strDataType)
            {
                case "SqlServer":
                    m_sql_sdr = (SqlDataReader)sdr;
                    break;
                case "Oracle":
                    m_ora_sdr = (OracleDataReader)sdr;
                    break;
            }
        }

        /// <summary>
        /// 从数据集中读取下一行数据。
        /// </summary>
        /// <returns>返回表示是否读取成功的bool值。</returns>
        public bool Read(string strDataType)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    return m_sql_sdr.Read();
                case "Oracle":
                    return m_ora_sdr.Read();
                default:
                    return m_sql_sdr.Read();
            }
        }

        /// <summary>
        /// 返回记录集是否为空
        /// </summary>
        /// <returns>返回表示是否读取成功的bool值</returns>
        public Boolean HasRows(string strDataType)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    return m_sql_sdr.HasRows;
                case "Oracle":
                    return m_ora_sdr.HasRows;
                default:
                    return m_sql_sdr.HasRows;
            }
        }

        /// <summary>
        /// 关闭BTDataReader对象并释放所有资源。
        /// </summary>
        public void Close(string strDataType)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    if (m_sql_sdr != null)
                        m_sql_sdr.Close();
                    break;
                case "Oracle":
                    if (m_ora_sdr != null)
                        m_ora_sdr.Close();
                    break;
            }
        }

        /// <summary>
        /// 从当前行中取出某个字段的值。
        /// </summary>
        /// <param name="name">字段名称。</param>
        /// <returns>表示字段的值的对象。</returns>
        public object Get(string strDataType,string name)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    return m_sql_sdr[name];
                case "Oracle":
                    if (m_ora_sdr[name].GetType().Name == "Decimal")
                    {
                        if (m_ora_sdr[name].ToString().IndexOf(".", 0) < 0)
                        {
                            return Int64.Parse(m_ora_sdr[name].ToString());
                        }
                        else
                        {
                            return m_ora_sdr[name];
                        }
                    }
                    else
                    {
                        return m_ora_sdr[name];
                    }
                default:
                    return m_sql_sdr[name];
            }
        }

        /// <summary>
        /// 从当前行中取出某个字段的值。
        /// </summary>
        /// <param name="index">字段的编号。</param>
        /// <returns>表示字段的值的对象。</returns>
        public object Get(string strDataType,int index)
        {
            switch (strDataType)
            {
                case "SqlServer":
                    return m_sql_sdr[index];
                case "Oracle":
                    if (m_ora_sdr[index].GetType().Name == "Decimal")
                    {
                        if (m_ora_sdr[index].ToString().IndexOf(".", 0) < 0)
                        {
                            return int.Parse(m_ora_sdr[index].ToString());
                        }
                        else
                        {
                            return m_ora_sdr[index];
                        }
                    }
                    else
                    {
                        return m_ora_sdr[index];
                    }
                default:
                    return m_sql_sdr[index];
            }
        }
    }
}
