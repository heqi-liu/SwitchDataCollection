using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchDataCollection.PLCHelper
{  /// <summary>
   /// 高低字节转换工具
   /// </summary>
    public static class FinsConverter
    {
        public static byte[] ReverseWord(byte[] data)
        {
            if (data == null || data.Length < 2)
                return data;

            byte[] res = (byte[])data.Clone();
            for (int i = 0; i < res.Length; i += 2)
            {
                if (i + 1 >= res.Length)
                    break;

                byte temp = res[i];
                res[i] = res[i + 1];
                res[i + 1] = temp;
            }
            return res;
        }

        /// <summary>
        /// 获得反馈指定错误消息描述
        /// </summary>
        /// <param name="errCode1"></param>
        /// <param name="errCode2"></param>
        /// <returns></returns>
        public static string GetErrorMessage(byte errCode1, byte errCode2)
        {
            string msg = $"错误代码【{errCode1:X2} {errCode2:X2}】";
            switch (errCode1)
            {
                case 0x00:
                    if (errCode2 == 0x01)
                        msg += "service canceled";
                    break;
                case 0x01:
                    switch (errCode2)
                    {
                        case 0x01: msg += "Ip配置错误:local node not in network"; break;
                        case 0x02: msg += "权限出错:token timeout"; break;
                        case 0x03: msg += "retries failed"; break;
                        case 0x04: msg += "too many send frames"; break;
                        case 0x05: msg += "node address range error"; break;
                        case 0x06: msg += "node address duplication"; break;
                    }
                    break;
                case 0x02:
                    switch (errCode2)
                    {
                        case 0x01: msg += "destination node not in network"; break;
                        case 0x02: msg += "unit missing"; break;
                        case 0x03: msg += "third node missing"; break;
                        case 0x04: msg += "destination node busy"; break;
                        case 0x05: msg += "response timeout"; break;
                    }
                    break;
                case 0x03:
                    switch (errCode2)
                    {
                        case 0x01: msg += "communications controller error"; break;
                        case 0x02: msg += "CPU unit error"; break;
                        case 0x03: msg += "控制器错误:controller error"; break;
                        case 0x04: msg += "unit number error"; break;
                    }
                    break;
                case 0x04:
                    switch (errCode2)
                    {
                        case 0x01: msg += "未定义的指令:undefined command"; break;
                        case 0x02: msg += "不支持的模式:not supported by model/version"; break;
                    }
                    break;
                case 0x05:
                    switch (errCode2)
                    {
                        case 0x01: msg += "destination address setting error"; break;
                        case 0x02: msg += "no routing tables"; break;
                        case 0x03: msg += "routing table error"; break;
                        case 0x04: msg += "too many relays"; break;
                    }
                    break;
                case 0x10:
                    switch (errCode2)
                    {
                        case 0x01: msg += "指令太长:command too long"; break;
                        case 0x02: msg += "command too short"; break;
                        case 0x03: msg += "数据与长度不匹配:elements/data don't match"; break;
                        case 0x04: msg += "command format error"; break;
                        case 0x05: msg += "header error"; break;
                    }
                    break;
                case 0x11:
                    switch (errCode2)
                    {
                        case 0x01: msg += "area classification missing"; break;
                        case 0x02: msg += "access size error"; break;
                        case 0x03: msg += "address range error"; break;
                        case 0x04: msg += "address range exceeded"; break;
                        case 0x06: msg += "program missing"; break;
                        case 0x09: msg += "relational error"; break;
                        case 0x0a: msg += "duplicate data access"; break;
                        case 0x0b: msg += "response too long"; break;
                        case 0x0c: msg += "parameter error"; break;
                    }
                    break;
                case 0x20:
                    switch (errCode2)
                    {
                        case 0x02: msg += "protected"; break;
                        case 0x03: msg += "table missing"; break;
                        case 0x04: msg += "data missing"; break;
                        case 0x05: msg += "program missing"; break;
                        case 0x06: msg += "file missing"; break;
                        case 0x07: msg += "data mismatch"; break;
                    }
                    break;
                case 0x21:
                    switch (errCode2)
                    {
                        case 0x01: msg += "read-only"; break;
                        case 0x02: msg += "protected,cannot write data link table"; break;
                        case 0x03: msg += "cannot register"; break;
                        case 0x05: msg += "program missing"; break;
                        case 0x06: msg += "file missing"; break;
                        case 0x07: msg += "file name already exists"; break;
                        case 0x08: msg += "cannot change"; break;
                    }
                    break;
                case 0x22:
                    switch (errCode2)
                    {
                        case 0x01: msg += "not possible during execution"; break;
                        case 0x02: msg += "not possible while running"; break;
                        case 0x03: msg += "wrong PLC mode"; break;
                        case 0x04: msg += "wrong PLC mode"; break;
                        case 0x05: msg += "wrong PLC mode"; break;
                        case 0x06: msg += "wrong PLC mode"; break;
                        case 0x07: msg += "specified node not polling node"; break;
                        case 0x08: msg += "step cannot be executed"; break;
                    }
                    break;
                case 0x23:
                    switch (errCode2)
                    {
                        case 0x01: msg += "file device missing"; break;
                        case 0x02: msg += "memory missing"; break;
                        case 0x03: msg += "clock missing"; break;
                    }
                    break;
                case 0x24:
                    if (errCode2 == 0x01) msg += "table missing";
                    break;
                case 0x25:
                    switch (errCode2)
                    {
                        case 0x02: msg += "memory error"; break;
                        case 0x03: msg += "I/O setting error"; break;
                        case 0x04: msg += "too many I/O points"; break;
                        case 0x05: msg += "CPU bus error"; break;
                        case 0x06: msg += "I/O duplication"; break;
                        case 0x07: msg += "CPU bus error"; break;
                        case 0x09: msg += "SYSMAC BUS/2 error"; break;
                        case 0x0a: msg += "CPU bus unit error"; break;
                        case 0x0d: msg += "SYSMAC BUS No. duplication"; break;
                        case 0x0f: msg += "memory error"; break;
                        case 0x10: msg += "SYSMAC BUS terminator missing"; break;
                    }
                    break;
                case 0x26:
                    switch (errCode2)
                    {
                        case 0x01: msg += "no protection"; break;
                        case 0x02: msg += "incorrect password"; break;
                        case 0x04: msg += "protected"; break;
                        case 0x05: msg += "service already executing"; break;
                        case 0x06: msg += "service stopped"; break;
                        case 0x07: msg += "no execution right"; break;
                        case 0x08: msg += "settings required before execution"; break;
                        case 0x09: msg += "necessary items not set"; break;
                        case 0x0a: msg += "number already defined"; break;
                        case 0x0b: msg += "error will not clear"; break;
                    }
                    break;
                case 0x30:
                    if (errCode2 == 0x01)
                        msg += "无访问权限:no access right";
                    break;
                case 0x40:
                    if (errCode2 == 0x01)
                        msg += "服务未开启:service aborted";
                    break;
            }
            return msg;
        }


    }
}
