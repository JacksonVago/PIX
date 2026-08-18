using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class ListQRCODE_341
    {
        public ParametrosItau parametros { get; set; }
        public List<CobsItau> cobs { get; set; }
        public ListQRCODE_341()
        {
            parametros = new ParametrosItau();
            cobs = new List<CobsItau>();
        }
    }
}
