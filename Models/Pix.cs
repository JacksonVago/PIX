using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class Pix
    {
        public string endToEndId { get; set; }
        public string tx_id { get; set; }
        public double valor { get; set; }
        public DateTime horario { get; set; }

        public Devedor pagador { get; set; }

        public string infoPagador { get; set; }

        public List<Devolucoes> devolucoes { get; set; }
        public Pix()
        {            
            pagador = new Devedor();
            devolucoes = new List<Devolucoes>();

        }

    }
}
