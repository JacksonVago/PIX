using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class Parametros
    {
        public DateTime data_inicial { get; set; }
        public DateTime data_final { get; set; }
        public string cpf { get; set; }
        public string cnpj { get; set; }

        public string status { get; set; }

        public Paginacao paginacao { get; set; }

        public Parametros()
        {
            paginacao = new Paginacao();
        }
    }
}
