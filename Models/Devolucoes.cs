using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class Devolucoes
    {
        public string Id { get; set; }
        public string rtrId { get; set; }

        public double valor { get; set; }

        public Horario horario { get; set; }

        public string status { get; set; }

        public Devolucoes()
        {
            horario = new Horario();
        }

    }
}
