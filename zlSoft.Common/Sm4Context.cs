using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace zlSoft.Common
{
    public class Sm4Context
    {
        public int mode;
        public long[] sk;
        public bool isPadding;
        public Sm4Context()
        {
            this.mode = 1;
            this.isPadding = true;
            this.sk = new long[32];
        }
    }
}
