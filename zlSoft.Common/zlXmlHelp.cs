using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Diagnostics;

namespace zlSoft.Common
{
    public class zlXmlHelp
    {
        /// <summary>
        /// 加载xml并做必要检测
        /// </summary>
        /// <param name="InputXmlDoc"> 返回的xml对象</param>
        /// <param name="strInput">xml字符串</param>
        /// <param name="errMsg">错误信息</param>
        /// <returns></returns>
        public static bool LoadInPars(ref XmlDocument InputXmlDoc, string strInput, ref string errMsg)
        {
            //只能直接载入整个XML信息,如果使用CreateElement的话会提示报错:包括<>符号
            string BusinessNumber = "";
            string TerminalNumber = "";
            string CooperationUnit = "";
            try
            {
                InputXmlDoc.LoadXml(strInput);
            }
            catch
            {
                errMsg = "入参的XML格式不正确!";
                return false;
            }

            ////读取交易号
            if (!GetXMLNode(ref InputXmlDoc, "InHead", "BusinessNumber", ref BusinessNumber))
            {
                errMsg = BusinessNumber;
                return false;
            }

            ////读取终端编号
            if (!GetXMLNode(ref InputXmlDoc, "InHead", "TerminalNumber", ref TerminalNumber))
            {
                errMsg = TerminalNumber;
                return false;
            }

            ////读取合作单位
            if (!GetXMLNode(ref InputXmlDoc, "InHead", "CooperationUnit", ref CooperationUnit))
            {
                errMsg = CooperationUnit;
                return false;
            }

            //效验终端编号、合作单位
            //using (OracleCommand cmd = new OracleCommand("", con))
            //    {
            //    cmd.CommandText = "Select Nvl(Max(状态),'无效') As 状态 From 一卡通合作单位 Where 终端编号 = '" +
            //                        TerminalNumber + "' And 合作单位 = '" + CooperationUnit + "'";
            //    Reader = cmd.ExecuteReader();
            //    Reader.Read();
            //    string strState = Reader["状态"].ToString();
            //    Reader.Close();
            //    if (strState != "启用")
            //        {
            //        errMsg = strState + "的合作单位和终端编号！" + CooperationUnit + "," + TerminalNumber;
            //        return false;
            //        }
            //    else
            //        {
            //        return true;
            //        }
            //    }
            return true;
        }
        /// <summary>
        /// 解析XML
        /// </summary>
        /// <param name="InputXmlDoc">输入xml</param>
        /// <param name="NodeType">节点名称</param>
        /// <param name="NodeName"></param>
        /// <param name="strValue"></param>
        /// <returns></returns>
        public static bool GetXMLNode(ref XmlDocument InputXmlDoc, string NodeType, string NodeName, ref string strValue)
        {
            try
            {
                strValue = InputXmlDoc.SelectSingleNode("Input").SelectSingleNode(NodeType).SelectSingleNode(NodeName).InnerText;
            }
            catch
            {
                strValue = "缺少[" + NodeName + "]参数";
                return false;
            }
            return true;
        }
        /// <summary>
        /// 创建一个xmlelement对象
        /// </summary>
        /// <param name="LevelElement">element对象名称</param>
        /// <param name="myXmlDoc">xmldom</param>
        /// <param name="NodeName">节点名称</param>
        /// <param name="NodeValue">节点值</param>
        public static void CreateNode(ref XmlElement LevelElement, ref XmlDocument myXmlDoc, string NodeName, string NodeValue)
        {
            //初始化一个子节点
            LevelElement = myXmlDoc.CreateElement(NodeName);
            //填充节点的属性值（SetAttribute）
            //LevelElement.SetAttribute("ID", "11111111");
            //LevelElement.SetAttribute("Description", "Made in China");                
            if (NodeValue != "")
            {
                LevelElement.InnerText = NodeValue;
            }
        }
    }
}
