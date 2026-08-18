using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class ConsListQRCODE_237
    {
        public Parametros parametros { get; set; }
        public List<Cobs> cobs { get; set; }
        public ConsListQRCODE_237()
        {
            parametros = new Parametros();
            cobs = new List<Cobs>();
        }
    }
}
