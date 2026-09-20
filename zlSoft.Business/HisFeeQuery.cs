using System;
using System.Collections.Generic;
using Npgsql;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// HIS（东昉）库费用查询。
    /// 退费时体检侧传回的仍是收费时的 settleCode（即 HIS 的登记流水号 waibujklsh），
    /// 需要按该流水号到 HIS 库反查 feiYongId / feiYongMxId，作为调用退费接口的参数。
    /// </summary>
    public class HisFeeQuery
    {
        /// <summary>HIS费用ID与费用明细ID</summary>
        public class HisFeeId
        {
            public string FeiYongId { get; set; }
            public string FeiYongMxId { get; set; }
        }

        /// <summary>
        /// 按外部接口流水号（waibujklsh = 收费时的 settleCode）查询HIS费用ID/费用明细ID。
        /// 一条费用可能存在多行明细，按行返回。
        /// </summary>
        public static List<HisFeeId> GetFeeIdList(string settleCode)
        {
            var list = new List<HisFeeId>();
            if (string.IsNullOrEmpty(settleCode))
            {
                return list;
            }

            string strConn = "";
            try
            {
                strConn = zlConfiguration.GetAppSetting("His");
            }
            catch
            {
                strConn = "";
            }

            if (string.IsNullOrEmpty(strConn))
            {
                throw new Exception("未配置HIS数据库连接串【His】，无法查询费用信息");
            }

            string strSql = @"select a.feiyongid, b.feiyongmxid
                                 from df_jj_menzhen.mz_feiyong1 a
                                 left join df_jj_menzhen.mz_feiyong2 b on a.feiyongid = b.feiyongid
                                where b.waibujklsh = @settleCode";

            using (NpgsqlConnection cn = new NpgsqlConnection(strConn))
            {
                cn.Open();
                using (NpgsqlCommand cmd = new NpgsqlCommand(strSql, cn))
                {
                    cmd.Parameters.AddWithValue("settleCode", settleCode);
                    using (NpgsqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            list.Add(new HisFeeId
                            {
                                FeiYongId = dr.IsDBNull(0) ? "" : dr.GetValue(0).ToString(),
                                FeiYongMxId = dr.IsDBNull(1) ? "" : dr.GetValue(1).ToString()
                            });
                        }
                    }
                }
            }

            return list;
        }
    }
}
