using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class CobsItau
    {
        public Calendario calendario { get; set; }
        public Devedor devedor { get; set; }
        public LocItau loc { get; set; }

        public string location { get; set; }
        public ValorItau valor { get; set; }
        public string chave { get; set; }
        public string solicitacaopagador { get; set; }
        public string txid { get; set; }
        public int revisao { get; set; }
        public string status { get; set; }

        public string pixCopiaECola { get; set; }
        
        public List<PixItau> pix { get; set; }

        public List<Info_adicionais> info_adicionais { get; set; }

        public CobsItau()
        {
            calendario = new Calendario();
            devedor = new Devedor();
            loc = new LocItau();
            valor = new ValorItau();
            info_adicionais = new List<Info_adicionais>();
            pix = new List<PixItau>();
        }
    }
}
