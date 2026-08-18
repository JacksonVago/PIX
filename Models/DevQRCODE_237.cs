    using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class DevQRCODE_237
    {
        public string id { get; set; }
        public string rtrid { get; set; }
        public double valor { get; set; }
        public string status { get; set; }
        
        public Horario horario { get; set; }

        public DevQRCODE_237()
        {
            horario = new Horario();
        }
    }
}
