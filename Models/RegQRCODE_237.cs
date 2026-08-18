using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class RegQRCODE_237
    {
        public Calendario calendario { get; set; }

        public string status { get; set; }
        public string tx_id { get; set; }
        public int revisao { get; set; }
        public string location { get; set; }

        public Devedor devedor { get; set; }

        public Valor valor { get; set; }

        public string chave_pix { get; set; }
        public string solicitacaopagador { get; set; }

        public List<Info_adicionais> info_adicionais { get; set; }

        public RegQRCODE_237()
        {
            calendario = new Calendario();
            devedor = new Devedor();
            valor = new Valor();
            info_adicionais = new List<Info_adicionais>();            
        }

    }
}
