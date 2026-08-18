using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class DevQRCODE_341
    {
        public string id { get; set; }
        public string rtrid { get; set; }
        public double valor { get; set; }
        public string natureza { get; set; }
        public string descricao { get; set; }
        public string motivo { get; set; }
        public string status { get; set; }

        public Horario horario { get; set; }

        public DevQRCODE_341()
        {
            horario = new Horario();
        }
    }
}
