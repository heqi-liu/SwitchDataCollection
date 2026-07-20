using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchDataCollection.PLCHelper
{
    /// <summary>
    /// Omron PLC地址类型【存储区域类别】
    /// </summary>
    public enum FinsMemoryArea
    {
        /// <summary>
        /// C区：I/O继电器区 【Input Output Area】 
        /// 输入区Input 1600 bits=100 word 范围【CIO0~CIO99】
        /// 输出区Output 1600 bits=100 word 范围【CIO100~CIO199】
        /// CPU Bus Unit Area 6400 bits=400Word 范围【CIO1500~CIO1899】
        /// </summary>
        [Description("CIO 区")]
        CIO = 0,
        /// <summary>
        /// W区：工作继电器区 【Work Relay Area】
        /// 8192 bits = 512 words 范围【W0~W511】
        /// </summary>
        [Description("WR 工作区")]
        WR = 1,
        /// <summary>
        /// D区：动态数据存储区，仅可由字（16位Word）进行存取【Data Memory Area】
        /// 32768Words 范围【D0~D32767】
        /// </summary>
        [Description("DM 数据区")]
        DM = 2,
        /// <summary>
        /// 保持继电器区 【Hold Relay】
        /// 8192 bits = 512 words 范围【H0~H511】
        /// </summary>
        [Description("HR 保持区")]
        HR = 3,
        /// <summary>
        /// 定时器区 【Timer】
        /// PVs 4096Words  范围【T0~T4095】
        /// CompletionFlag 4096bits 范围【T0~T4095】
        /// </summary>
        [Description("TIM 定时器")]
        TIM = 4,
        /// <summary>
        /// 特殊辅助继电器区 【Auxiliary Relay Area】
        /// ReadOnly 7168 bits=448Words 范围【A0~A447】
        /// Read-Write 8192 bits=512Words 范围【A448~A959】
        /// </summary>
        [Description("AR 辅助区")]
        AR = 5,
        /// <summary>
        /// 计数器区 【Counter】
        /// PVs 4096Words  范围【C0~C4095】
        /// CompletionFlag 4096bits 范围【C0~C4095】
        /// </summary>
        [Description("CNT 计数器")]
        CNT = 6,

        [Description("E0 扩展区")]
        E0 = 7,
        [Description("E1 扩展区")]
        E1 = 8,
        [Description("E2 扩展区")]
        E2 = 9,
        [Description("E3 扩展区")]
        E3 = 10,
        [Description("E4 扩展区")]
        E4 = 11,
        [Description("E5 扩展区")]
        E5 = 12,
        [Description("E6 扩展区")]
        E6 = 13,
        [Description("E7 扩展区")]
        E7 = 14
    }
}
