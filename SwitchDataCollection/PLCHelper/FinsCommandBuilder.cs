using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchDataCollection.PLCHelper
{
    internal static class FinsCommandBuilder
    {
        public static byte GetAreaCode(FinsMemoryArea area, bool isBit)
        {
            if (isBit)
                return GetBitAreaCode(area);
            else
                return GetWordAreaCode(area);
        }

        private static byte GetBitAreaCode(FinsMemoryArea area)
        {
            switch (area)
            {
                case FinsMemoryArea.CIO: return 0x30;
                case FinsMemoryArea.WR: return 0x31;
                case FinsMemoryArea.DM: return 0x02;
                case FinsMemoryArea.HR: return 0x32;
                case FinsMemoryArea.TIM: return 0x09;
                case FinsMemoryArea.AR: return 0x33;
                case FinsMemoryArea.CNT: return 0x09;
                default: return 0x00;
            }
        }

        private static byte GetWordAreaCode(FinsMemoryArea area)
        {
            switch (area)
            {
                case FinsMemoryArea.CIO: return 0xB0;
                case FinsMemoryArea.WR: return 0xB1;
                case FinsMemoryArea.DM: return 0x82;
                case FinsMemoryArea.HR: return 0xB2;
                case FinsMemoryArea.TIM: return 0x89;
                case FinsMemoryArea.CNT: return 0x89;
                case FinsMemoryArea.AR: return 0xB3;
                case FinsMemoryArea.E0: return 0xA0;
                case FinsMemoryArea.E1: return 0xA1;
                case FinsMemoryArea.E2: return 0xA2;
                case FinsMemoryArea.E3: return 0xA3;
                case FinsMemoryArea.E4: return 0xA4;
                case FinsMemoryArea.E5: return 0xA5;
                case FinsMemoryArea.E6: return 0xA6;
                case FinsMemoryArea.E7: return 0xA7;
                default: return 0x00;
            }
        }
    }
}
